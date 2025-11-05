using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BasicDataBase.FileIO;

namespace BasicDataBase.Table
{
    public sealed class TableDiagnosticsResult
    {
        public List<TableDiagnosticsTableResult> Tables { get; } = new List<TableDiagnosticsTableResult>();
        public List<string> CatalogIssues { get; } = new List<string>();
        public bool HasErrors => CatalogIssues.Count > 0 || Tables.Any(t => t.Errors.Count > 0);
        public bool HasWarnings => Tables.Any(t => t.Warnings.Count > 0);
    }

    public sealed class TableDiagnosticsTableResult
    {
        public TableDiagnosticsTableResult(string tableName, int rowCount)
        {
            TableName = tableName;
            RowCount = rowCount;
        }

        public string TableName { get; }
        public int RowCount { get; }
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public Dictionary<string, double> IndexBuildMs { get; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    }

    public static class TableDiagnostics
    {
        public static TableDiagnosticsResult Analyze(TableManager manager)
        {
            var result = new TableDiagnosticsResult();

            var catalogRecords = manager.GetAllRecords("__catalog");
            var userTables = manager.GetAllTableInfo();
            var userTableNames = new HashSet<string>(userTables.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);

            foreach (var entry in catalogRecords)
            {
                var tableName = entry.Length > 0 ? entry[0]?.ToString() : null;
                if (string.IsNullOrWhiteSpace(tableName))
                {
                    result.CatalogIssues.Add("Catalog entry with missing table name.");
                    continue;
                }

                if (!userTableNames.Contains(tableName))
                {
                    result.CatalogIssues.Add($"Catalog references '{tableName}' but table directory is missing.");
                }
            }

            foreach (var table in userTables)
            {
                var status = new TableDiagnosticsTableResult(table.Name, table.RowCount);
                result.Tables.Add(status);
                AnalyzeTable(manager, table, status);
            }

            return result;
        }

        public static void Print(TableDiagnosticsResult result)
        {
            Console.WriteLine("=== Table Diagnostics Report ===");

            if (result.CatalogIssues.Count == 0)
            {
                Console.WriteLine("Catalog: OK");
            }
            else
            {
                Console.WriteLine("Catalog issues:");
                foreach (var issue in result.CatalogIssues)
                {
                    Console.WriteLine($" - {issue}");
                }
            }

            foreach (var table in result.Tables)
            {
                Console.WriteLine($"Table '{table.TableName}' (rows={table.RowCount}):");
                if (table.Errors.Count == 0 && table.Warnings.Count == 0)
                {
                    Console.WriteLine("  OK");
                }
                else
                {
                    foreach (var error in table.Errors)
                    {
                        Console.WriteLine($"  ERROR: {error}");
                    }
                    foreach (var warning in table.Warnings)
                    {
                        Console.WriteLine($"  WARN: {warning}");
                    }
                }

                foreach (var kvp in table.IndexBuildMs)
                {
                    Console.WriteLine($"  Index '{kvp.Key}' rebuilt in {kvp.Value:F2} ms");
                }
            }

            if (result.Tables.Count == 0)
            {
                Console.WriteLine("No user tables detected under the active storage root.");
            }

            if (!result.HasErrors)
            {
                Console.WriteLine(result.HasWarnings ? "Diagnostics completed with warnings." : "Diagnostics completed without issues.");
            }
            else
            {
                Console.WriteLine("Diagnostics detected errors. Review the report above.");
            }
        }

        private static void AnalyzeTable(TableManager manager, TableInfo info, TableDiagnosticsTableResult status)
        {
            // Verify metadata
            try
            {
                var schemaLine = File.ReadLines(info.MetadataPath).FirstOrDefault();
                if (schemaLine == null)
                {
                    status.Errors.Add("Metadata file is empty.");
                }
                else if (!string.Equals(schemaLine, info.Schema.ToString(), StringComparison.Ordinal))
                {
                    status.Errors.Add("Metadata schema line does not match expected schema.");
                }
            }
            catch (Exception ex)
            {
                status.Errors.Add($"Failed to read metadata: {ex.Message}");
            }

            object?[,] rawRecords;
            try
            {
                rawRecords = FileIOManager.ReadAll(info.MetadataPath, info.DataPath);
            }
            catch (Exception ex)
            {
                status.Errors.Add($"Failed to read data file: {ex.Message}");
                return;
            }

            int rowCount = rawRecords.GetLength(0);
            if (rowCount != info.RowCount)
            {
                status.Warnings.Add($"Row count mismatch (metadata reports {info.RowCount}, actual {rowCount}).");
            }

            // Blob validation
            var blobFields = info.Schema.Fields.Select((field, idx) => new { field, idx })
                .Where(x => x.field.Type == FieldType.Blob)
                .ToList();

            if (blobFields.Count > 0)
            {
                var referencedBlobs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int r = 0; r < rowCount; r++)
                {
                    foreach (var blobField in blobFields)
                    {
                        if (rawRecords[r, blobField.idx] is string blobRef && !string.IsNullOrEmpty(blobRef))
                        {
                            referencedBlobs.Add(blobRef);
                        }
                    }
                }

                var blobDir = Path.Combine(info.Directory, "blobs");
                if (!Directory.Exists(blobDir))
                {
                    status.Errors.Add("Blob directory missing.");
                }
                else
                {
                    var actualFiles = Directory
                        .GetFiles(blobDir)
                        .Select(Path.GetFileName)
                        .Where(name => name != null)
                        .Select(name => name!)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    foreach (var referenced in referencedBlobs)
                    {
                        if (!actualFiles.Contains(referenced))
                        {
                            status.Errors.Add($"Referenced blob '{referenced}' not found on disk.");
                        }
                    }

                    foreach (var file in actualFiles)
                    {
                        if (!referencedBlobs.Contains(file))
                        {
                            status.Warnings.Add($"Blob file '{file}' is orphaned (no record references it).");
                        }
                    }
                }
            }

            // Index rebuild check
            for (int fieldIndex = 0; fieldIndex < info.Schema.Fields.Count; fieldIndex++)
            {
                var field = info.Schema.Fields[fieldIndex];
                try
                {
                    var sw = Stopwatch.StartNew();
                    manager.BuildIndex(info.Name, field.Name, force: true);
                    sw.Stop();
                    status.IndexBuildMs[field.Name] = sw.Elapsed.TotalMilliseconds;

                    if (rowCount > 0)
                    {
                        var sampleValue = rawRecords[0, fieldIndex]?.ToString();
                        if (!string.IsNullOrEmpty(sampleValue))
                        {
                            var hits = manager.SearchExact(info.Name, field.Name, sampleValue);
                            if (hits.Count == 0)
                            {
                                status.Warnings.Add($"Index '{field.Name}' missing sample key '{sampleValue}'.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    status.Errors.Add($"Index '{field.Name}' rebuild failed: {ex.Message}");
                }
            }

            // Hydration sanity check on first few records
            int sampleCount = Math.Min(rowCount, 5);
            for (int i = 0; i < sampleCount; i++)
            {
                try
                {
                    var hydrated = manager.GetRecord(info.Name, i);
                    if (hydrated == null)
                    {
                        status.Warnings.Add($"Unable to hydrate record at index {i}.");
                    }
                }
                catch (Exception ex)
                {
                    status.Errors.Add($"Hydration failed for record {i}: {ex.Message}");
                }
            }
        }
    }
}
