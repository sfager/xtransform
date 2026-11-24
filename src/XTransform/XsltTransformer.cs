// One transformation run. Not thread-safe; create one per run from XsltExecutable.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using XTransform.Xdm;
using XTransform.Xslt;

namespace XTransform;

/// <summary>
/// Runs an <see cref="XsltExecutable"/> against an input document. Cheap to create, not
/// thread-safe, and reusable for consecutive runs.
/// </summary>
public sealed class XsltTransformer
{
    private readonly CompiledStylesheet _compiled;
    private readonly IResourceResolver _resolver;
    private readonly Dictionary<QName, XdmSequence> _parameters = new Dictionary<QName, XdmSequence>();

    internal XsltTransformer(CompiledStylesheet compiled, IResourceResolver resolver)
    {
        _compiled = compiled;
        _resolver = resolver;
    }

    /// <summary>The mode to start in; default mode when <see langword="null"/>.</summary>
    public QName? InitialMode { get; set; }

    /// <summary>A named template to start from instead of applying templates to the input.</summary>
    public QName? InitialTemplate { get; set; }

    /// <summary>Base URI against which <c>xsl:result-document</c> hrefs resolve (reserved: result documents arrive in phase 4).</summary>
    public Uri? BaseOutputUri { get; set; }

    /// <summary>Receives <c>xsl:message</c> output (reserved: <c>xsl:message</c> arrives in phase 4).</summary>
    public Action<XsltMessage>? MessageListener { get; set; }

    /// <summary>Destination for secondary result documents (reserved: arrives in phase 4).</summary>
    public IResultDocumentHandler? ResultDocumentHandler { get; set; }

    /// <summary>Maximum template call depth before <c>XTDE0810</c>; default 10,000.</summary>
    public int MaxRecursionDepth { get; set; } = XsltRunState.DefaultMaxRecursionDepth;

    /// <summary>Supplies a value for a global <c>xsl:param</c>.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentException">The stylesheet declares no global parameter of that name.</exception>
    public void SetParameter(QName name, XdmSequence value)
    {
        Require(value, nameof(value));

        if (!_compiled.Declarations.GlobalVariables.TryGetValue(name, out GlobalVariableDecl? decl) || !decl.IsParam)
        {
            throw new ArgumentException(
                $"The stylesheet declares no global xsl:param named '{name.ClarkName}'",
                nameof(name));
        }

        _parameters[name] = value;
    }

    /// <summary>Supplies a single-item value for a global <c>xsl:param</c>.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The value.</param>
    public void SetParameter(QName name, XdmItem value)
    {
        Require(value, nameof(value));
        SetParameter(name, XdmSequence.Of(value));
    }

    /// <summary>Transforms the XML in <paramref name="input"/>, writing the principal result to <paramref name="output"/> per <c>xsl:output</c>.</summary>
    /// <param name="input">The source document; left open.</param>
    /// <param name="output">The destination; left open.</param>
    public void Transform(Stream input, Stream output) =>
        TransformStreams(input, output, CancellationToken.None);

    /// <summary>Transforms <paramref name="input"/>, writing the result to <paramref name="output"/> per <c>xsl:output</c> (the writer's own encoding applies).</summary>
    /// <param name="input">The source document reader.</param>
    /// <param name="output">The destination writer.</param>
    public void Transform(XmlReader input, TextWriter output)
    {
        Require(input, nameof(input));
        Require(output, nameof(output));
        Execute(input, pool => OutputEmitterFactory.Create(_compiled.PrincipalOutput, output, pool), CancellationToken.None);
    }

    /// <summary>Transforms <paramref name="input"/> into <paramref name="output"/>; the writer's settings, not <c>xsl:output</c>, govern formatting.</summary>
    /// <param name="input">The source document reader.</param>
    /// <param name="output">The destination writer.</param>
    public void Transform(XmlReader input, XmlWriter output)
    {
        Require(input, nameof(input));
        Require(output, nameof(output));
        Execute(input, pool => OutputEmitterFactory.CreateForXmlWriter(output, pool), CancellationToken.None);
    }

    /// <summary>Transforms and returns the raw result sequence instead of serialising it.</summary>
    /// <param name="input">The source document, or <see langword="null"/> to start from <see cref="InitialTemplate"/>.</param>
    /// <returns>The result sequence.</returns>
    public XdmSequence TransformToSequence(XmlReader? input)
    {
        SequenceResultReceiver? receiver = null;
        Execute(
            input,
            pool =>
            {
                receiver = new SequenceResultReceiver(pool);
                return receiver;
            },
            CancellationToken.None);
        return receiver!.ToSequence();
    }

    /// <summary>Asynchronous <see cref="Transform(Stream, Stream)"/> that stops when <paramref name="cancellationToken"/> is cancelled.</summary>
    /// <param name="input">The source document; left open.</param>
    /// <param name="output">The destination; left open.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>A task that completes when the transformation has finished.</returns>
    public Task TransformAsync(Stream input, Stream output, CancellationToken cancellationToken = default)
    {
        Require(input, nameof(input));
        Require(output, nameof(output));
        return Task.Run(() => TransformStreams(input, output, cancellationToken), cancellationToken);
    }

    // ── Core ────────────────────────────────────────────────────────────────

    private void TransformStreams(Stream input, Stream output, CancellationToken cancellationToken)
    {
        Require(input, nameof(input));
        Require(output, nameof(output));
        using XmlReader reader = XmlReader.Create(input, XsltInput.CreateReaderSettings());
        Execute(reader, pool => OutputEmitterFactory.Create(_compiled.PrincipalOutput, output, pool), cancellationToken);
    }

    private void Execute(XmlReader? input, Func<NamePool, IResultReceiver> createSink, CancellationToken cancellationToken)
    {
        if (input is null && InitialTemplate is null)
        {
            throw new InvalidOperationException("Supply an input document or set InitialTemplate before transforming");
        }

        NamePool pool = new NamePool();
        XdmNode? document = input is null ? null : XsltInput.Load(input, pool);
        IResultReceiver sink = createSink(pool);
        try
        {
            XsltRunner.Transform(
                _compiled,
                document,
                sink,
                pool,
                _parameters.Count == 0 ? null : _parameters,
                _resolver,
                cancellationToken,
                MaxRecursionDepth,
                InitialMode,
                InitialTemplate);
        }
        finally
        {
            (sink as IDisposable)?.Dispose();
        }
    }

    private static void Require(object? value, string name)
    {
        if (value is null)
        {
            throw new ArgumentNullException(name);
        }
    }
}
