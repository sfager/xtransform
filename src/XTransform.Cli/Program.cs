using System;

namespace XTransform.Cli;

/// <summary>
/// Entry point for the <c>xtransform</c> command-line tool.
/// Argument syntax follows the common XSLT processor convention:
/// <c>xtransform -s:input.xml -xsl:style.xsl -o:output.html [name=value ...]</c>.
/// </summary>
internal static class Program
{
    private const int ExitUsage = 2;

    private static int Main(string[] args)
    {
        if (args.Length == 0 || Array.Exists(args, a => a is "-?" or "-h" or "--help"))
        {
            PrintUsage();
            return args.Length == 0 ? ExitUsage : 0;
        }

        // The transformation pipeline is not implemented yet (see docs/phase-1-tasks.md).
        Console.Error.WriteLine("xtransform: the engine is not implemented yet.");
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: xtransform -s:<source> -xsl:<stylesheet> [-o:<output>] [-it:<template>] [-im:<mode>] [name=value ...]");
        Console.WriteLine();
        Console.WriteLine("  -s:<file>      Source document");
        Console.WriteLine("  -xsl:<file>    Stylesheet");
        Console.WriteLine("  -o:<file>      Output file (default: standard output)");
        Console.WriteLine("  -it:<name>     Initial named template");
        Console.WriteLine("  -im:<name>     Initial mode");
        Console.WriteLine("  name=value     Stylesheet parameter (string value)");
    }
}
