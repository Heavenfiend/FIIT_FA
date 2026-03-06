using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Implementations.BST;

namespace TreeDataStructures.Implementations.Splay;

public class SplayTree<TKey, TValue> : BinarySearchTree<TKey, TValue>
    where TKey : IComparable<TKey>
{
    protected override BstNode<TKey, TValue> CreateNode(TKey key, TValue value)
        => new(key, value);
    
    protected override void OnNodeAdded(BstNode<TKey, TValue> newNode)
    {
        Splay(newNode);
    }
    
    protected override void OnNodeRemoved(BstNode<TKey, TValue>? parent, BstNode<TKey, TValue>? child)
    {
        if (parent != null)
        {
            Splay(parent);
        }
    }
    
    public override bool ContainsKey(TKey key)
    {
        return FindNodeAndSplay(key) != null;
    }

    public override bool Remove(TKey key)
    {
        if (Root == null) return false;

        var node = FindNodeAndSplay(key);
        if (node == null || Comparer.Compare(node.Key, key) != 0)
        {
            return false;
        }

        if (node.Left == null)
        {
            Transplant(node, node.Right);
        }
        else if (node.Right == null)
        {
            Transplant(node, node.Left);
        }
        else
        {
            var y = node.Right;
            while (y.Left != null)
            {
                y = y.Left;
            }

            if (y.Parent != node)
            {
                Transplant(y, y.Right);
                y.Right = node.Right;
                y.Right.Parent = y;
            }

            Transplant(node, y);
            y.Left = node.Left;
            y.Left.Parent = y;
        }

        Count--;
        return true;
    }

    public override bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        var node = FindNodeAndSplay(key);
        if (node != null)
        {
            value = node.Value;
            return true;
        }
        value = default;
        return false;
    }
    
    private BstNode<TKey, TValue>? FindNodeAndSplay(TKey key)
    {
        var node = Root;
        BstNode<TKey, TValue>? parent = null;
        
        while (node != null)
        {
            int cmp = Comparer.Compare(key, node.Key);
            if (cmp == 0)
            {
                Splay(node);
                return node;
            }
            parent = node;
            node = cmp < 0 ? node.Left : node.Right;
        }

        if (parent != null)
        {
            Splay(parent);
        }

        return null;
    }

    private void Splay(BstNode<TKey, TValue> x)
    {
        while (x.Parent != null)
        {
            if (x.Parent.Parent == null)
            {
                if (x == x.Parent.Left)
                {
                    RotateRight(x.Parent);
                }
                else
                {
                    RotateLeft(x.Parent);
                }
            }
            else if (x == x.Parent.Left && x.Parent == x.Parent.Parent.Left)
            {
                RotateRight(x.Parent.Parent);
                RotateRight(x.Parent);
            }
            else if (x == x.Parent.Right && x.Parent == x.Parent.Parent.Right)
            {
                RotateLeft(x.Parent.Parent);
                RotateLeft(x.Parent);
            }
            else if (x == x.Parent.Right && x.Parent == x.Parent.Parent.Left)
            {
                RotateLeft(x.Parent);
                RotateRight(x.Parent);
            }
            else
            {
                RotateRight(x.Parent);
                RotateLeft(x.Parent);
            }
        }
    }
}
