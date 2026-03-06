using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.AVL;

public class AvlTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, AvlNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override AvlNode<TKey, TValue> CreateNode(TKey key, TValue value)
        => new(key, value);
    
    protected override void OnNodeAdded(AvlNode<TKey, TValue> newNode)
    {
        var current = newNode.Parent;
        while (current != null)
        {
            var parent = current.Parent;
            Balance(current);
            current = parent;
        }
    }

    protected override void OnNodeRemoved(AvlNode<TKey, TValue>? parent, AvlNode<TKey, TValue>? child)
    {
        var current = parent;
        while (current != null)
        {
            var nextParent = current.Parent;
            Balance(current);
            current = nextParent;
        }
    }

    private int GetHeight(AvlNode<TKey, TValue>? node)
    {
        return node?.Height ?? 0;
    }

    private int GetBalanceFactor(AvlNode<TKey, TValue>? node)
    {
        if (node == null) return 0;
        return GetHeight(node.Right) - GetHeight(node.Left);
    }

    private void UpdateHeight(AvlNode<TKey, TValue>? node)
    {
        if (node == null) return;
        node.Height = Math.Max(GetHeight(node.Left), GetHeight(node.Right)) + 1;
    }

    private void Balance(AvlNode<TKey, TValue> node)
    {
        UpdateHeight(node);
        int balanceFactor = GetBalanceFactor(node);

        if (balanceFactor == 2)
        {
            if (GetBalanceFactor(node.Right) < 0)
            {
                var right = node.Right;
                RotateRight(right!);
                UpdateHeight(right);
                UpdateHeight(node.Right);
            }
            RotateLeft(node);
            UpdateHeight(node);
            UpdateHeight(node.Parent);
        }
        else if (balanceFactor == -2)
        {
            if (GetBalanceFactor(node.Left) > 0)
            {
                var left = node.Left;
                RotateLeft(left!);
                UpdateHeight(left);
                UpdateHeight(node.Left);
            }
            RotateRight(node);
            UpdateHeight(node);
            UpdateHeight(node.Parent);
        }
    }
}