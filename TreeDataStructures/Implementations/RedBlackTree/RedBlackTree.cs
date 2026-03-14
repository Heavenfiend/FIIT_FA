using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.RedBlackTree;

public class RedBlackTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, RbNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override RbNode<TKey, TValue> CreateNode(TKey key, TValue value)
    {
        return new RbNode<TKey, TValue>(key, value);
    }
    
    protected override void OnNodeAdded(RbNode<TKey, TValue> newNode)
    {
        newNode.Color = RbColor.Red;
        InsertFixup(newNode);
    }

    protected override void OnNodeRemoved(RbNode<TKey, TValue>? parent, RbNode<TKey, TValue>? child)
    {
        
    }

    protected override void RemoveNode(RbNode<TKey, TValue> z)
    {
        RbNode<TKey, TValue>? y = z;
        RbColor yOriginalColor = GetColor(y);
        RbNode<TKey, TValue>? x;
        RbNode<TKey, TValue>? xParent;

        if (z.Left == null)
        {
            x = z.Right;
            xParent = z.Parent;
            Transplant(z, z.Right);
        }
        else if (z.Right == null)
        {
            x = z.Left;
            xParent = z.Parent;
            Transplant(z, z.Left);
        }
        else
        {
            y = z.Right;
            while (y.Left != null)
            {
                y = y.Left;
            }
            yOriginalColor = GetColor(y);
            x = y.Right;

            if (y.Parent == z)
            {
                xParent = y;
            }
            else
            {
                xParent = y.Parent;
                Transplant(y, y.Right);
                y.Right = z.Right;
                if (y.Right != null)
                {
                    y.Right.Parent = y;
                }
            }
            Transplant(z, y);
            y.Left = z.Left;
            if (y.Left != null)
            {
                y.Left.Parent = y;
            }
            SetColor(y, GetColor(z));
        }

        if (yOriginalColor == RbColor.Black)
        {
            DeleteFixup(x, xParent);
        }
    }

    private RbColor GetColor(RbNode<TKey, TValue>? node) => node?.Color ?? RbColor.Black;
    private void SetColor(RbNode<TKey, TValue>? node, RbColor color)
    {
        if (node != null) node.Color = color;
    }

    private void InsertFixup(RbNode<TKey, TValue> z)
    {
        while (z.Parent != null && GetColor(z.Parent) == RbColor.Red)
        {
            if (z.Parent == z.Parent.Parent?.Left)
            {
                var y = z.Parent.Parent.Right;
                if (GetColor(y) == RbColor.Red)
                {
                    SetColor(z.Parent, RbColor.Black);
                    SetColor(y, RbColor.Black);
                    SetColor(z.Parent.Parent, RbColor.Red);
                    z = z.Parent.Parent;
                }
                else
                {
                    if (z == z.Parent.Right)
                    {
                        z = z.Parent;
                        RotateLeft(z);
                    }
                    SetColor(z.Parent, RbColor.Black);
                    SetColor(z.Parent?.Parent, RbColor.Red);
                    if (z.Parent?.Parent != null)
                    {
                        RotateRight(z.Parent.Parent);
                    }
                }
            }
            else if (z.Parent.Parent != null)
            {
                var y = z.Parent.Parent.Left;
                if (GetColor(y) == RbColor.Red)
                {
                    SetColor(z.Parent, RbColor.Black);
                    SetColor(y, RbColor.Black);
                    SetColor(z.Parent.Parent, RbColor.Red);
                    z = z.Parent.Parent;
                }
                else
                {
                    if (z == z.Parent.Left)
                    {
                        z = z.Parent;
                        RotateRight(z);
                    }
                    SetColor(z.Parent, RbColor.Black);
                    SetColor(z.Parent?.Parent, RbColor.Red);
                    if (z.Parent?.Parent != null)
                    {
                        RotateLeft(z.Parent.Parent);
                    }
                }
            }
            else
            {
                break;
            }
        }
        SetColor(Root as RbNode<TKey, TValue>, RbColor.Black);
    }

    private void DeleteFixup(RbNode<TKey, TValue>? x, RbNode<TKey, TValue>? xParent)
    {
        while (x != Root && GetColor(x) == RbColor.Black)
        {
            if (x == xParent?.Left)
            {
                var w = xParent.Right;
                if (GetColor(w) == RbColor.Red) // случай 1 брат (w) красный
                {
                    SetColor(w, RbColor.Black);      // красим брата в черный
                    SetColor(xParent, RbColor.Red);   // красим родителя в красный
                    RotateLeft(xParent);             // делаем левый поворот
                    w = xParent.Right;               // обновляем брата после поворота
                }
                if (GetColor(w?.Left) == RbColor.Black && GetColor(w?.Right) == RbColor.Black) // случай 2 брат черный, и оба его ребенка тоже черные
                {
                    SetColor(w, RbColor.Red); // dелаем брата красным
                    x = xParent; // переносим двойную черность на родителя
                    xParent = x?.Parent;
                }
                else
                {
                    if (GetColor(w?.Right) == RbColor.Black) // случай 3 брат черный, его левый ребенок красный, а правый черный
                    {
                        SetColor(w?.Left, RbColor.Black); // ккрасим левого племянника в черный
                        SetColor(w, RbColor.Red); // красим брата в красный
                        if (w != null) RotateRight(w); // правый поворот вокруг брата
                        w = xParent.Right;  // обновляем брата
                    }
                    
                    // случай 4 брат черный, его правый ребенок красный
                    SetColor(w, GetColor(xParent)); // брат берет цвет родителя
                    SetColor(xParent, RbColor.Black); // родитель становится черным
                    SetColor(w?.Right, RbColor.Black); // правый племянник становится черным
                    RotateLeft(xParent); // финальный левый поворот
                    x = Root as RbNode<TKey, TValue>;
                    xParent = null; 
                }
            }
            else // тут все зеркально только для правой стороны
            {
                var w = xParent?.Left;
                if (GetColor(w) == RbColor.Red)
                {
                    SetColor(w, RbColor.Black);
                    SetColor(xParent, RbColor.Red);
                    if (xParent != null) RotateRight(xParent);
                    w = xParent?.Left;
                }
                if (GetColor(w?.Right) == RbColor.Black && GetColor(w?.Left) == RbColor.Black)
                {
                    SetColor(w, RbColor.Red);
                    x = xParent;
                    xParent = x?.Parent;
                }
                else
                {
                    if (GetColor(w?.Left) == RbColor.Black)
                    {
                        SetColor(w?.Right, RbColor.Black);
                        SetColor(w, RbColor.Red);
                        if (w != null) RotateLeft(w);
                        w = xParent?.Left;
                    }
                    SetColor(w, GetColor(xParent));
                    SetColor(xParent, RbColor.Black);
                    SetColor(w?.Left, RbColor.Black);
                    if (xParent != null) RotateRight(xParent);
                    x = Root as RbNode<TKey, TValue>;
                    xParent = null; 
                }
            }
        }
        SetColor(x, RbColor.Black);
    }
}