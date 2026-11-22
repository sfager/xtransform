using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;

namespace XTransform.Tests.Xdm;

[TestFixture]
public sealed class IteratorTests
{
    // =========================================================================
    // EmptyIterator
    // =========================================================================

    [Test]
    public void EmptyIterator_Next_AlwaysReturnsNull()
    {
        // Arrange
        EmptyIterator it = EmptyIterator.Instance;

        // Act / Assert
        Assert.That(it.Next(), Is.Null, "First call must return null.");
        Assert.That(it.Next(), Is.Null, "Second call must still return null.");
    }

    [Test]
    public void EmptyIterator_Position_AlwaysZero()
    {
        // Arrange
        EmptyIterator it = EmptyIterator.Instance;

        // Act
        it.Next();

        // Assert
        Assert.That(it.Position, Is.EqualTo(0));
    }

    [Test]
    public void EmptyIterator_Clone_ReturnsSameInstance()
    {
        // Arrange
        EmptyIterator it = EmptyIterator.Instance;

        // Act
        IXdmIterator clone = it.Clone();

        // Assert
        Assert.That(ReferenceEquals(clone, EmptyIterator.Instance), Is.True, "Clone should return the singleton instance.");
    }

    // =========================================================================
    // SingleItemIterator
    // =========================================================================

    [Test]
    public void SingleItemIterator_Next_YieldsItemOnceThenNull()
    {
        // Arrange
        XdmAtomicValue item = XdmAtomicValue.Integer(42L);
        SingleItemIterator it = new SingleItemIterator(item);

        // Act
        XdmItem? first = it.Next();
        XdmItem? second = it.Next();

        // Assert
        Assert.That(first, Is.SameAs(item));
        Assert.That(second, Is.Null);
    }

    [Test]
    public void SingleItemIterator_Position_OneAfterFirstNext()
    {
        // Arrange
        SingleItemIterator it = new SingleItemIterator(XdmAtomicValue.Boolean(true));

        // Act
        it.Next();

        // Assert
        Assert.That(it.Position, Is.EqualTo(1));
    }

    [Test]
    public void SingleItemIterator_Clone_RestartsFromBeginning()
    {
        // Arrange
        XdmAtomicValue item = XdmAtomicValue.Integer(7L);
        SingleItemIterator it = new SingleItemIterator(item);
        it.Next(); // exhaust

        // Act
        IXdmIterator clone = it.Clone();

        // Assert
        Assert.That(clone.Position, Is.EqualTo(0), "Clone position must start at 0.");
        Assert.That(clone.Next(), Is.SameAs(item), "Clone must yield the item again.");
    }

    // =========================================================================
    // ArrayIterator
    // =========================================================================

    [Test]
    public void ArrayIterator_Next_YieldsAllItemsInOrder()
    {
        // Arrange
        XdmAtomicValue a = XdmAtomicValue.Integer(1L);
        XdmAtomicValue b = XdmAtomicValue.Integer(2L);
        XdmAtomicValue c = XdmAtomicValue.Integer(3L);
        ArrayIterator it = new ArrayIterator(new XdmItem[] { a, b, c });

        // Act
        List<XdmItem> results = CollectAll(it);

        // Assert
        Assert.That(results, Is.EqualTo(new[] { a, b, c }));
    }

    [Test]
    public void ArrayIterator_Position_IncrementsCumulatively()
    {
        // Arrange
        ArrayIterator it = new ArrayIterator(new XdmItem[] {
            XdmAtomicValue.Integer(1L),
            XdmAtomicValue.Integer(2L)
        });

        // Act / Assert
        Assert.That(it.Position, Is.EqualTo(0));
        it.Next();
        Assert.That(it.Position, Is.EqualTo(1));
        it.Next();
        Assert.That(it.Position, Is.EqualTo(2));
        it.Next(); // past end
        Assert.That(it.Position, Is.EqualTo(2), "Position must not change after exhaustion.");
    }

    [Test]
    public void ArrayIterator_Clone_RestartsFromBeginning()
    {
        // Arrange
        XdmAtomicValue a = XdmAtomicValue.String("x");
        XdmAtomicValue b = XdmAtomicValue.String("y");
        ArrayIterator it = new ArrayIterator(new XdmItem[] { a, b });
        it.Next(); // consume first

        // Act
        IXdmIterator clone = it.Clone();
        List<XdmItem> cloneResults = CollectAll(clone);

        // Assert
        Assert.That(cloneResults, Is.EqualTo(new[] { a, b }), "Clone must start from the first item.");
    }

    // =========================================================================
    // ConcatIterator
    // =========================================================================

    [Test]
    public void ConcatIterator_YieldsAllSourceItemsInOrder()
    {
        // Arrange
        XdmAtomicValue a = XdmAtomicValue.Integer(1L);
        XdmAtomicValue b = XdmAtomicValue.Integer(2L);
        XdmAtomicValue c = XdmAtomicValue.Integer(3L);
        XdmAtomicValue d = XdmAtomicValue.Integer(4L);

        ConcatIterator it = new ConcatIterator(new IXdmIterator[]
        {
            new SingleItemIterator(a),
            new ArrayIterator(new XdmItem[] { b, c }),
            new SingleItemIterator(d)
        });

        // Act
        List<XdmItem> results = CollectAll(it);

        // Assert
        Assert.That(results, Is.EqualTo(new[] { a, b, c, d }));
    }

    [Test]
    public void ConcatIterator_Position_IsCumulativeAcrossSources()
    {
        // Arrange
        ConcatIterator it = new ConcatIterator(new IXdmIterator[]
        {
            new ArrayIterator(new XdmItem[] { XdmAtomicValue.Integer(1L), XdmAtomicValue.Integer(2L) }),
            new SingleItemIterator(XdmAtomicValue.Integer(3L))
        });

        // Act / Assert
        it.Next(); Assert.That(it.Position, Is.EqualTo(1));
        it.Next(); Assert.That(it.Position, Is.EqualTo(2));
        it.Next(); Assert.That(it.Position, Is.EqualTo(3));
        it.Next(); // past end
        Assert.That(it.Position, Is.EqualTo(3), "Position must not advance after exhaustion.");
    }

    [Test]
    public void ConcatIterator_EmptySources_YieldsNothing()
    {
        // Arrange
        ConcatIterator it = new ConcatIterator(new IXdmIterator[]
        {
            EmptyIterator.Instance,
            EmptyIterator.Instance
        });

        // Act / Assert
        Assert.That(it.Next(), Is.Null);
        Assert.That(it.Position, Is.EqualTo(0));
    }

    [Test]
    public void ConcatIterator_Clone_RestartsFromBeginning()
    {
        // Arrange
        XdmAtomicValue a = XdmAtomicValue.Integer(10L);
        XdmAtomicValue b = XdmAtomicValue.Integer(20L);
        ConcatIterator it = new ConcatIterator(new IXdmIterator[]
        {
            new SingleItemIterator(a),
            new SingleItemIterator(b)
        });
        it.Next(); // consume first item

        // Act
        IXdmIterator clone = it.Clone();
        List<XdmItem> cloneResults = CollectAll(clone);

        // Assert
        Assert.That(cloneResults, Is.EqualTo(new[] { a, b }), "Clone must restart from the beginning.");
    }

    // =========================================================================
    // MappingIterator
    // =========================================================================

    [Test]
    public void MappingIterator_ExpandsEachSourceItemToSubIterator()
    {
        // Arrange — each integer n expands to [n, n] (two copies)
        ArrayIterator source = new ArrayIterator(new XdmItem[]
        {
            XdmAtomicValue.Integer(1L),
            XdmAtomicValue.Integer(2L)
        });
        MappingIterator it = new MappingIterator(source, item =>
            new ArrayIterator(new XdmItem[] { item, item }));

        // Act
        List<XdmItem> results = CollectAll(it);

        // Assert — [1,1,2,2]
        Assert.That(results.Count, Is.EqualTo(4));
        Assert.That(((XdmAtomicValue)results[0]).AsInt64(), Is.EqualTo(1L));
        Assert.That(((XdmAtomicValue)results[1]).AsInt64(), Is.EqualTo(1L));
        Assert.That(((XdmAtomicValue)results[2]).AsInt64(), Is.EqualTo(2L));
        Assert.That(((XdmAtomicValue)results[3]).AsInt64(), Is.EqualTo(2L));
    }

    [Test]
    public void MappingIterator_Position_IsGlobalCount()
    {
        // Arrange
        ArrayIterator source = new ArrayIterator(new XdmItem[]
        {
            XdmAtomicValue.Integer(1L),
            XdmAtomicValue.Integer(2L)
        });
        MappingIterator it = new MappingIterator(source, item => new SingleItemIterator(item));

        // Act / Assert
        it.Next(); Assert.That(it.Position, Is.EqualTo(1));
        it.Next(); Assert.That(it.Position, Is.EqualTo(2));
    }

    [Test]
    public void MappingIterator_Clone_RestartsFromBeginning()
    {
        // Arrange
        XdmAtomicValue a = XdmAtomicValue.Integer(5L);
        XdmAtomicValue b = XdmAtomicValue.Integer(6L);
        ArrayIterator source = new ArrayIterator(new XdmItem[] { a, b });
        MappingIterator it = new MappingIterator(source, item => new SingleItemIterator(item));
        it.Next(); // consume first item

        // Act
        IXdmIterator clone = it.Clone();
        List<XdmItem> cloneResults = CollectAll(clone);

        // Assert
        Assert.That(cloneResults, Is.EqualTo(new[] { a, b }), "Clone must restart from the beginning.");
    }

    [Test]
    public void MappingIterator_EmptySource_YieldsNothing()
    {
        // Arrange
        MappingIterator it = new MappingIterator(EmptyIterator.Instance,
            item => new SingleItemIterator(item));

        // Act / Assert
        Assert.That(it.Next(), Is.Null);
        Assert.That(it.Position, Is.EqualTo(0));
    }

    [Test]
    public void MappingIterator_MapperReturnsEmpty_YieldsNothing()
    {
        // Arrange
        SingleItemIterator source = new SingleItemIterator(XdmAtomicValue.Integer(99L));
        MappingIterator it = new MappingIterator(source, _ => EmptyIterator.Instance);

        // Act / Assert
        Assert.That(it.Next(), Is.Null);
    }

    // =========================================================================
    // Helper
    // =========================================================================

    private static List<XdmItem> CollectAll(IXdmIterator it)
    {
        List<XdmItem> list = new List<XdmItem>();
        XdmItem? item;
        while ((item = it.Next()) != null)
        {
            list.Add(item);
        }

        return list;
    }
}
