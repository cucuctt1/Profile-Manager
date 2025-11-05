using System;
using System.Collections.Generic;

namespace BasicDataBase.Index
{
    // Balanced B+ tree for string keys -> lists of int record ids.
    // Supports insert, delete, exact search, prefix search, and range search.
    public class BPlusTree
    {
        private readonly int _maxKeys;
        private readonly int _minKeysInternal;
        private readonly int _minKeysLeaf;

        private Node _root;

        public BPlusTree(int order = 32)
        {
            if (order < 4) order = 4;
            _maxKeys = order - 1;
            _minKeysInternal = _maxKeys / 2;
            _minKeysLeaf = (_maxKeys + 1) / 2;
            _root = new LeafNode(order);
        }

        // Insert key -> recordId
        public void Insert(string key, int recordId)
        {
            if (key == null) key = string.Empty;
            var split = _root.Insert(key, recordId, _maxKeys, _minKeysInternal, _minKeysLeaf);
            if (split != null)
            {
                var newRoot = new InternalNode(_root.Order)
                {
                    Keys = { split.Key },
                    Children = { _root, split.RightNode }
                };
                _root.Parent = newRoot;
                split.RightNode.Parent = newRoot;
                _root = newRoot;
            }
        }

        // Delete a specific recordId for key. Returns true if removed.
        public bool Delete(string key, int recordId)
        {
            if (key == null) key = string.Empty;
            bool removed = _root.Delete(key, recordId, _maxKeys, _minKeysInternal, _minKeysLeaf);
            if (_root is InternalNode internalNode && internalNode.Keys.Count == 0)
            {
                if (internalNode.Children.Count > 0)
                {
                    _root = internalNode.Children[0];
                    _root.Parent = null;
                }
            }
            return removed;
        }

        // Exact match search
        public List<int> Search(string key)
        {
            if (key == null) key = string.Empty;
            return _root.Search(key);
        }

        // Prefix search
        public List<int> SearchPrefix(string prefix)
        {
            prefix ??= string.Empty;
            var result = new List<int>();
            if (_root is LeafNode leaf)
            {
                leaf.SearchPrefix(prefix, result);
            }
            else
            {
                var leafNode = _root.FindLeaf(prefix);
                leafNode.SearchPrefix(prefix, result);
            }
            return result;
        }

        // Range search inclusive
        public List<int> SearchRange(string? minKey, string? maxKey)
        {
            var result = new List<int>();
            Node leaf = _root;
            if (!(leaf is LeafNode)) leaf = _root.FindLeaf(minKey ?? string.Empty);
            ((LeafNode)leaf).SearchRange(minKey, maxKey, result);
            return result;
        }

        // Clear tree
        public void Clear()
        {
            _root = new LeafNode(_root.Order);
        }

        public IEnumerable<KeyValuePair<string, IReadOnlyList<int>>> Traverse(string? minKey = null, string? maxKey = null, bool minInclusive = true, bool maxInclusive = true)
        {
            LeafNode? current;
            int startIndex = 0;
            if (_root is LeafNode leaf)
            {
                current = leaf;
            }
            else
            {
                current = _root.FindLeaf(minKey ?? string.Empty);
            }

            if (minKey != null)
            {
                startIndex = current.FindFirstPosition(minKey, minInclusive);
                while (current != null && startIndex >= current.Keys.Count && current.Next != null)
                {
                    current = current.Next;
                    startIndex = current?.FindFirstPosition(minKey, minInclusive) ?? 0;
                }
            }
            else
            {
                current = _root.GetLeftmostLeaf();
                startIndex = 0;
            }

            while (current != null)
            {
                for (int i = startIndex; i < current.Keys.Count; i++)
                {
                    var key = current.Keys[i];
                    if (maxKey != null)
                    {
                        int cmp = string.Compare(key, maxKey, StringComparison.Ordinal);
                        if (cmp > 0 || (!maxInclusive && cmp == 0)) yield break;
                    }
                    yield return new KeyValuePair<string, IReadOnlyList<int>>(key, current.Values[i]);
                }
                current = current.Next;
                startIndex = 0;
            }
        }

        private abstract class Node
        {
            protected Node(int order)
            {
                Order = order;
                Keys = new List<string>();
            }

            public int Order { get; }
            public List<string> Keys { get; protected set; }
            public InternalNode? Parent { get; set; }

            public abstract SplitResult? Insert(string key, int value, int maxKeys, int minInternal, int minLeaf);
            public abstract bool Delete(string key, int value, int maxKeys, int minInternal, int minLeaf);
            public abstract List<int> Search(string key);
            public abstract LeafNode FindLeaf(string key);
            public abstract string? GetFirstKey();
            public abstract LeafNode GetLeftmostLeaf();
        }

        private sealed class InternalNode : Node
        {
            public InternalNode(int order) : base(order)
            {
                Children = new List<Node>();
            }

            public List<Node> Children { get; }

            public override SplitResult? Insert(string key, int value, int maxKeys, int minInternal, int minLeaf)
            {
                int childIndex = FindChildIndex(key);
                var child = Children[childIndex];
                var split = child.Insert(key, value, maxKeys, minInternal, minLeaf);
                if (split != null)
                {
                    Keys.Insert(childIndex, split.Key);
                    Children.Insert(childIndex + 1, split.RightNode);
                    split.RightNode.Parent = this;
                }

                if (Keys.Count > maxKeys)
                {
                    int mid = Keys.Count / 2;
                    string upKey = Keys[mid];

                    var right = new InternalNode(Order)
                    {
                        Parent = Parent
                    };

                    // move keys and children
                    for (int i = mid + 1; i < Keys.Count; i++)
                        right.Keys.Add(Keys[i]);
                    Keys.RemoveRange(mid, Keys.Count - mid);

                    for (int i = mid + 1; i < Children.Count; i++)
                    {
                        var childNode = Children[i];
                        childNode.Parent = right;
                        right.Children.Add(childNode);
                    }
                    Children.RemoveRange(mid + 1, Children.Count - (mid + 1));

                    return new SplitResult(upKey, right);
                }

                return null;
            }

            public override bool Delete(string key, int value, int maxKeys, int minInternal, int minLeaf)
            {
                int childIndex = FindChildIndex(key);
                var child = Children[childIndex];
                bool removed = child.Delete(key, value, maxKeys, minInternal, minLeaf);
                if (!removed) return false;

                // Check child for underflow and rebalance if needed
                if (child is LeafNode leafChild && leafChild.Keys.Count < minLeaf)
                {
                    childIndex = BalanceChild(childIndex, minInternal, minLeaf);
                }
                else if (child is InternalNode internalChild && internalChild.Keys.Count < minInternal)
                {
                    childIndex = BalanceChild(childIndex, minInternal, minLeaf);
                }

                RebuildKeys();

                // Root collapse handled by caller
                return true;
            }

            private int BalanceChild(int index, int minInternal, int minLeaf)
            {
                if (index > 0)
                {
                    var leftSibling = Children[index - 1];
                    var child = Children[index];
                    if (TryBorrowLeft(leftSibling, child, index - 1, minInternal, minLeaf)) return index;
                }

                if (index + 1 < Children.Count)
                {
                    var rightSibling = Children[index + 1];
                    var child = Children[index];
                    if (TryBorrowRight(child, rightSibling, index, minInternal, minLeaf)) return index;
                }

                if (index > 0)
                    return MergeChildren(index - 1);
                if (index + 1 < Children.Count)
                    return MergeChildren(index);

                return index;
            }

            private bool TryBorrowLeft(Node left, Node child, int separatorIndex, int minInternal, int minLeaf)
            {
                if (left is LeafNode leftLeaf && leftLeaf.Keys.Count > minLeaf)
                {
                    var leafChild = (LeafNode)child;
                    var movedKey = leftLeaf.Keys[^1];
                    var movedValues = leftLeaf.Values[^1];
                    leftLeaf.Keys.RemoveAt(leftLeaf.Keys.Count - 1);
                    leftLeaf.Values.RemoveAt(leftLeaf.Values.Count - 1);
                    leafChild.Keys.Insert(0, movedKey);
                    leafChild.Values.Insert(0, movedValues);
                    Keys[separatorIndex] = leafChild.Keys[0];
                    return true;
                }
                if (left is InternalNode leftInternal && leftInternal.Keys.Count > minInternal)
                {
                    var internalChild = (InternalNode)child;
                    internalChild.Keys.Insert(0, Keys[separatorIndex]);
                    Keys[separatorIndex] = leftInternal.Keys[^1];
                    internalChild.Children.Insert(0, leftInternal.Children[^1]);
                    internalChild.Children[0].Parent = internalChild;
                    leftInternal.Keys.RemoveAt(leftInternal.Keys.Count - 1);
                    leftInternal.Children.RemoveAt(leftInternal.Children.Count - 1);
                    return true;
                }
                return false;
            }

            private bool TryBorrowRight(Node child, Node right, int separatorIndex, int minInternal, int minLeaf)
            {
                if (right is LeafNode rightLeaf && rightLeaf.Keys.Count > minLeaf)
                {
                    var leafChild = (LeafNode)child;
                    var movedKey = rightLeaf.Keys[0];
                    var movedValues = rightLeaf.Values[0];
                    rightLeaf.Keys.RemoveAt(0);
                    rightLeaf.Values.RemoveAt(0);
                    leafChild.Keys.Add(movedKey);
                    leafChild.Values.Add(movedValues);
                    if (rightLeaf.Keys.Count > 0)
                        Keys[separatorIndex] = rightLeaf.Keys[0];
                    else
                        Keys[separatorIndex] = leafChild.Keys[^1];
                    return true;
                }
                if (right is InternalNode rightInternal && rightInternal.Keys.Count > minInternal)
                {
                    var internalChild = (InternalNode)child;
                    internalChild.Keys.Add(Keys[separatorIndex]);
                    Keys[separatorIndex] = rightInternal.Keys[0];
                    internalChild.Children.Add(rightInternal.Children[0]);
                    internalChild.Children[^1].Parent = internalChild;
                    rightInternal.Keys.RemoveAt(0);
                    rightInternal.Children.RemoveAt(0);
                    return true;
                }
                return false;
            }

            private int MergeChildren(int index)
            {
                var left = Children[index];
                var right = Children[index + 1];
                if (left is LeafNode leftLeaf && right is LeafNode rightLeaf)
                {
                    leftLeaf.Merge(rightLeaf);
                }
                else if (left is InternalNode leftInternal && right is InternalNode rightInternal)
                {
                    leftInternal.Keys.Add(Keys[index]);
                    leftInternal.Keys.AddRange(rightInternal.Keys);
                    foreach (var child in rightInternal.Children)
                    {
                        child.Parent = leftInternal;
                        leftInternal.Children.Add(child);
                    }
                }
                Keys.RemoveAt(index);
                Children.RemoveAt(index + 1);
                return index;
            }

            public override List<int> Search(string key)
            {
                var child = Children[FindChildIndex(key)];
                return child.Search(key);
            }

            public override LeafNode FindLeaf(string key)
            {
                return Children[FindChildIndex(key)].FindLeaf(key);
            }

            public override string? GetFirstKey()
            {
                if (Children.Count == 0) return null;
                return Children[0].GetFirstKey();
            }

            public override LeafNode GetLeftmostLeaf()
            {
                if (Children.Count == 0) throw new InvalidOperationException("Internal node with no children");
                return Children[0].GetLeftmostLeaf();
            }

            private void RebuildKeys()
            {
                for (int i = 0; i < Keys.Count; i++)
                {
                    var key = Children[i + 1].GetFirstKey();
                    if (key == null)
                    {
                        Keys.RemoveAt(i);
                        Children.RemoveAt(i + 1);
                        i--;
                    }
                    else
                    {
                        Keys[i] = key;
                    }
                }
            }

            private int FindChildIndex(string key)
            {
                int i = 0;
                while (i < Keys.Count && string.Compare(key, Keys[i], StringComparison.Ordinal) >= 0)
                    i++;
                return i;
            }
        }

        private sealed class LeafNode : Node
        {
            public LeafNode(int order) : base(order)
            {
                Values = new List<List<int>>();
            }

            public List<List<int>> Values { get; }
            public LeafNode? Next { get; set; }

            public override SplitResult? Insert(string key, int value, int maxKeys, int minInternal, int minLeaf)
            {
                int index = Keys.BinarySearch(key, StringComparer.Ordinal);
                if (index >= 0)
                {
                    Values[index].Add(value);
                }
                else
                {
                    int insertIndex = ~index;
                    Keys.Insert(insertIndex, key);
                    Values.Insert(insertIndex, new List<int> { value });
                }

                if (Keys.Count > maxKeys)
                {
                    int mid = Keys.Count / 2;
                    var right = new LeafNode(Order)
                    {
                        Parent = Parent,
                        Next = Next
                    };

                    for (int i = mid; i < Keys.Count; i++)
                    {
                        right.Keys.Add(Keys[i]);
                        right.Values.Add(Values[i]);
                    }
                    Keys.RemoveRange(mid, Keys.Count - mid);
                    Values.RemoveRange(mid, Values.Count - mid);

                    Next = right;
                    return new SplitResult(right.Keys[0], right);
                }

                return null;
            }

            public override bool Delete(string key, int value, int maxKeys, int minInternal, int minLeaf)
            {
                int index = Keys.BinarySearch(key, StringComparer.Ordinal);
                if (index < 0) return false;
                var list = Values[index];
                int vIndex = list.IndexOf(value);
                if (vIndex < 0) return false;
                list.RemoveAt(vIndex);
                if (list.Count == 0)
                {
                    Keys.RemoveAt(index);
                    Values.RemoveAt(index);
                }
                return true;
            }

            public override List<int> Search(string key)
            {
                int index = Keys.BinarySearch(key, StringComparer.Ordinal);
                if (index < 0) return new List<int>();
                return new List<int>(Values[index]);
            }

            public override LeafNode FindLeaf(string key)
            {
                return this;
            }

            public override string? GetFirstKey()
            {
                return Keys.Count > 0 ? Keys[0] : null;
            }

            public override LeafNode GetLeftmostLeaf()
            {
                return this;
            }

            public int FindFirstPosition(string key, bool inclusive)
            {
                int index = Keys.BinarySearch(key, StringComparer.Ordinal);
                if (index >= 0)
                {
                    return inclusive ? index : index + 1;
                }
                return ~index;
            }

            public void SearchPrefix(string prefix, List<int> result)
            {
                var current = this;
                while (current != null)
                {
                    for (int i = 0; i < current.Keys.Count; i++)
                    {
                        if (current.Keys[i].StartsWith(prefix, StringComparison.Ordinal))
                            result.AddRange(current.Values[i]);
                        else if (string.Compare(current.Keys[i], prefix, StringComparison.Ordinal) > 0 && !current.Keys[i].StartsWith(prefix, StringComparison.Ordinal))
                            return;
                    }
                    current = current.Next;
                }
            }

            public void SearchRange(string? minKey, string? maxKey, List<int> result)
            {
                var current = this;
                while (current != null)
                {
                    for (int i = 0; i < current.Keys.Count; i++)
                    {
                        var key = current.Keys[i];
                        if (minKey != null && string.Compare(key, minKey, StringComparison.Ordinal) < 0)
                            continue;
                        if (maxKey != null && string.Compare(key, maxKey, StringComparison.Ordinal) > 0)
                            return;
                        result.AddRange(current.Values[i]);
                    }
                    current = current.Next;
                }
            }

            public void Merge(LeafNode right)
            {
                for (int i = 0; i < right.Keys.Count; i++)
                {
                    Keys.Add(right.Keys[i]);
                    Values.Add(right.Values[i]);
                }
                Next = right.Next;
            }
        }

        private sealed class SplitResult
        {
            public SplitResult(string key, Node right)
            {
                Key = key;
                RightNode = right;
            }

            public string Key { get; }
            public Node RightNode { get; }
        }
    }
}
