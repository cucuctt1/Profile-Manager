using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using BasicDataBase.FileIO;
using BasicDataBase.Index;

namespace BasicDataBase.Table
{
    public record TableInfo(string Name, string Directory, string MetadataPath, string DataPath, Schema Schema, int RowCount);

    // Manages table directories (metadata + data files) and keeps in-memory indexes in sync.
    public class TableManager
    {
        private const string CatalogFolderName = "__catalog";
        private const string CatalogSchemaString = "TableName:string:128,Schema:string:2048,BlobFields:string:1024,CreatedAt:datetime,UpdatedAt:datetime";

        private sealed record MaterializedRecord(object?[] Values, HashSet<string> BlobReferences);

        private readonly string _rootDirectory;
        private readonly Dictionary<string, TableDefinition> _tables = new(StringComparer.OrdinalIgnoreCase);
        private TableDefinition? _catalog;

        private TableDefinition Catalog => _catalog ?? throw new InvalidOperationException("Catalog not initialized");

        public TableManager(string? rootDirectory = null)
        {
            _rootDirectory = rootDirectory ?? Path.Combine(Environment.CurrentDirectory, "base", "Table");
            Directory.CreateDirectory(_rootDirectory);
            EnsureCatalog();
        }

        public IReadOnlyCollection<string> Tables
        {
            get
            {
                LoadAllDefinitions();
                return new ReadOnlyCollection<string>(new List<string>(_tables.Keys));
            }
        }

        public void CreateTable(string tableName, Schema schema)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name is required", nameof(tableName));
            if (schema == null) throw new ArgumentNullException(nameof(schema));
            if (IsCatalogName(tableName)) throw new InvalidOperationException("Table name conflicts with catalog storage");

            string tableDir = GetTableDirectory(tableName);
            if (Directory.Exists(tableDir)) throw new InvalidOperationException($"Table '{tableName}' already exists");

            Directory.CreateDirectory(tableDir);
            Directory.CreateDirectory(Path.Combine(tableDir, "blobs"));

            string metadataPath = Path.Combine(tableDir, "metadata.meta");
            string dataPath = Path.Combine(tableDir, "data.dat");

            var instruction = new SchemaInstruction(schema);
            MetaWriter.WriteMetaData(metadataPath, schema, instruction.ByteRule);
            using (File.Create(dataPath)) { }

            var definition = new TableDefinition(tableName, tableDir, metadataPath, dataPath, schema);
            _tables[tableName] = definition;
            AddCatalogEntry(definition);
        }

        public TableInfo GetCatalogInfo()
        {
            EnsureCatalog();
            return Catalog.AsInfo();
        }

        public TableInfo GetTableInfo(string tableName)
        {
            EnsureUserTable(tableName);
            var table = GetOrLoadTable(tableName);
            return table.AsInfo();
        }

        public IReadOnlyList<TableInfo> GetAllTableInfo()
        {
            LoadAllDefinitions();
            return _tables.Values.Select(t => t.AsInfo()).ToList();
        }

        public void BuildIndex(string tableName, string fieldName, bool force = false)
        {
            EnsureUserTable(tableName);
            var table = GetOrLoadTable(tableName);
            table.BuildIndex(fieldName, force);
        }

        public void DropTable(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name is required", nameof(tableName));
            if (IsCatalogName(tableName)) throw new InvalidOperationException("Cannot drop internal catalog table");
            var dir = GetTableDirectory(tableName);
            if (!Directory.Exists(dir)) return;

            RemoveCatalogEntry(tableName);
            _tables.Remove(tableName);
            Directory.Delete(dir, recursive: true);
        }

        public void InsertRecord(string tableName, object[] record)
        {
            EnsureUserTable(tableName);
            var table = GetOrLoadTable(tableName);
            ValidateRecord(table.Schema, record);
            var materialized = table.MaterializeRecord(record);
            FileIOManager.AppendRecord(table.MetadataPath, table.DataPath, materialized.Values);
            table.RowCount++;
            table.MarkIndexesDirty();
            TouchCatalog(tableName);
        }

        public void InsertRecords(string tableName, IEnumerable<object[]> records)
        {
            EnsureUserTable(tableName);
            if (records == null) throw new ArgumentNullException(nameof(records));
            foreach (var record in records)
            {
                InsertRecord(tableName, record);
            }
        }

        public object?[]? GetRecord(string tableName, int index)
        {
            if (IsCatalogName(tableName))
            {
                EnsureCatalog();
                return FileIOManager.ReadRecord(Catalog.MetadataPath, Catalog.DataPath, index);
            }
            var table = GetOrLoadTable(tableName);
            if (index < 0 || index >= table.RowCount) return null;
            var stored = FileIOManager.ReadRecord(table.MetadataPath, table.DataPath, index);
            if (stored == null) return null;
            return table.HydrateRecord(stored);
        }

        public IReadOnlyList<object?[]> GetAllRecords(string tableName)
        {
            if (IsCatalogName(tableName))
            {
                EnsureCatalog();
                var allCatalog = FileIOManager.ReadAll(Catalog.MetadataPath, Catalog.DataPath);
                var resultCatalog = new List<object?[]>();
                for (int r = 0; r < allCatalog.GetLength(0); r++)
                {
                    var row = new object?[allCatalog.GetLength(1)];
                    for (int c = 0; c < allCatalog.GetLength(1); c++) row[c] = allCatalog[r, c];
                    resultCatalog.Add(row);
                }
                return resultCatalog;
            }
            var table = GetOrLoadTable(tableName);
            var all = FileIOManager.ReadAll(table.MetadataPath, table.DataPath);
            var list = new List<object?[]>();
            for (int r = 0; r < all.GetLength(0); r++)
            {
                var row = new object?[all.GetLength(1)];
                for (int c = 0; c < all.GetLength(1); c++) row[c] = all[r, c];
                list.Add(table.HydrateRecord(row));
            }
            return list;
        }

        public void UpdateRecord(string tableName, int index, object[] newValues)
        {
            EnsureUserTable(tableName);
            var table = GetOrLoadTable(tableName);
            if (index < 0 || index >= table.RowCount) throw new ArgumentOutOfRangeException(nameof(index));
            ValidateRecord(table.Schema, newValues);
            var existingRecord = FileIOManager.ReadRecord(table.MetadataPath, table.DataPath, index);
            var oldRefs = table.ExtractBlobReferences(existingRecord);
            var materialized = table.MaterializeRecord(newValues);
            FileIOManager.EditRecord(table.MetadataPath, table.DataPath, index, materialized.Values);
            table.CleanupBlobs(oldRefs, materialized.BlobReferences);
            table.MarkIndexesDirty();
            TouchCatalog(tableName);
        }

        public void DeleteRecord(string tableName, int index)
        {
            EnsureUserTable(tableName);
            var table = GetOrLoadTable(tableName);
            if (index < 0 || index >= table.RowCount) throw new ArgumentOutOfRangeException(nameof(index));
            var existingRecord = FileIOManager.ReadRecord(table.MetadataPath, table.DataPath, index);
            var oldRefs = table.ExtractBlobReferences(existingRecord);
            FileIOManager.DeleteRecordByIndex(table.MetadataPath, table.DataPath, index);
            table.RowCount = Math.Max(0, table.RowCount - 1);
            table.CleanupBlobs(oldRefs, null);
            table.MarkIndexesDirty();
            TouchCatalog(tableName);
        }

        public void DeleteRecords(string tableName, IEnumerable<int> indexes)
        {
            EnsureUserTable(tableName);
            if (indexes == null) throw new ArgumentNullException(nameof(indexes));
            var list = new List<int>(indexes);
            list.Sort();
            list.Reverse();
            foreach (var idx in list)
            {
                DeleteRecord(tableName, idx);
            }
        }

        public List<int> SearchExact(string tableName, string fieldName, string key)
        {
            EnsureUserTable(tableName);
            var table = GetOrLoadTable(tableName);
            table.EnsureIndex(fieldName);
            return table.IndexManager.SearchExact(fieldName, key);
        }

        public List<int> SearchPrefix(string tableName, string fieldName, string prefix)
        {
            EnsureUserTable(tableName);
            var table = GetOrLoadTable(tableName);
            table.EnsureIndex(fieldName);
            return table.IndexManager.SearchPrefix(fieldName, prefix);
        }

        public List<int> SearchRange(string tableName, string fieldName, string? minKey, string? maxKey)
        {
            EnsureUserTable(tableName);
            var table = GetOrLoadTable(tableName);
            table.EnsureIndex(fieldName);
            return table.IndexManager.SearchRange(fieldName, minKey, maxKey);
        }
        
        public List<int> SearchGreaterThan(string tableName, string fieldName, string key, bool inclusive = false)
        {
            EnsureUserTable(tableName);
            var table = GetOrLoadTable(tableName);
            table.EnsureIndex(fieldName);
            return table.IndexManager.SearchGreaterThan(fieldName, key, inclusive);
        }
        
        public List<int> SearchLessThan(string tableName, string fieldName, string key, bool inclusive = false)
        {
            EnsureUserTable(tableName);
            var table = GetOrLoadTable(tableName);
            table.EnsureIndex(fieldName);
            return table.IndexManager.SearchLessThan(fieldName, key, inclusive);
        }
        
        public List<int> SearchTopK(string tableName, string fieldName, int k, bool descending = false)
        {
            EnsureUserTable(tableName);
            var table = GetOrLoadTable(tableName);
            table.EnsureIndex(fieldName);
            return table.IndexManager.SearchTopK(fieldName, k, descending);
        }

        public string? GetBlobPath(string tableName, int recordIndex, string fieldName)
        {
            EnsureUserTable(tableName);
            var table = GetOrLoadTable(tableName);
            if (!table.TryGetFieldIndex(fieldName, out var fieldIndex))
                throw new ArgumentException($"Field '{fieldName}' not found in schema");
            if (recordIndex < 0 || recordIndex >= table.RowCount) return null;
            var record = FileIOManager.ReadRecord(table.MetadataPath, table.DataPath, recordIndex);
            if (record == null || fieldIndex >= record.Length) return null;
            if (record[fieldIndex] is string blobRef && !string.IsNullOrEmpty(blobRef))
            {
                return table.ResolveBlobPath(blobRef);
            }
            return null;
        }

        public byte[]? ReadBlob(string tableName, int recordIndex, string fieldName)
        {
            EnsureUserTable(tableName);
            var path = GetBlobPath(tableName, recordIndex, fieldName);
            if (path == null || !File.Exists(path)) return null;
            return File.ReadAllBytes(path);
        }

        private TableDefinition GetOrLoadTable(string tableName)
        {
            if (IsCatalogName(tableName))
            {
                EnsureCatalog();
                return Catalog;
            }
            if (!_tables.TryGetValue(tableName, out var definition))
            {
                definition = LoadDefinition(tableName);
                _tables[tableName] = definition;
            }
            return definition;
        }

        private void LoadAllDefinitions()
        {
            foreach (var dir in Directory.GetDirectories(_rootDirectory))
            {
                var name = Path.GetFileName(dir);
                if (IsCatalogName(name)) continue;
                if (!_tables.ContainsKey(name))
                {
                    _tables[name] = LoadDefinition(name);
                }
            }
        }

        private TableDefinition LoadDefinition(string tableName)
        {
            if (IsCatalogName(tableName)) throw new InvalidOperationException("Catalog table is managed internally");
            string tableDir = GetTableDirectory(tableName);
            string metadataPath = Path.Combine(tableDir, "metadata.meta");
            string dataPath = Path.Combine(tableDir, "data.dat");
            if (!File.Exists(metadataPath)) throw new FileNotFoundException($"Metadata for table '{tableName}' not found", metadataPath);
            if (!File.Exists(dataPath)) throw new FileNotFoundException($"Data file for table '{tableName}' not found", dataPath);

            using var reader = new StreamReader(File.OpenRead(metadataPath));
            string? schemaLine = reader.ReadLine();
            if (schemaLine == null) throw new InvalidDataException($"Schema missing for table '{tableName}'");
            var schema = Schema.FromString(schemaLine);
            var definition = new TableDefinition(tableName, tableDir, metadataPath, dataPath, schema);
            return definition;
        }

        private string GetTableDirectory(string tableName)
        {
            return Path.Combine(_rootDirectory, tableName);
        }

        private static bool IsCatalogName(string tableName)
        {
            return tableName.Equals(CatalogFolderName, StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureUserTable(string tableName)
        {
            if (IsCatalogName(tableName))
                throw new InvalidOperationException("Operation not permitted on catalog table");
        }

        private static void ValidateRecord(Schema schema, object[] record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (schema.Fields.Count != record.Length)
                throw new ArgumentException($"Record field count ({record.Length}) does not match schema ({schema.Fields.Count})");

            for (int i = 0; i < schema.Fields.Count; i++)
            {
                var field = schema.Fields[i];
                var value = record[i];
                if (value == null) continue;
                switch (field.Type)
                {
                    case FieldType.Integer:
                        if (value is not int)
                            throw new ArgumentException($"Field '{field.Name}' requires int value");
                        break;
                    case FieldType.Boolean:
                        if (value is not bool)
                            throw new ArgumentException($"Field '{field.Name}' requires bool value");
                        break;
                    case FieldType.DateTime:
                        if (value is not DateTime)
                            throw new ArgumentException($"Field '{field.Name}' requires DateTime value");
                        break;
                    case FieldType.String:
                        if (value is not string s)
                            throw new ArgumentException($"Field '{field.Name}' requires string value");
                        if (field.MaxLength > 0 && s.Length > field.MaxLength)
                            throw new ArgumentException($"Field '{field.Name}' exceeds max length {field.MaxLength}");
                        break;
                    case FieldType.Blob:
                        if (value is not string && value is not byte[])
                            throw new ArgumentException($"Field '{field.Name}' requires blob path or bytes");
                        break;
                }
            }
        }

        private void EnsureCatalog()
        {
            if (_catalog != null) return;

            string catalogDir = GetTableDirectory(CatalogFolderName);
            string metadataPath = Path.Combine(catalogDir, "metadata.meta");
            string dataPath = Path.Combine(catalogDir, "data.dat");

            Directory.CreateDirectory(catalogDir);
            Directory.CreateDirectory(Path.Combine(catalogDir, "blobs"));

            var schema = Schema.FromString(CatalogSchemaString);
            if (!File.Exists(metadataPath))
            {
                var instruction = new SchemaInstruction(schema);
                MetaWriter.WriteMetaData(metadataPath, schema, instruction.ByteRule);
            }
            if (!File.Exists(dataPath))
            {
                using (File.Create(dataPath)) { }
            }

            _catalog = new TableDefinition(CatalogFolderName, catalogDir, metadataPath, dataPath, schema);
        }

        private void AddCatalogEntry(TableDefinition definition)
        {
            EnsureCatalog();
            var catalog = Catalog;
            var blobFields = string.Join("|", definition.Schema.Fields
                .Where(f => f.Type == FieldType.Blob)
                .Select(f => f.Name));
            var now = DateTime.UtcNow;
            var record = new object[]
            {
                definition.Name,
                definition.Schema.ToString(),
                blobFields,
                now,
                now
            };
            var materialized = catalog.MaterializeRecord(record);
            FileIOManager.AppendRecord(catalog.MetadataPath, catalog.DataPath, materialized.Values);
            catalog.RowCount++;
            catalog.MarkIndexesDirty();
        }

        private void RemoveCatalogEntry(string tableName)
        {
            EnsureCatalog();
            var catalog = Catalog;
            catalog.EnsureIndex("TableName");
            var matches = catalog.IndexManager.SearchExact("TableName", tableName);
            if (matches.Count == 0) return;

            matches.Sort();
            matches.Reverse();
            foreach (var idx in matches)
            {
                FileIOManager.DeleteRecordByIndex(catalog.MetadataPath, catalog.DataPath, idx);
                catalog.RowCount = Math.Max(0, catalog.RowCount - 1);
            }
            catalog.MarkIndexesDirty();
        }

        private void TouchCatalog(string tableName)
        {
            EnsureCatalog();
            var catalog = Catalog;
            catalog.EnsureIndex("TableName");
            var matches = catalog.IndexManager.SearchExact("TableName", tableName);
            if (matches.Count == 0) return;

            var index = matches[0];
            var record = FileIOManager.ReadRecord(catalog.MetadataPath, catalog.DataPath, index);
            if (record == null || record.Length < 5) return;
            record[4] = DateTime.UtcNow;
            FileIOManager.EditRecord(catalog.MetadataPath, catalog.DataPath, index, record);
            catalog.MarkIndexesDirty();
        }

        private sealed class TableDefinition
        {
            private readonly Dictionary<string, bool> _indexDirty = new(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> _fieldNames = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, int> _fieldIndexes = new(StringComparer.OrdinalIgnoreCase);
            private readonly List<int> _blobFieldIndexes = new();

            public TableDefinition(string name, string directory, string metadataPath, string dataPath, Schema schema)
            {
                Name = name;
                Directory = directory;
                MetadataPath = metadataPath;
                DataPath = dataPath;
                Schema = schema;
                IndexManager = new IndexManager(metadataPath, dataPath);
                BlobDirectory = Path.Combine(directory, "blobs");
                System.IO.Directory.CreateDirectory(BlobDirectory);
                for (int i = 0; i < schema.Fields.Count; i++)
                {
                    var field = schema.Fields[i];
                    _fieldNames.Add(field.Name);
                    _fieldIndexes[field.Name] = i;
                    if (field.Type == FieldType.Blob)
                        _blobFieldIndexes.Add(i);
                }
                RowCount = CountRecords(metadataPath, dataPath);
            }

            public string Name { get; }
            public string Directory { get; }
            public string BlobDirectory { get; }
            public string MetadataPath { get; }
            public string DataPath { get; }
            public Schema Schema { get; }
            public IndexManager IndexManager { get; }
            public int RowCount { get; set; }

            public void EnsureIndex(string fieldName)
            {
                if (!_fieldNames.Contains(fieldName))
                    throw new ArgumentException($"Field '{fieldName}' not found in schema");
                if (!_indexDirty.TryGetValue(fieldName, out var dirty) || dirty)
                {
                    IndexManager.BuildIndex(fieldName);
                    _indexDirty[fieldName] = false;
                }
            }

            public void BuildIndex(string fieldName, bool force)
            {
                if (!_fieldNames.Contains(fieldName))
                    throw new ArgumentException($"Field '{fieldName}' not found in schema");
                if (force)
                {
                    _indexDirty[fieldName] = true;
                }
                EnsureIndex(fieldName);
            }

            public void MarkIndexesDirty()
            {
                if (_indexDirty.Count == 0)
                {
                    foreach (var field in Schema.Fields)
                    {
                        _indexDirty[field.Name] = true;
                    }
                }
                else
                {
                    var keys = new List<string>(_indexDirty.Keys);
                    foreach (var key in keys) _indexDirty[key] = true;
                }
                IndexManager.DropAll();
            }

            public TableInfo AsInfo() => new TableInfo(Name, Directory, MetadataPath, DataPath, Schema, RowCount);

            public bool TryGetFieldIndex(string fieldName, out int index)
            {
                return _fieldIndexes.TryGetValue(fieldName, out index);
            }

            public MaterializedRecord MaterializeRecord(object[] record)
            {
                var values = new object?[record.Length];
                var blobRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < record.Length; i++)
                {
                    var field = Schema.Fields[i];
                    var value = record[i];
                    if (field.Type == FieldType.Blob)
                    {
                        values[i] = NormalizeBlobValue(field, value, blobRefs) ?? value;
                    }
                    else
                    {
                        values[i] = value;
                    }
                }
                return new MaterializedRecord(values, blobRefs);
            }

            public object?[] HydrateRecord(object?[] stored)
            {
                var hydrated = new object?[stored.Length];
                Array.Copy(stored, hydrated, stored.Length);
                foreach (var index in _blobFieldIndexes)
                {
                    if (index < hydrated.Length && hydrated[index] is string blobRef && !string.IsNullOrEmpty(blobRef))
                    {
                        var path = ResolveBlobPath(blobRef);
                        if (path != null)
                        {
                            hydrated[index] = path;
                        }
                    }
                }
                return hydrated;
            }

            public HashSet<string> ExtractBlobReferences(object?[]? record)
            {
                var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (record == null) return refs;
                foreach (var index in _blobFieldIndexes)
                {
                    if (index < record.Length && record[index] is string blobRef && !string.IsNullOrEmpty(blobRef))
                    {
                        refs.Add(blobRef);
                    }
                }
                return refs;
            }

            public void CleanupBlobs(HashSet<string> oldRefs, HashSet<string>? keepRefs)
            {
                foreach (var blob in oldRefs)
                {
                    if (keepRefs != null && keepRefs.Contains(blob)) continue;
                    var path = ResolveBlobPath(blob);
                    if (path != null && File.Exists(path))
                    {
                        try { File.Delete(path); } catch { }
                    }
                }
            }

            public string? ResolveBlobPath(string? reference)
            {
                if (string.IsNullOrEmpty(reference)) return null;
                if (Path.IsPathRooted(reference)) return reference;
                var blobPath = Path.Combine(BlobDirectory, reference);
                if (File.Exists(blobPath)) return blobPath;
                var tableRelative = Path.Combine(Directory, reference);
                return File.Exists(tableRelative) ? tableRelative : blobPath;
            }

            private string? NormalizeBlobValue(Field field, object? value, HashSet<string> blobRefs)
            {
                if (value == null) return null;

                if (value is byte[] bytes)
                {
                    string fileName = $"{field.Name}_{Guid.NewGuid():N}.bin";
                    string destination = Path.Combine(BlobDirectory, fileName);
                    File.WriteAllBytes(destination, bytes);
                    blobRefs.Add(fileName);
                    return fileName;
                }

                if (value is string str)
                {
                    if (string.IsNullOrEmpty(str))
                    {
                        return str;
                    }

                    if (IsStoredBlobReference(str))
                    {
                        blobRefs.Add(str);
                        return str;
                    }

                    string candidate = str;
                    if (!Path.IsPathRooted(candidate))
                    {
                        candidate = Path.GetFullPath(candidate);
                    }

                    if (File.Exists(candidate))
                    {
                        string fileName = $"{field.Name}_{Guid.NewGuid():N}{Path.GetExtension(candidate)}";
                        string destination = Path.Combine(BlobDirectory, fileName);
                        File.Copy(candidate, destination, overwrite: true);
                        blobRefs.Add(fileName);
                        return fileName;
                    }

                    blobRefs.Add(str);
                    return str;
                }

                throw new ArgumentException($"Unsupported blob value type '{value.GetType()}' for field '{field.Name}'");
            }

            private bool IsStoredBlobReference(string reference)
            {
                if (string.IsNullOrEmpty(reference)) return false;
                if (Path.IsPathRooted(reference)) return false;
                var blobPath = Path.Combine(BlobDirectory, reference);
                if (File.Exists(blobPath)) return true;
                var tableRelative = Path.Combine(Directory, reference);
                return File.Exists(tableRelative);
            }

            private static int CountRecords(string metadataPath, string dataPath)
            {
                if (!File.Exists(dataPath)) return 0;
                var (_, fieldCount) = FileIOManager.LoadSchemaAndFieldCount(metadataPath);
                int count = 0;
                using var fs = File.OpenRead(dataPath);
                using var reader = new BinaryReader(fs);
                while (fs.Position < fs.Length)
                {
                    bool truncated = false;
                    for (int i = 0; i < fieldCount; i++)
                    {
                        if (fs.Position + 4 > fs.Length) { truncated = true; break; }
                        int len = reader.ReadInt32();
                        if (len < 0) len = 0;
                        if (fs.Position + len > fs.Length) { truncated = true; break; }
                        fs.Seek(len, SeekOrigin.Current);
                    }
                    if (truncated) break;
                    count++;
                }
                return count;
            }
        }
    }
}
