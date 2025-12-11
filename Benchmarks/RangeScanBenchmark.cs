using System;
using System.Diagnostics;
using System.IO;
using ProfileManager.Profiles;

namespace ProfileManager.Benchmarks
{
    internal static class RangeScanBenchmark
    {
        // Run benchmark on separate data directories, multiple fields and sizes (no UI involved).
        public static void Run(string? root = null, int iterations = 3)
        {
            Console.WriteLine("Range Scan Benchmark");
            string baseDir = root ?? Path.Combine(AppContext.BaseDirectory, "bench_data");
            Directory.CreateDirectory(baseDir);
            Console.WriteLine($"Data directory: {baseDir}");

            // Allow env overrides to avoid huge runs by default
            int[] defaultSizes = {  1_000_000 };
            var sizes = ParseSizes(Environment.GetEnvironmentVariable("BENCH_SIZES")) ?? defaultSizes;
            bool includeMillion = string.Equals(Environment.GetEnvironmentVariable("BENCH_INCLUDE_MILLION"), "1", StringComparison.OrdinalIgnoreCase);
            if (includeMillion)
            {
                var list = new List<int>(sizes);
                list.Add(1_000_000);
                sizes = list.ToArray();
            }

            if (int.TryParse(Environment.GetEnvironmentVariable("BENCH_ITER"), out var parsedIter) && parsedIter > 0)
            {
                iterations = parsedIter;
            }

            string[] fields = { "HoVaTen", "QueQuan", "Lop", "MaSoBhyt", "NamSinh" };
            string logPath = Path.Combine(baseDir, $"bench_results_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log");
            using var log = new StreamWriter(File.Open(logPath, FileMode.Create, FileAccess.Write, FileShare.Read));
            log.WriteLine($"Benchmark started at {DateTime.UtcNow:u}");
            log.WriteLine($"Iterations per test: {iterations}");
            log.WriteLine($"Sizes: {string.Join(",", sizes)}");
            log.WriteLine($"Fields: {string.Join(",", fields)}");
            log.WriteLine();

            int totalSteps = sizes.Length * fields.Length;
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

                Console.WriteLine($"=== Size {size} ===");
                log.WriteLine($"=== Size {size} ===");

                foreach (var field in fields)
                {
                    var (min, max) = PickBounds(field);
                    double seq = TimeMs(iterations, () => repo.SearchRangeIds(field, min, max));
                    double par = TimeMs(iterations, () => repo.SearchRangeParallelIds(field, min, max));

                    string line = $"Field={field}\tSeq={seq:F2} ms\tPar={par:F2} ms";
                    Console.WriteLine(line);
                    log.WriteLine(line);

                    step++;
                    RenderProgress(step, totalSteps);
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

        private static (string? min, string? max) PickBounds(string field)
        {
            // simple bounds per field type; null/null means full scan
            return field.ToLowerInvariant() switch
            {
                "namsinh" => ("1900-01-01", "2100-12-31"),
                _ => ("A", "Zzzzzzz")
            };
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
    }
}
