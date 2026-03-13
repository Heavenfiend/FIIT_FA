using System.Collections;
using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Interfaces;

namespace TreeDataStructures.Core;

public abstract class BinarySearchTreeBase<TKey, TValue, TNode>(IComparer<TKey>? comparer = null) 
    : ITree<TKey, TValue>
    where TNode : Node<TKey, TValue, TNode>
{
    protected TNode? Root;
    public IComparer<TKey> Comparer { get; protected set; } = comparer ?? Comparer<TKey>.Default; // use it to compare Keys

    public int Count { get; protected set; }
    
    public bool IsReadOnly => false;

    public ICollection<TKey> Keys => InOrder().Select(e => e.Key).ToList();
    public ICollection<TValue> Values => InOrder().Select(e => e.Value).ToList();
    
    
    public virtual void Add(TKey key, TValue value)
    {
        TNode z = CreateNode(key, value);
        TNode? y = null;
        TNode? x = Root;

        while (x != null)
        {
            y = x;
            int cmp = Comparer.Compare(z.Key, x.Key);
            if (cmp == 0)
            {
                x.Value = value;
                return;
            }
            else if (cmp < 0)
            {
                x = x.Left;
            }
            else
            {
                x = x.Right;
            }
        }

        z.Parent = y;
        if (y == null)
        {
            Root = z;
        }
        else if (Comparer.Compare(z.Key, y.Key) < 0)
        {
            y.Left = z;
        }
        else
        {
            y.Right = z;
        }

        Count++;
        OnNodeAdded(z);
    }

    
    public virtual bool Remove(TKey key)
    {
        TNode? node = FindNode(key);
        if (node == null) { return false; }

        RemoveNode(node);
        this.Count--;
        return true;
    }
    
    
    protected virtual void RemoveNode(TNode node)
    {
        TNode? parentToNotify;
        TNode? childToNotify;

        if (node.Left == null)
        {
            parentToNotify = node.Parent;
            childToNotify = node.Right;
            Transplant(node, node.Right);
            OnNodeRemoved(parentToNotify, childToNotify);
        }
        else if (node.Right == null)
        {
            parentToNotify = node.Parent;
            childToNotify = node.Left;
            Transplant(node, node.Left);
            OnNodeRemoved(parentToNotify, childToNotify);
        }
        else
        {
            TNode y = node.Right;
            while (y.Left != null)
            {
                y = y.Left;
            }

            if (y.Parent != node)
            {
                parentToNotify = y.Parent;
                childToNotify = y.Right;

                Transplant(y, y.Right);
                y.Right = node.Right;
                y.Right.Parent = y;
            }
            else
            {
                parentToNotify = y;
                childToNotify = y.Right;
            }

            Transplant(node, y);
            y.Left = node.Left;
            y.Left.Parent = y;

            OnNodeRemoved(parentToNotify, childToNotify);
        }
    }

    public virtual bool ContainsKey(TKey key) => FindNode(key) != null;
    
    public virtual bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        TNode? node = FindNode(key);
        if (node != null)
        {
            value = node.Value;
            return true;
        }
        value = default;
        return false;
    }

    public TValue this[TKey key]
    {
        get => TryGetValue(key, out TValue? val) ? val : throw new KeyNotFoundException();
        set => Add(key, value);
    }

    
    #region Hooks
    
    /// <summary>
    /// Вызывается после успешной вставки
    /// </summary>
    /// <param name="newNode">Узел, который встал на место</param>
    protected virtual void OnNodeAdded(TNode newNode) { }
    
    /// <summary>
    /// Вызывается после удаления. 
    /// </summary>
    /// <param name="parent">Узел, чей ребенок изменился</param>
    /// <param name="child">Узел, который встал на место удаленного</param>
    protected virtual void OnNodeRemoved(TNode? parent, TNode? child) { }
    
    #endregion
    
    
    #region Helpers
    protected abstract TNode CreateNode(TKey key, TValue value);
    
    
    protected TNode? FindNode(TKey key)
    {
        TNode? current = Root;
        while (current != null)
        {
            int cmp = Comparer.Compare(key, current.Key);
            if (cmp == 0) { return current; }
            current = cmp < 0 ? current.Left : current.Right;
        }
        return null;
    }

    protected void RotateLeft(TNode x)
    {
        TNode? y = x.Right;
        if (y == null) return;

        x.Right = y.Left;
        if (y.Left != null)
        {
            y.Left.Parent = x;
        }

        y.Parent = x.Parent;
        if (x.Parent == null)
        {
            Root = y;
        }
        else if (x == x.Parent.Left)
        {
            x.Parent.Left = y;
        }
        else
        {
            x.Parent.Right = y;
        }

        y.Left = x;
        x.Parent = y;
    }

    protected void RotateRight(TNode y)
    {
        TNode? x = y.Left;
        if (x == null) return;

        y.Left = x.Right;
        if (x.Right != null)
        {
            x.Right.Parent = y;
        }

        x.Parent = y.Parent;
        if (y.Parent == null)
        {
            Root = x;
        }
        else if (y == y.Parent.Right)
        {
            y.Parent.Right = x;
        }
        else
        {
            y.Parent.Left = x;
        }

        x.Right = y;
        y.Parent = x;
    }
    
    protected void RotateBigLeft(TNode x)
    {
        TNode? p = x.Right;
        if (p == null) return;
        RotateLeft(x);
        RotateLeft(p);
    }
    
    protected void RotateBigRight(TNode y)
    {
        TNode? p = y.Left;
        if (p == null) return;
        RotateRight(y);
        RotateRight(p);
    }
    
    protected void RotateDoubleLeft(TNode x)
    {
        if (x.Right == null) return;
        RotateRight(x.Right);
        RotateLeft(x);
    }
    
    protected void RotateDoubleRight(TNode y)
    {
        if (y.Left == null) return;
        RotateLeft(y.Left);
        RotateRight(y);
    }
    
    protected void Transplant(TNode u, TNode? v)
    {
        if (u.Parent == null)
        {
            Root = v;
        }
        else if (u.IsLeftChild)
        {
            u.Parent.Left = v;
        }
        else
        {
            u.Parent.Right = v;
        }
        v?.Parent = u.Parent;
    }
    #endregion
    
    public IEnumerable<TreeEntry<TKey, TValue>>  InOrder() => new TreeIterator(Root, TraversalStrategy.InOrder);
    public IEnumerable<TreeEntry<TKey, TValue>>  PreOrder() => new TreeIterator(Root, TraversalStrategy.PreOrder);
    public IEnumerable<TreeEntry<TKey, TValue>>  PostOrder() => new TreeIterator(Root, TraversalStrategy.PostOrder);
    public IEnumerable<TreeEntry<TKey, TValue>>  InOrderReverse() => new TreeIterator(Root, TraversalStrategy.InOrderReverse);
    public IEnumerable<TreeEntry<TKey, TValue>>  PreOrderReverse() => new TreeIterator(Root, TraversalStrategy.PreOrderReverse);
    public IEnumerable<TreeEntry<TKey, TValue>>  PostOrderReverse() => new TreeIterator(Root, TraversalStrategy.PostOrderReverse);
    
    /// <summary>
    /// Внутренний класс-итератор. 
    /// Реализует паттерн Iterator вручную, без yield return (ban).
    /// </summary>
    private struct TreeIterator : 
        IEnumerable<TreeEntry<TKey, TValue>>,
        IEnumerable<KeyValuePair<TKey, TValue>>,
        IEnumerator<TreeEntry<TKey, TValue>>,
        IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private readonly TraversalStrategy _strategy;
        private readonly TNode? _root;
        private TNode? _current;
        private TNode? _next;
        private bool _started;
        private readonly bool _isDictionaryIterator;

        public TreeIterator(TNode? root, TraversalStrategy strategy, bool isDictionaryIterator = false)
        {
            _root = root;
            _strategy = strategy;
            _current = null;
            _next = null;
            _started = false;
            _isDictionaryIterator = isDictionaryIterator;

            Initialize();
        }

        private void Initialize()
        {
             if (_root == null) return;

             switch (_strategy)
             {
                 case TraversalStrategy.PreOrder:
                     _next = _root;
                     break;
                 case TraversalStrategy.PreOrderReverse:
                     _next = GetPreOrderReverseFirst(_root);
                     break;

                 case TraversalStrategy.InOrder:
                     _next = GetLeftmost(_root);
                     break;
                 case TraversalStrategy.InOrderReverse:
                     _next = GetRightmost(_root);
                     break;

                 case TraversalStrategy.PostOrder:
                     _next = GetPostOrderFirst(_root);
                     break;
                 case TraversalStrategy.PostOrderReverse:
                     _next = _root;
                     break;
             }
        }
        
        private static TNode GetLeftmost(TNode node)
        {
            while (node.Left != null) node = node.Left;
            return node;
        }

        private static TNode GetRightmost(TNode node)
        {
            while (node.Right != null) node = node.Right;
            return node;
        }

        private static TNode GetPostOrderFirst(TNode node)
        {
            while (node.Left != null || node.Right != null)
            {
                if (node.Left != null) node = node.Left;
                else node = node.Right!;
            }
            return node;
        }

        private static TNode GetPreOrderReverseFirst(TNode node)
        {
            while (node.Right != null || node.Left != null)
            {
                if (node.Right != null) node = node.Right;
                else node = node.Left!;
            }
            return node;
        }

        public IEnumerator<TreeEntry<TKey, TValue>> GetEnumerator() => this;
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => this;
        
        public TreeEntry<TKey, TValue> Current
        {
            get
            {
                if (_current == null) throw new InvalidOperationException();
                int depth = 0;
                var p = _current.Parent;
                while (p != null) { depth++; p = p.Parent; }
                return new TreeEntry<TKey, TValue>(_current.Key, _current.Value, depth);
            }
        }
        
        KeyValuePair<TKey, TValue> IEnumerator<KeyValuePair<TKey, TValue>>.Current
        {
            get
            {
                if (_current == null) throw new InvalidOperationException();
                return new KeyValuePair<TKey, TValue>(_current.Key, _current.Value);
            }
        }

        object IEnumerator.Current => _isDictionaryIterator ? ((IEnumerator<KeyValuePair<TKey, TValue>>)this).Current : Current;
        
        public bool MoveNext()
        {
            if (!_started)
            {
                _started = true;
            }

            if (_next == null) return false;

            _current = _next;
            Advance();
            return true;
        }

        private void Advance()
        {
            if (_current == null) return;
            switch (_strategy)
            {
                case TraversalStrategy.PreOrder:
                    _next = GetNextPreOrder(_current);
                    break;
                case TraversalStrategy.PreOrderReverse:
                    _next = GetNextPreOrderReverse(_current);
                    break;
                case TraversalStrategy.InOrder:
                    _next = GetNextInOrder(_current);
                    break;
                case TraversalStrategy.InOrderReverse:
                    _next = GetNextInOrderReverse(_current);
                    break;
                case TraversalStrategy.PostOrder:
                    _next = GetNextPostOrder(_current);
                    break;
                case TraversalStrategy.PostOrderReverse:
                    _next = GetNextPostOrderReverse(_current);
                    break;
            }
        }

        private static TNode? GetNextInOrder(TNode node)
        {
            if (node.Right != null) return GetLeftmost(node.Right);
            var p = node.Parent;
            while (p != null && node == p.Right) { node = p; p = p.Parent; }
            return p;
        }

        private static TNode? GetNextInOrderReverse(TNode node)
        {
            if (node.Left != null) return GetRightmost(node.Left);
            var p = node.Parent;
            while (p != null && node == p.Left) { node = p; p = p.Parent; }
            return p;
        }

        private static TNode? GetNextPreOrder(TNode node)
        {
            if (node.Left != null) return node.Left;
            if (node.Right != null) return node.Right;
            var p = node.Parent;
            while (p != null) {
                if (node == p.Left && p.Right != null) return p.Right;
                node = p; p = p.Parent;
            }
            return null;
        }

        private static TNode? GetNextPostOrderReverse(TNode node)
        {
            if (node.Right != null) return node.Right;
            if (node.Left != null) return node.Left;
            var p = node.Parent;
            while (p != null) {
                if (node == p.Right && p.Left != null) return p.Left;
                node = p; p = p.Parent;
            }
            return null;
        }

        private static TNode? GetNextPostOrder(TNode node)
        {
            var p = node.Parent;
            if (p == null) return null;
            if (node == p.Right || p.Right == null) return p;
            return GetPostOrderFirst(p.Right);
        }

        private static TNode? GetNextPreOrderReverse(TNode node)
        {
            var p = node.Parent;
            if (p == null) return null;
            if (node == p.Left || p.Left == null) return p;
            return GetPreOrderReverseFirst(p.Left);
        }
        
        public void Reset()
        {
            _current = null;
            _next = null;
            _started = false;
            Initialize();
        }

        public void Dispose() { }
    }
    
    
    private enum TraversalStrategy { InOrder, PreOrder, PostOrder, InOrderReverse, PreOrderReverse, PostOrderReverse }
    
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => new TreeIterator(Root, TraversalStrategy.InOrder, isDictionaryIterator: true);
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
    public void Clear() { Root = null; Count = 0; }
    public bool Contains(KeyValuePair<TKey, TValue> item) => ContainsKey(item.Key);
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => throw new NotImplementedException();
    public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);
}