using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
public sealed class NamePoolTests
{
    // --- Allocate ---

    [Test]
    public void Allocate_SameName_ReturnsSameCode()
    {
        // Arrange
        var pool = new NamePool();

        // Act
        int a = pool.Allocate("http://example.com", "foo");
        int b = pool.Allocate("http://example.com", "foo");

        // Assert
        Assert.That(b, Is.EqualTo(a));
    }

    [Test]
    public void Allocate_DifferentNames_ReturnsDifferentCodes()
    {
        // Arrange
        var pool = new NamePool();

        // Act
        int a = pool.Allocate("http://example.com", "foo");
        int b = pool.Allocate("http://example.com", "bar");
        int c = pool.Allocate("http://other.com", "foo");

        // Assert
        Assert.That(new[] { a, b, c }, Is.Unique);
    }

    [Test]
    public void Allocate_CodesDenseFromZero()
    {
        // Arrange
        var pool = new NamePool();

        // Act
        int first = pool.Allocate("", "a");
        int second = pool.Allocate("", "b");
        int third = pool.Allocate("", "c");

        // Assert
        Assert.That(first, Is.EqualTo(0));
        Assert.That(second, Is.EqualTo(1));
        Assert.That(third, Is.EqualTo(2));
    }

    [Test]
    public void Allocate_QNameOverload_ReturnsSameCodeAsUriAndLocal()
    {
        // Arrange
        var pool = new NamePool();
        var qname = new QName("px", "http://example.com", "foo");

        // Act
        int byParts = pool.Allocate("http://example.com", "foo");
        int byQName = pool.Allocate(qname);

        // Assert
        Assert.That(byQName, Is.EqualTo(byParts));
    }

    // --- TryGetCode ---

    [Test]
    public void TryGetCode_AllocatedName_ReturnsTrue()
    {
        // Arrange
        var pool = new NamePool();
        int allocated = pool.Allocate("http://example.com", "local");

        // Act
        bool found = pool.TryGetCode("http://example.com", "local", out int code);

        // Assert
        Assert.That(found, Is.True);
        Assert.That(code, Is.EqualTo(allocated));
    }

    [Test]
    public void TryGetCode_UnallocatedName_ReturnsFalse()
    {
        // Arrange
        var pool = new NamePool();

        // Act
        bool found = pool.TryGetCode("http://example.com", "missing", out _);

        // Assert
        Assert.That(found, Is.False);
    }

    // --- GetNamespaceUri / GetLocalName / GetQName ---

    [Test]
    public void GetNamespaceUri_ReturnsAllocatedUri()
    {
        // Arrange
        var pool = new NamePool();
        int code = pool.Allocate("http://example.com", "local");

        // Act / Assert
        Assert.That(pool.GetNamespaceUri(code), Is.EqualTo("http://example.com"));
    }

    [Test]
    public void GetLocalName_ReturnsAllocatedLocal()
    {
        // Arrange
        var pool = new NamePool();
        int code = pool.Allocate("http://example.com", "local");

        // Act / Assert
        Assert.That(pool.GetLocalName(code), Is.EqualTo("local"));
    }

    [Test]
    public void GetQName_WithPrefix_ReturnsQNameWithCorrectFields()
    {
        // Arrange
        var pool = new NamePool();
        int code = pool.Allocate("http://example.com", "local");

        // Act
        var q = pool.GetQName(code, "px");

        // Assert
        Assert.That(q.Prefix, Is.EqualTo("px"));
        Assert.That(q.NamespaceUri, Is.EqualTo("http://example.com"));
        Assert.That(q.LocalName, Is.EqualTo("local"));
    }

    // --- URI codes ---

    [Test]
    public void AllocateUri_SameUri_ReturnsSameCode()
    {
        // Arrange
        var pool = new NamePool();

        // Act
        int a = pool.AllocateUri("http://example.com");
        int b = pool.AllocateUri("http://example.com");

        // Assert
        Assert.That(b, Is.EqualTo(a));
    }

    [Test]
    public void GetUriCode_MatchesAllocateUri()
    {
        // Arrange
        var pool = new NamePool();
        int uriCode = pool.AllocateUri("http://example.com");
        int nameCode = pool.Allocate("http://example.com", "local");

        // Act / Assert
        Assert.That(pool.GetUriCode(nameCode), Is.EqualTo(uriCode));
    }

    // --- Thread safety ---

    [Test]
    [CancelAfter(10_000)]
    public void Allocate_ConcurrentThreads_AllReturnSameCodeForSameName()
    {
        // Arrange
        const int threads = 8;
        const int namesPerThread = 10_000;
        var pool = new NamePool();
        var codes = new int[threads * namesPerThread];

        // Act
        Parallel.For(0, threads, t =>
        {
            for (int i = 0; i < namesPerThread; i++)
            {
                // Each thread allocates the same set of names.
                codes[t * namesPerThread + i] = pool.Allocate("http://example.com", "name" + i);
            }
        });

        // Assert: every thread must have gotten the same code for each name.
        for (int i = 0; i < namesPerThread; i++)
        {
            int expected = codes[i]; // thread 0's result
            for (int t = 1; t < threads; t++)
            {
                Assert.That(codes[t * namesPerThread + i], Is.EqualTo(expected),
                    $"Thread {t} got a different code for 'name{i}'");
            }
        }

        // Codes must be dense: exactly namesPerThread distinct names were allocated.
        var distinct = new HashSet<int>();
        for (int i = 0; i < namesPerThread; i++)
        {
            distinct.Add(codes[i]);
        }
        Assert.That(distinct.Count, Is.EqualTo(namesPerThread));
    }
}
