using System;
using System.Collections.Generic;
using System.IO;

namespace XTransform.Conformance;

/// <summary>
/// Reads the known-failures file and provides lookup by test-case name.
/// </summary>
internal sealed class KnownFailures
{
    private readonly Dictionary<string, string> _entries;

    /// <summary>
    /// Initializes from a file. If <paramref name="filePath"/> is <see langword="null"/> or
    /// the file does not exist, the instance is empty.
    /// </summary>
    public KnownFailures(string? filePath)
    {
        _entries = new Dictionary<string, string>(StringComparer.Ordinal);

        if (filePath is null || !File.Exists(filePath))
        {
            return;
        }

        foreach (string rawLine in File.ReadLines(filePath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            int commentIdx = line.IndexOf('#', StringComparison.Ordinal);
            string name;
            string reason;

            if (commentIdx >= 0)
            {
                name = line.Substring(0, commentIdx).Trim();
                reason = line.Substring(commentIdx + 1).Trim();
            }
            else
            {
                name = line;
                reason = string.Empty;
            }

            if (name.Length > 0)
            {
                _entries[name] = reason;
            }
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="testCaseName"/> is in the
    /// known-failures list, and sets <paramref name="reason"/> to the associated comment.
    /// </summary>
    public bool IsKnownFailure(string testCaseName, out string? reason)
    {
        if (_entries.TryGetValue(testCaseName, out string? r))
        {
            reason = r.Length > 0 ? r : null;
            return true;
        }

        reason = null;
        return false;
    }

    /// <summary>
    /// Loads the g1.txt file from the <c>known-failures</c> subdirectory of the conformance
    /// test output directory.
    /// </summary>
    public static KnownFailures LoadForG1(string conformanceOutputDir)
    {
        // The file is copied to the output directory next to the assembly.
        string path = Path.Combine(conformanceOutputDir, "known-failures", "g1.txt");
        return new KnownFailures(path);
    }
}
