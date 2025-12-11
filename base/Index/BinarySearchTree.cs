using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BasicDataBase.Index
{
    // Simple binary search tree keyed by strings, storing lists of record ids per key.
    public class BinarySearchTree
    {
        private Node? root;

        // Build a balanced tree from pre-sorted key/id pairs (keys sorted ascending).
        public void RebuildBalancedFrom(List<KeyValuePair<string, int>> sortedEntries)
        {
            if (sortedEntries == null) throw new ArgumentNullException(nameof(sortedEntries));
            if (sortedEntries.Count == 0)
            {
                root = null;
                return;
            }

            // group duplicate keys to avoid repeated nodes
            var grouped = new List<KeyValuePair<string, List<int>>>();
            string currentKey = sortedEntries[0].Key ?? string.Empty;
            var bucket = new List<int> { sortedEntries[0].Value };
            for (int i = 1; i < sortedEntries.Count; i++)
            {
                var (k, v) = sortedEntries[i];
                k ??= string.Empty;
                if (string.Compare(k, currentKey, StringComparison.Ordinal) == 0)
                {
                    bucket.Add(v);
                }
                else
                {
                    grouped.Add(new KeyValuePair<string, List<int>>(currentKey, bucket));
                    currentKey = k;
                    bucket = new List<int> { v };
                }
            }
            grouped.Add(new KeyValuePair<string, List<int>>(currentKey, bucket));

            root = BuildBalanced(grouped, 0, grouped.Count - 1);
        }

        public void Insert(string key, int recordId) // add a record id to the key
        {
            key ??= string.Empty;
            root = Insert(root, key, recordId);
        }

        public bool Delete(string key, int recordId) // remove record
        {
            key ??= string.Empty;
            root = Delete(root, key, recordId, out bool removed);
            return removed;
        }

        public List<int> Search(string key) // get list of record ids for the key
        {
            key ??= string.Empty; // nếu key là null, gán nó thành chuỗi rỗng
            var node = FindNode(root, key); // tìm nút với khóa tương ứng
            return node == null ? new List<int>() : new List<int>(node.Values); // nếu nút không tồn tại, trả về danh sách rỗng, ngược lại trả về danh sách recordId
        }

        public List<int> SearchPrefix(string prefix) // get list of record ids for keys with the prefix
        {
            prefix ??= string.Empty; // nếu prefix là null, gán nó thành chuỗi rỗng
            var upper = PrefixUpperBound(prefix);
            var result = new List<int>();
            foreach (var kvp in traverse(prefix, upper, minInclusive: true, maxInclusive: false))
            {
                result.AddRange(kvp.Value);
            }
            return result;
        }

        public List<int> SearchRange(string? minKey, string? maxKey) // get list of record ids for keys in the range [minKey, maxKey]
        {
            var result = new List<int>(); // tạo danh sách kết quả
            foreach (var kvp in traverse(minKey, maxKey)) // duyệt các cặp khóa-giá trị trong phạm vi
            {
                result.AddRange(kvp.Value); // thêm các recordId vào danh sách kết quả
            }
            return result;
        }

        // Parallel range scan: split traversal into chunks and flatten in order
        public List<int> SearchRangeParallel(string? minKey, string? maxKey, int? degree = null)
        {
            var nodes = traverse(minKey, maxKey).ToList();
            if (nodes.Count == 0) return new List<int>();

            // If total payload is small, avoid parallel overhead
            int totalValues = nodes.Sum(n => n.Value.Count);
            if (totalValues < 10_000) // tuned for small/medium datasets
            {
                var small = new List<int>(totalValues);
                foreach (var kvp in nodes) small.AddRange(kvp.Value);
                return small;
            }

            int processorCount = degree.HasValue && degree.Value > 0 ? degree.Value : Environment.ProcessorCount;
            if (processorCount <= 1)
            {
                var fallback = new List<int>(totalValues);
                foreach (var kvp in nodes) fallback.AddRange(kvp.Value);
                return fallback;
            }

            int chunks = Math.Max(1, Math.Min(processorCount, (nodes.Count + 255) / 256));
            var partials = new List<int>[chunks];

            Parallel.For(0, chunks, i =>
            {
                int start = i * nodes.Count / chunks;
                int end = (i + 1) * nodes.Count / chunks;
                int localCap = 0;
                for (int j = start; j < end; j++) localCap += nodes[j].Value.Count;
                var local = new List<int>(localCap);
                for (int j = start; j < end; j++)
                {
                    local.AddRange(nodes[j].Value);
                }
                partials[i] = local;
            });

            var result = new List<int>(totalValues);
            for (int i = 0; i < chunks; i++)
            {
                if (partials[i] != null) result.AddRange(partials[i]);
            }
            return result;
        }

        public void Clear()
        {
            root = null;
        }

        public IEnumerable<KeyValuePair<string, IReadOnlyList<int>>> traverse(string? minKey = null, string? maxKey = null, bool minInclusive = true, bool maxInclusive = true)
        {
            foreach (var node in traversenodes(root, minKey, maxKey, minInclusive, maxInclusive))
            {
                yield return new KeyValuePair<string, IReadOnlyList<int>>(node.Key, node.Values);
            }
        }

        private Node? Insert(Node? node, string key, int recordId) // insert record node
        {
            if (node == null) return new Node(key, recordId); // nếu nút hiện tại là null, tạo nút mới với khóa và recordId

            int cmp = string.Compare(key, node.Key, StringComparison.Ordinal); // so sánh khóa cần chèn với khóa của nút hiện tại
            if (cmp == 0)
            {
                if (!node.Values.Contains(recordId)) node.Values.Add(recordId); // nếu khóa đã tồn tại, thêm recordId vào danh sách Values nếu chưa có
            }
            else if (cmp < 0)
            {
                node.Left = Insert(node.Left, key, recordId); // đệ quy vào cây con bên trái
            }
            else
            {
                node.Right = Insert(node.Right, key, recordId); // đệ quy vào cây con bên phải
            }
            return node;
        }

        private Node? Delete(Node? node, string key, int recordId, out bool removed) // remove record node
        {
            if (node == null) // kiểm tra nếu nút hiện tại là null
            {
                removed = false;
                return null;
            }

            int cmp = string.Compare(key, node.Key, StringComparison.Ordinal); // so sánh khóa cần xóa với khóa của nút hiện tại
            if (cmp < 0) // nếu khóa cần xóa nhỏ hơn khóa của nút hiện tại
            {
                node.Left = Delete(node.Left, key, recordId, out removed); // đệ quy vào cây con bên trái
                return node;
            }
            if (cmp > 0) // nếu khóa cần xóa lớn hơn khóa của nút hiện tại
            {
                node.Right = Delete(node.Right, key, recordId, out removed); // đệ quy vào cây con bên phải
                return node;
            }

            int index = node.Values.IndexOf(recordId); // tìm chỉ mục của recordId trong danh sách Values
            if (index >= 0) // nếu tìm thấy recordId trong danh sách Values
            {
                node.Values.RemoveAt(index); // xóa recordId khỏi danh sách Values
                removed = true;
            }
            else
            {
                removed = false; // nếu không tìm thấy recordId, đặt removed thành false
                return node;
            }

            if (node.Values.Count > 0) // nếu danh sách Values vẫn còn phần tử
            {
                return node;
            }

            if (node.Left == null) return node.Right; // nếu nút bên trái là null, trả về nút bên phải
            if (node.Right == null) return node.Left; // nếu nút bên phải là null, trả về nút bên trái


            // nút có hai con, tìm người kế vị (successor)
            Node successor = findmin(node.Right);
            node.Key = successor.Key;
            node.Values = new List<int>(successor.Values);
            node.Right = removeMin(node.Right);
            return node;
        }

        private Node? removeMin(Node? node) // remove the minimum node private method
        {
            if (node == null) return null;
            if (node.Left == null) return node.Right;
            node.Left = removeMin(node.Left);
            return node;
        }

        private Node findmin(Node node)
        {
            while (node.Left != null) node = node.Left;
            return node;
        }

        private Node? FindNode(Node? node, string key)
        {
            while (node != null)
            {
                int cmp = string.Compare(key, node.Key, StringComparison.Ordinal);
                if (cmp == 0) return node;
                node = cmp < 0 ? node.Left : node.Right;
            }
            return null;
        }

        private IEnumerable<Node> traversenodes(Node? node, string? minKey, string? maxKey, bool minInclusive, bool maxInclusive)
        {
            if (node == null) yield break;

            int cmpMin = minKey == null ? 1 : string.Compare(node.Key, minKey, StringComparison.Ordinal);
            int cmpMax = maxKey == null ? -1 : string.Compare(node.Key, maxKey, StringComparison.Ordinal);

            if (minKey == null || cmpMin > 0)
            {
                foreach (var left in traversenodes(node.Left, minKey, maxKey, minInclusive, maxInclusive))
                    yield return left;
            }

            bool minOk = minKey == null || cmpMin > 0 || (minInclusive && cmpMin == 0);
            bool maxOk = maxKey == null || cmpMax < 0 || (maxInclusive && cmpMax == 0);
            if (minOk && maxOk)
            {
                yield return node;
            }

            if (maxKey == null || cmpMax < 0)
            {
                foreach (var right in traversenodes(node.Right, minKey, maxKey, minInclusive, maxInclusive))
                    yield return right;
            }
        }

        private static string PrefixUpperBound(string prefix)
        {
            // simple upper bound: prefix + char.MaxValue ensures all strings starting with prefix are < upper
            return prefix + char.MaxValue;
        }

        private Node BuildBalanced(List<KeyValuePair<string, List<int>>> items, int lo, int hi)
        {
            if (lo > hi) return null!;
            int mid = lo + ((hi - lo) / 2);
            var kv = items[mid];
            var node = new Node(kv.Key, kv.Value);
            if (lo < mid) node.Left = BuildBalanced(items, lo, mid - 1);
            if (mid < hi) node.Right = BuildBalanced(items, mid + 1, hi);
            return node;
        }

        private sealed class Node
        {
            public Node(string key, int recordId)
            {
                Key = key;
                Values = new List<int> { recordId };
            }

            public Node(string key, List<int> values)
            {
                Key = key;
                Values = new List<int>(values);
            }
            public string Key { get; set; }
            public List<int> Values { get; set; }
            public Node? Left { get; set; }
            public Node? Right { get; set; }
        }
    }
}
