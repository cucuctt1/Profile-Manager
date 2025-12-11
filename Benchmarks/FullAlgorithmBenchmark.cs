using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ProfileManager.Profiles;
using BasicDataBase.Table;

namespace ProfileManager.Benchmarks
{
    internal static class FullAlgorithmBenchmark
    {
        // Benchmark all index search variants (non-blob) across multiple fields and sizes.
        public static void Run(string? root = null, int iterations = 3)
        {
            Console.WriteLine("Full Algorithm Benchmark (search ops, no blob)");
            string baseDir = root ?? Path.Combine(AppContext.BaseDirectory, "bench_data_full");
            Directory.CreateDirectory(baseDir);
            Console.WriteLine($"Data directory: {baseDir}");

            int[] defaultSizes = { 1_000, 10_000,100000, 500_000, 1_000_000 };
            var sizes = ParseSizes(Environment.GetEnvironmentVariable("BENCH2_SIZES")) ?? defaultSizes;
            if (int.TryParse(Environment.GetEnvironmentVariable("BENCH2_ITER"), out var parsedIter) && parsedIter > 0)
            {
                iterations = parsedIter;
            }

            string[] fields = { "Id", "HoVaTen", "QueQuan", "Lop", "MaSoBhyt", "NamSinh" };
            string logPath = Path.Combine(baseDir, $"bench2_results_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log");
            using var log = new StreamWriter(File.Open(logPath, FileMode.Create, FileAccess.Write, FileShare.Read));
            log.WriteLine($"Benchmark started at {DateTime.UtcNow:u}");
            log.WriteLine($"Iterations per test: {iterations}");
            log.WriteLine($"Sizes: {string.Join(",", sizes)}");
            log.WriteLine($"Fields: {string.Join(",", fields)}");
            log.WriteLine();

            int totalSteps = sizes.Length * fields.Length * 6 * 3; // ops per field per 3 scenarios
            int step = 0;
            Console.WriteLine("Starting benchmark...");

            foreach (var size in sizes)
            {
                string dataDir = Path.Combine(baseDir, $"size_{size}");
                if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
                Directory.CreateDirectory(dataDir);

                var repo = new ProfileRepository(dataDir);
                repo.WipeAll();
                var swSeed = Stopwatch.StartNew();
                ProfileDataSeeder.Seed(repo, size);
                swSeed.Stop();
                Console.WriteLine($"Seeded {size} rows in {swSeed.Elapsed.TotalSeconds:F2}s");
                log.WriteLine($"Seeded {size} rows in {swSeed.Elapsed.TotalSeconds:F2}s");

                // Use TableManager directly for all index APIs
                var tableManager = new TableManager(dataDir);
                const string tableName = "Profiles";

                Console.WriteLine($"=== Size {size} ===");
                log.WriteLine($"=== Size {size} ===");

                foreach (var field in fields)
                {
                    var scenarios = BuildScenarios(tableManager, tableName, field);

                    foreach (var sc in scenarios)
                    {
                        double exact = TimeMs(iterations, () => tableManager.SearchExact(tableName, field, sc.key));
                        double prefixMs = TimeMs(iterations, () => tableManager.SearchPrefix(tableName, field, sc.prefix));
                        double range = TimeMs(iterations, () => tableManager.SearchRange(tableName, field, sc.min, sc.max));
                        double rangePar = TimeMs(iterations, () => tableManager.SearchRangeParallel(tableName, field, sc.min, sc.max));
                        double gt = TimeMs(iterations, () => tableManager.SearchGreaterThan(tableName, field, sc.key, inclusive: false));
                        double lt = TimeMs(iterations, () => tableManager.SearchLessThan(tableName, field, sc.key, inclusive: false));
                        double topK = TimeMs(iterations, () => tableManager.SearchTopK(tableName, field, 20, descending: false));

                        string line1 = $"Field={field}\tCase={sc.label}\tExact={exact:F2} ms\tPrefix={prefixMs:F2} ms\tRange={range:F2} ms\tRangePar={rangePar:F2} ms";
                        string line2 = $"Field={field}\tCase={sc.label}\tGreater={gt:F2} ms\tLess={lt:F2} ms\tTopK={topK:F2} ms\tKey='{Truncate(sc.key, 12)}'";
                        Console.WriteLine(line1);
                        Console.WriteLine(line2);
                        log.WriteLine(line1);
                        log.WriteLine(line2);
                        log.WriteLine();

                        step += 2;
                        RenderProgress(step, totalSteps);
                    }
                }
                Console.WriteLine();
                log.WriteLine();
            }

            Console.WriteLine($"Benchmark complete. Log: {logPath}");
            log.WriteLine($"Benchmark completed at {DateTime.UtcNow:u}");
        }

        private static double TimeMs(int iterations, Action action)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                action();
            }
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds / Math.Max(1, iterations);
        }

        private static List<(string label, string key, string prefix, string? min, string? max)> BuildScenarios(TableManager manager, string tableName, string fieldName)
        {
            var result = new List<(string, string, string, string?, string?)>();
            var info = manager.GetTableInfo(tableName);
            int idx = info.Schema.Fields.FindIndex(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) return result;

            int rowCount = info.RowCount;
            if (rowCount == 0) return result;

            var rng = new Random(12345 + fieldName.GetHashCode());

            string keyFirst = GetKeyAt(manager, tableName, idx, 0) ?? "A";
            string keyRandom = GetKeyAt(manager, tableName, idx, rng.Next(rowCount)) ?? keyFirst;
            string keyLast = GetKeyAt(manager, tableName, idx, rowCount - 1) ?? keyRandom;

            foreach (var item in new[]
            {
                ("First", keyFirst),
                ("Random", keyRandom),
                ("Last", keyLast),
            })
            {
                var prefix = PickPrefix(item.Item2);
                var (min, max) = PickBounds(fieldName, item.Item2);
                result.Add((item.Item1, item.Item2, prefix, min, max));
            }

            return result;
        }

        private static string? GetKeyAt(TableManager manager, string tableName, int fieldIndex, int recordIndex)
        {
            var record = manager.GetRecord(tableName, recordIndex);
            if (record == null || fieldIndex >= record.Length) return null;
            return record[fieldIndex]?.ToString();
        }

        private static string PickPrefix(string? sample)
        {
            if (string.IsNullOrEmpty(sample)) return "A";
            return sample.Length <= 2 ? sample : sample.Substring(0, 2);
        }

        private static (string? min, string? max) PickBounds(string field, string? sample)
        {
            if (field.Equals("NamSinh", StringComparison.OrdinalIgnoreCase))
            {
                return ("1900-01-01", "2100-12-31");
            }
            if (field.Equals("Id", StringComparison.OrdinalIgnoreCase))
            {
                return ("0", "9999999");
            }
            if (string.IsNullOrEmpty(sample))
            {
                return ("A", "Zzzzzzz");
            }
            // Range around sample for string fields
            var min = sample.Substring(0, Math.Max(1, Math.Min(2, sample.Length))) + "";
            return (min, "Zzzzzzz");
        }

        private static void RenderProgress(int step, int total)
        {
            int width = 40;
            double ratio = total == 0 ? 0 : (double)step / total;
            int filled = (int)(ratio * width);
            string bar = new string('#', filled).PadRight(width, '-');
            Console.Write($"\r[{bar}] {step}/{total}");
            if (step == total) Console.WriteLine();
        }

        private static int[]? ParseSizes(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var list = new List<int>();
            foreach (var p in parts)
            {
                if (int.TryParse(p, out var v) && v > 0) list.Add(v);
            }
            return list.Count == 0 ? null : list.ToArray();
        }

        private static string Truncate(string? value, int max)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= max ? value : value.Substring(0, max) + "…";
        }
    }
}