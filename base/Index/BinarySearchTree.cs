using System;
using System.Collections.Generic;

namespace BasicDataBase.Index
{
    // Simple binary search tree keyed by strings, storing lists of record ids per key.
    public class BinarySearchTree
    {
        private Node? root;

        public void Insert(string key, int recordId)
        {
            key ??= string.Empty;
            root = Insert(root, key, recordId);
        }

        public bool Delete(string key, int recordId)
        {
            key ??= string.Empty;
            root = Delete(root, key, recordId, out bool removed);
            return removed;
        }

        public List<int> Search(string key)
        {
            key ??= string.Empty;
            var node = FindNode(root, key);
            return node == null ? new List<int>() : new List<int>(node.Values);
        }

        public List<int> SearchPrefix(string prefix)
        {
            prefix ??= string.Empty;
            var result = new List<int>();
            foreach (var kvp in Traverse())
            {
                if (kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    result.AddRange(kvp.Value);
                }
                else if (string.Compare(kvp.Key, prefix, StringComparison.Ordinal) > 0)
                {
                    break;
                }
            }
            return result;
        }

        public List<int> SearchRange(string? minKey, string? maxKey)
        {
            var result = new List<int>();
            foreach (var kvp in Traverse(minKey, maxKey))
            {
                result.AddRange(kvp.Value);
            }
            return result;
        }

        public void Clear()
        {
            root = null;
        }

        public IEnumerable<KeyValuePair<string, IReadOnlyList<int>>> Traverse(string? minKey = null, string? maxKey = null, bool minInclusive = true, bool maxInclusive = true)
        {
            foreach (var node in TraverseNodes(root, minKey, maxKey, minInclusive, maxInclusive))
            {
                yield return new KeyValuePair<string, IReadOnlyList<int>>(node.Key, node.Values);
            }
        }

        private Node? Insert(Node? node, string key, int recordId)
        {
            if (node == null) return new Node(key, recordId);

            int cmp = string.Compare(key, node.Key, StringComparison.Ordinal);
            if (cmp == 0)
            {
                if (!node.Values.Contains(recordId)) node.Values.Add(recordId);
            }
            else if (cmp < 0)
            {
                node.Left = Insert(node.Left, key, recordId);
            }
            else
            {
                node.Right = Insert(node.Right, key, recordId);
            }
            return node;
        }

        private Node? Delete(Node? node, string key, int recordId, out bool removed)
        {
            if (node == null)
            {
                removed = false;
                return null;
            }

            int cmp = string.Compare(key, node.Key, StringComparison.Ordinal);
            if (cmp < 0)
            {
                node.Left = Delete(node.Left, key, recordId, out removed);
                return node;
            }
            if (cmp > 0)
            {
                node.Right = Delete(node.Right, key, recordId, out removed);
                return node;
            }

            int index = node.Values.IndexOf(recordId);
            if (index >= 0)
            {
                node.Values.RemoveAt(index);
                removed = true;
            }
            else
            {
                removed = false;
                return node;
            }

            if (node.Values.Count > 0)
            {
                return node;
            }

            if (node.Left == null) return node.Right;
            if (node.Right == null) return node.Left;

            Node successor = FindMin(node.Right);
            node.Key = successor.Key;
            node.Values = new List<int>(successor.Values);
            node.Right = RemoveMin(node.Right);
            return node;
        }

        private Node? RemoveMin(Node? node)
        {
            if (node == null) return null;
            if (node.Left == null) return node.Right;
            node.Left = RemoveMin(node.Left);
            return node;
        }

        private Node FindMin(Node node)
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

        private IEnumerable<Node> TraverseNodes(Node? node, string? minKey, string? maxKey, bool minInclusive, bool maxInclusive)
        {
            if (node == null) yield break;

            int cmpMin = minKey == null ? 1 : string.Compare(node.Key, minKey, StringComparison.Ordinal);
            int cmpMax = maxKey == null ? -1 : string.Compare(node.Key, maxKey, StringComparison.Ordinal);

            if (minKey == null || cmpMin > 0)
            {
                foreach (var left in TraverseNodes(node.Left, minKey, maxKey, minInclusive, maxInclusive))
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
                foreach (var right in TraverseNodes(node.Right, minKey, maxKey, minInclusive, maxInclusive))
                    yield return right;
            }
        }

        private sealed class Node
        {
            public Node(string key, int recordId)
            {
                Key = key;
                Values = new List<int> { recordId };
            }

            public string Key { get; set; }
            public List<int> Values { get; set; }
            public Node? Left { get; set; }
            public Node? Right { get; set; }
        }
    }
}
