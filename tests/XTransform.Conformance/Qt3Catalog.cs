using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace XTransform.Conformance;

/// <summary>Represents the top-level QT3 catalog.</summary>
internal sealed class Qt3Catalog
{
    private static readonly XNamespace Ns = "http://www.w3.org/2010/09/qt-fots-catalog";

    /// <summary>The root directory of the QT3 test suite.</summary>
    public string Qt3Root { get; }

    /// <summary>All test-set references found in the catalog.</summary>
    public IReadOnlyList<Qt3TestSetRef> TestSetRefs { get; }

    /// <summary>Global environments declared in the catalog, keyed by name.</summary>
    public IReadOnlyDictionary<string, Qt3Environment> GlobalEnvironments { get; }

    private Qt3Catalog(string qt3Root, IReadOnlyList<Qt3TestSetRef> testSetRefs,
                       IReadOnlyDictionary<string, Qt3Environment> globalEnvironments)
    {
        Qt3Root = qt3Root;
        TestSetRefs = testSetRefs;
        GlobalEnvironments = globalEnvironments;
    }

    /// <summary>Loads the catalog from the given QT3 root directory.</summary>
    public static Qt3Catalog Load(string qt3Root)
    {
        string catalogPath = Path.Combine(qt3Root, "catalog.xml");
        XDocument doc = XDocument.Load(catalogPath);
        XElement root = doc.Root!;

        // Load global environments from the catalog root
        Dictionary<string, Qt3Environment> globalEnvs = new Dictionary<string, Qt3Environment>(System.StringComparer.Ordinal);
        foreach (XElement envEl in root.Elements(Ns + "environment"))
        {
            Qt3Environment env = Qt3Environment.FromXml(envEl, qt3Root);
            if (env.Name.Length > 0)
            {
                globalEnvs[env.Name] = env;
            }
        }

        List<Qt3TestSetRef> refs = new();
        foreach (XElement el in root.Elements(Ns + "test-set"))
        {
            string? name = (string?)el.Attribute("name");
            string? file = (string?)el.Attribute("file");
            if (name is not null && file is not null)
            {
                refs.Add(new Qt3TestSetRef(name, file));
            }
        }

        return new Qt3Catalog(qt3Root, refs, globalEnvs);
    }
}

/// <summary>A reference to a single test-set within the QT3 catalog.</summary>
internal sealed record Qt3TestSetRef(string Name, string File);
