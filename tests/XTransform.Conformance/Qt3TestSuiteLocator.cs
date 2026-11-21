using System.IO;
using NUnit.Framework;

namespace XTransform.Conformance;

/// <summary>Locates the QT3 test suite directory by walking up from the test output directory.</summary>
internal static class Qt3TestSuiteLocator
{
    public const string MissingSuiteMessage =
        "QT3 test suite is not present. Run eng/fetch-test-suites.sh (or pwsh eng/fetch-test-suites.ps1) to clone the pinned W3C suites into .testsuites/.";

    /// <summary>
    /// Walks up from the NUnit test directory to find a folder containing
    /// <c>.testsuites/qt3tests/catalog.xml</c>. Returns the <c>qt3tests</c> directory path,
    /// or <see langword="null"/> if the suite is not present.
    /// </summary>
    public static string? FindQt3Root()
    {
        string? dir = TestContext.CurrentContext.TestDirectory;
        for (int i = 0; i < 10 && dir is not null; i++)
        {
            string candidate = Path.Combine(dir, ".testsuites", "qt3tests", "catalog.xml");
            if (File.Exists(candidate))
            {
                return Path.GetDirectoryName(candidate);
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    /// <summary>
    /// Walks up from the NUnit test directory to find the repository root (the first ancestor
    /// that contains a <c>.git</c> directory or the solution file <c>XTransform.sln</c>).
    /// Returns <see langword="null"/> if not found.
    /// </summary>
    public static string? FindRepoRoot()
    {
        string? dir = TestContext.CurrentContext.TestDirectory;
        for (int i = 0; i < 10 && dir is not null; i++)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")) ||
                File.Exists(Path.Combine(dir, "XTransform.sln")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }
}
