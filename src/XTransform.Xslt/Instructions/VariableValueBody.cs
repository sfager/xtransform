// XSLT 2.0 §9.3 — the value of an xsl:variable or xsl:param defined by a sequence constructor rather
// than a select attribute. Without an as attribute the content is wrapped in a document node (a
// temporary tree); with an as attribute the value is the sequence the constructor produces.

using XTransform.Xdm;
using XTransform.XPath;
using XTransform.Serialization;

namespace XTransform.Xslt.Instructions;

/// <summary>The compiled content of a variable-binding element, and how its value is formed.</summary>
internal sealed class VariableValueBody
{
    /// <summary>Initializes a body; <paramref name="buildsDocument"/> is true when no <c>as</c> attribute was given.</summary>
    public VariableValueBody(XsltInstruction body, bool buildsDocument, string? baseUri = null)
    {
        Body = body;
        BuildsDocument = buildsDocument;
        BaseUri = baseUri;
    }

    /// <summary>The static base URI of the instruction, which is the base URI of the document node built from its content.</summary>
    public string? BaseUri { get; }

    /// <summary>The compiled sequence constructor.</summary>
    public XsltInstruction Body { get; }

    /// <summary>True when the value is a document node wrapping the content (§9.3: no <c>as</c> attribute).</summary>
    public bool BuildsDocument { get; }

    /// <summary>Evaluates the content in <paramref name="context"/> and returns the variable's value.</summary>
    public XdmSequence Evaluate(DynamicContext context, XsltRunState run)
    {
        if (BuildsDocument)
        {
            TreeBuilder tree = new TreeBuilder(run.NamePool, BaseUri);
            SequenceNormalizer normalizer = new SequenceNormalizer(tree, run.NamePool);
            normalizer.StartDocument();
            Body.Execute(context, normalizer, run);
            normalizer.EndDocument();
            return XdmSequence.Of(tree.Build());
        }

        SequenceResultReceiver sequence = new SequenceResultReceiver(run.NamePool, baseUri: BaseUri);
        Body.Execute(context, sequence, run);
        return sequence.ToSequence();
    }
}
