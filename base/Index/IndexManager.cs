using System;
using System.Collections.Generic;
using BasicDataBase.FileIO;

namespace BasicDataBase.Index
{
    // High-level index manager for tables. Builds an index on a named field
    // and provides search helpers that return record indexes or full records.
    public class IndexManager
    {
        private readonly string metadataPath;
        private readonly string dataPath;
        private readonly Dictionary<string, BPlusTree> indexes = new Dictionary<string, BPlusTree>(StringComparer.OrdinalIgnoreCase);

        public IndexManager(string metadataPath, string dataPath)
        {
            this.metadataPath = metadataPath ?? throw new ArgumentNullException(nameof(metadataPath));
            this.dataPath = dataPath ?? throw new ArgumentNullException(nameof(dataPath));
        }

        // Build an index on the named field (e.g., "username"), returns the number of indexed rows
        public int BuildIndex(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName)) throw new ArgumentException("fieldName required");

            var schema = Schema.FromString(System.IO.File.ReadAllText(metadataPath).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0]);
            int fieldPos = -1;
            for (int i = 0; i < schema.Fields.Count; i++)
            {
                if (schema.Fields[i].Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase)) { fieldPos = i; break; }
            }
            if (fieldPos < 0) throw new ArgumentException($"Field '{fieldName}' not found in schema");

            var all = FileIOManager.ReadAll(metadataPath, dataPath);
            var tree = new BPlusTree();
            for (int r = 0; r < all.GetLength(0); r++)
            {
                var key = all[r, fieldPos]?.ToString() ?? string.Empty;
                tree.Insert(key, r);
            }

            indexes[fieldName] = tree;
            return all.GetLength(0);
        }

        // Exact search on a previously built index. Returns record indexes.
        public List<int> SearchExact(string fieldName, string key)
        {
            if (!indexes.TryGetValue(fieldName, out var tree)) throw new InvalidOperationException($"Index for '{fieldName}' not built");
            return tree.Search(key);
        }

        // Prefix search on a previously built index. Returns record indexes.
        public List<int> SearchPrefix(string fieldName, string prefix)
        {
            if (!indexes.TryGetValue(fieldName, out var tree)) throw new InvalidOperationException($"Index for '{fieldName}' not built");
            return tree.SearchPrefix(prefix);
        }

        // Range search inclusive on the given index. Returns record indexes.
        public List<int> SearchRange(string fieldName, string? minKey, string? maxKey)
        {
            if (!indexes.TryGetValue(fieldName, out var tree)) throw new InvalidOperationException($"Index for '{fieldName}' not built");
            return tree.SearchRange(minKey, maxKey);
        }

        // Greater than search. If inclusive is true, includes records equal to key.
        public List<int> SearchGreaterThan(string fieldName, string key, bool inclusive = false)
        {
            if (!indexes.TryGetValue(fieldName, out var tree)) throw new InvalidOperationException($"Index for '{fieldName}' not built");
            if (key == null) throw new ArgumentNullException(nameof(key));
            var result = tree.SearchRange(key, null);
            if (!inclusive && result.Count > 0)
            {
                var equals = tree.Search(key);
                if (equals.Count > 0)
                {
                    var set = new HashSet<int>(equals);
                    result.RemoveAll(set.Contains);
                }
            }
            return result;
        }

        // Less than search. If inclusive is true, includes records equal to key.
        public List<int> SearchLessThan(string fieldName, string key, bool inclusive = false)
        {
            if (!indexes.TryGetValue(fieldName, out var tree)) throw new InvalidOperationException($"Index for '{fieldName}' not built");
            if (key == null) throw new ArgumentNullException(nameof(key));
            var result = tree.SearchRange(null, key);
            if (!inclusive && result.Count > 0)
            {
                var equals = tree.Search(key);
                if (equals.Count > 0)
                {
                    var set = new HashSet<int>(equals);
                    result.RemoveAll(set.Contains);
                }
            }
            return result;
        }

        // Return up to K record indexes ordered by key (ascending by default).
        public List<int> SearchTopK(string fieldName, int k, bool descending = false)
        {
            if (k <= 0) return new List<int>();
            if (!indexes.TryGetValue(fieldName, out var tree)) throw new InvalidOperationException($"Index for '{fieldName}' not built");

            if (!descending)
            {
                var result = new List<int>(k);
                foreach (var kvp in tree.Traverse())
                {
                    foreach (var id in kvp.Value)
                    {
                        result.Add(id);
                        if (result.Count == k) return result;
                    }
                }
                return result;
            }
            else
            {
                var all = new List<int>();
                foreach (var kvp in tree.Traverse())
                {
                    all.AddRange(kvp.Value);
                }
                if (all.Count == 0) return all;
                all.Reverse();
                if (all.Count > k)
                {
                    return all.GetRange(0, k);
                }
                return all;
            }
        }

        // Drop an index so it can be rebuilt fresh on next request.
        public void DropIndex(string fieldName)
        {
            indexes.Remove(fieldName);
        }

        // Clear all indexes (e.g., after bulk mutation).
        public void DropAll()
        {
            indexes.Clear();
        }

        // Convenience: return records (object arrays) for exact search results
        public List<object?[]?> SearchRecordsExact(string fieldName, string key)
        {
            var ids = SearchExact(fieldName, key);
            var result = new List<object?[]?>();
            foreach (var id in ids)
            {
                result.Add(FileIOManager.ReadRecord(metadataPath, dataPath, id));
            }
            return result;
        }
    }
}
