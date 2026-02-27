using TreeDataStructures.Implementations.BST;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace TreeDataStructures.Tests;

[TestFixture]
public class IteratorTests
{
    [Test]
    public void Test_DictionaryEnumerator_UsesTreeIterator()
    {
        var tree = new BinarySearchTree<int, string>();
        tree.Add(5, "Five");
        tree.Add(3, "Three");
        tree.Add(7, "Seven");

        // This implicitly calls GetEnumerator()
        int count = 0;
        var keys = new List<int>();

        foreach (KeyValuePair<int, string> kvp in tree)
        {
            keys.Add(kvp.Key);
            count++;
        }

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(3));
            Assert.That(keys, Is.EqualTo(new[] { 3, 5, 7 }), "Enumerator should return items in sorted order (InOrder)");
        });

        // Verify that the enumerator returned is indeed the struct (though boxed as interface)
        // and doesn't throw implementation exceptions or behave incorrectly.
        using var enumerator = tree.GetEnumerator();
        Assert.That(enumerator.MoveNext(), Is.True);
        Assert.That(enumerator.Current.Key, Is.EqualTo(3));
        Assert.That(enumerator.MoveNext(), Is.True);
        Assert.That(enumerator.Current.Key, Is.EqualTo(5));
        Assert.That(enumerator.MoveNext(), Is.True);
        Assert.That(enumerator.Current.Key, Is.EqualTo(7));
        Assert.That(enumerator.MoveNext(), Is.False);
    }
}
