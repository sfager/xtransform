using NUnit.Framework;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// Tests for <see cref="SlotAllocator"/>: sequential slot allocation and count tracking.
/// </summary>
[TestFixture]
internal sealed class SlotAllocatorTests
{
    [Test]
    public void Allocate_Returns0Then1Then2()
    {
        // Arrange
        SlotAllocator allocator = new SlotAllocator();

        // Act
        int s0 = allocator.Allocate();
        int s1 = allocator.Allocate();
        int s2 = allocator.Allocate();

        // Assert
        Assert.That(s0, Is.EqualTo(0));
        Assert.That(s1, Is.EqualTo(1));
        Assert.That(s2, Is.EqualTo(2));
    }

    [Test]
    public void SlotCount_ReflectsAllocations()
    {
        // Arrange
        SlotAllocator allocator = new SlotAllocator();
        Assert.That(allocator.SlotCount, Is.EqualTo(0));

        // Act
        allocator.Allocate();
        Assert.That(allocator.SlotCount, Is.EqualTo(1));

        allocator.Allocate();
        allocator.Allocate();

        // Assert
        Assert.That(allocator.SlotCount, Is.EqualTo(3));
    }
}
