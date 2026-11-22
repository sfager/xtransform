using System;
using System.Text;

namespace XTransform.Xdm;

// XPath 2.0 §2.5.4 — SequenceType and ItemType syntax and semantics.
// XPath 2.0 §2.5.5 — subtype relationships.

/// <summary>
/// An XPath 2.0 item type: <c>item()</c>, <c>node()</c>, a kind test, or an atomic type test.
/// Instances are immutable and thread-safe.
/// </summary>
public abstract class ItemType
{
    // Prevent external subclassing.
    private protected ItemType() { }

    // ── Singletons ─────────────────────────────────────────────────────────────

    /// <summary>Gets the item type <c>item()</c>, which matches any item.</summary>
    public static ItemType AnyItem { get; } = new AnyItemType();

    /// <summary>Gets the item type <c>node()</c>, which matches any node.</summary>
    public static ItemType AnyNode { get; } = new AnyNodeType();

    // ── Factories ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns an atomic item type matching values whose type is a subtype of
    /// <paramref name="type"/> in the XSD type hierarchy.
    /// </summary>
    /// <param name="type">The required atomic type code.</param>
    /// <returns>An <see cref="ItemType"/> that matches atomic values of the given type.</returns>
    public static ItemType Atomic(XdmTypeCode type)
        => new AtomicItemType(type);

    /// <summary>
    /// Returns a kind test item type matching nodes of <paramref name="kind"/>.
    /// If <paramref name="name"/> is provided, only nodes with that expanded name match.
    /// </summary>
    /// <param name="kind">The required node kind.</param>
    /// <param name="name">
    ///   An optional expanded name constraint.
    ///   For <see cref="XdmNodeKind.ProcessingInstruction"/>, matching is by local name only.
    ///   Pass <see langword="null"/> to accept any name.
    /// </param>
    /// <returns>An <see cref="ItemType"/> that matches nodes of the given kind and optional name.</returns>
    public static ItemType Kind(XdmNodeKind kind, QName? name = null)
        => new KindItemType(kind, name);

    /// <summary>
    /// Returns an item type for <c>document-node()</c> or <c>document-node(element(…))</c>.
    /// </summary>
    /// <param name="elementContent">
    ///   An optional element kind test describing the required single child element of the document.
    ///   Must be a <see cref="KindItemType"/> with <see cref="XdmNodeKind.Element"/> if provided.
    ///   Pass <see langword="null"/> to match any document node.
    /// </param>
    /// <returns>An <see cref="ItemType"/> that matches document nodes.</returns>
    /// <exception cref="ArgumentException">
    ///   Thrown when <paramref name="elementContent"/> is not <see langword="null"/> and is not an
    ///   element kind test.
    /// </exception>
    public static ItemType DocumentNode(ItemType? elementContent = null)
    {
        if (elementContent != null)
        {
            if (elementContent is not KindItemType kt || kt.NodeKind != XdmNodeKind.Element)
            {
                throw new ArgumentException(
                    "elementContent must be an element kind test (Kind(XdmNodeKind.Element, ...)).",
                    nameof(elementContent));
            }
        }

        return new DocumentElementItemType(elementContent);
    }

    // ── Abstract members ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="item"/> is an instance of this type.
    /// </summary>
    /// <param name="item">The XDM item to test.</param>
    /// <returns><see langword="true"/> if the item matches; otherwise <see langword="false"/>.</returns>
    public abstract bool Matches(XdmItem item);

    /// <summary>
    /// Returns <see langword="true"/> when every item that satisfies this type also satisfies
    /// <paramref name="other"/> (i.e., this is a subtype of <paramref name="other"/>).
    /// </summary>
    /// <param name="other">The candidate supertype.</param>
    /// <returns><see langword="true"/> if the subtype relationship holds.</returns>
    public abstract bool IsSubtypeOf(ItemType other);

    /// <summary>Returns the canonical XPath 2.0 lexical form of this item type.</summary>
    /// <returns>For example: <c>"item()"</c>, <c>"node()"</c>, <c>"element(ns:local)"</c>, <c>"xs:integer"</c>.</returns>
    // RS0016 suppressed: the PublicAPI analyzer does not correctly track 'abstract override' of inherited
    // object.ToString(); the method is public by virtue of overriding object.ToString().
#pragma warning disable RS0016
    public abstract override string ToString();
#pragma warning restore RS0016

    // =========================================================================
    // Internal concrete subtypes
    // =========================================================================

    // ── AnyItemType ────────────────────────────────────────────────────────────

    /// <summary>Matches any item. Corresponds to <c>item()</c>.</summary>
    internal sealed class AnyItemType : ItemType
    {
        internal AnyItemType() { }

        /// <inheritdoc/>
        public override bool Matches(XdmItem item) => true;

        /// <inheritdoc/>
        // XPath 2.0 §2.5.5 — item() is the common supertype of all item types.
        public override bool IsSubtypeOf(ItemType other) => other is AnyItemType;

        /// <inheritdoc/>
        public override string ToString() => "item()";
    }

    // ── AnyNodeType ────────────────────────────────────────────────────────────

    /// <summary>Matches any node. Corresponds to <c>node()</c>.</summary>
    internal sealed class AnyNodeType : ItemType
    {
        internal AnyNodeType() { }

        /// <inheritdoc/>
        public override bool Matches(XdmItem item) => item.IsNode;

        /// <inheritdoc/>
        // XPath 2.0 §2.5.5 — node() is subtype of item(); every specific node kind is subtype of node().
        public override bool IsSubtypeOf(ItemType other)
            => other is AnyNodeType || other is AnyItemType;

        /// <inheritdoc/>
        public override string ToString() => "node()";
    }

    // ── AtomicItemType ─────────────────────────────────────────────────────────

    /// <summary>Matches atomic values whose type code is a subtype of <see cref="TypeCode"/>.</summary>
    internal sealed class AtomicItemType : ItemType
    {
        internal AtomicItemType(XdmTypeCode typeCode)
        {
            TypeCode = typeCode;
        }

        /// <summary>Gets the required atomic type code.</summary>
        internal XdmTypeCode TypeCode { get; }

        /// <inheritdoc/>
        // XPath 2.0 §2.5.4 — an AtomicType matches av where av's type is derived from the target type.
        public override bool Matches(XdmItem item)
        {
            if (item is not XdmAtomicValue av)
            {
                return false;
            }

            return CastTable.IsSubtypeOf(av.TypeCode, TypeCode);
        }

        /// <inheritdoc/>
        // XPath 2.0 §2.5.5 — AtomicType(T1) ⊆ AtomicType(T2) iff T1 is derived from T2.
        public override bool IsSubtypeOf(ItemType other)
        {
            if (other is AnyItemType)
            {
                return true;
            }

            if (other is AtomicItemType otherAtomic)
            {
                return CastTable.IsSubtypeOf(TypeCode, otherAtomic.TypeCode);
            }

            return false;
        }

        /// <inheritdoc/>
        public override string ToString()
            => "xs:" + CastTable.GetXsLocalName(TypeCode);
    }

    // ── KindItemType ───────────────────────────────────────────────────────────

    /// <summary>
    /// Matches nodes of a specific kind, with an optional expanded-name constraint.
    /// Namespace wildcards (e.g. <c>element(prefix:*)</c>, <c>element(*:local)</c>) are not
    /// supported in this version; a full name or no-name constraint is required.
    /// </summary>
    internal sealed class KindItemType : ItemType
    {
        internal KindItemType(XdmNodeKind kind, QName? name)
        {
            NodeKind = kind;
            Name = name;
        }

        /// <summary>Gets the required node kind.</summary>
        internal XdmNodeKind NodeKind { get; }

        /// <summary>Gets the optional name constraint; <see langword="null"/> means any name.</summary>
        internal QName? Name { get; }

        /// <inheritdoc/>
        // XPath 2.0 §2.5.4 — KindTest matches a node whose node kind equals Kind and
        // (if Name is given) whose expanded name equals Name.
        public override bool Matches(XdmItem item)
        {
            if (item is not XdmNode node)
            {
                return false;
            }

            if (node.NodeKind != NodeKind)
            {
                return false;
            }

            if (Name == null)
            {
                return true;
            }

            QName? nodeName = node.NodeName;

            // For processing-instruction nodes the name test uses the target (local name only).
            if (NodeKind == XdmNodeKind.ProcessingInstruction)
            {
                return nodeName != null &&
                       string.Equals(nodeName.Value.LocalName, Name.Value.LocalName, StringComparison.Ordinal);
            }

            return nodeName != null && nodeName.Value.Equals(Name.Value);
        }

        /// <inheritdoc/>
        // XPath 2.0 §2.5.5 — KindTest(K1,N1) ⊆ KindTest(K2,N2) iff K1==K2 and (N2 is null or N1==N2).
        public override bool IsSubtypeOf(ItemType other)
        {
            if (other is AnyItemType || other is AnyNodeType)
            {
                return true;
            }

            if (other is KindItemType otherKind)
            {
                if (NodeKind != otherKind.NodeKind)
                {
                    return false;
                }

                // other has no name constraint → any name satisfies it
                if (otherKind.Name == null)
                {
                    return true;
                }

                // other has a name constraint → we must have the same name
                return Name != null && Name.Value.Equals(otherKind.Name.Value);
            }

            // DocumentElementItemType(null) ≡ KindItemType(Document,null) semantically,
            // but KindItemType(Document) is a supertype of DocumentElementItemType, not the other way.
            return false;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            string kindName = KindToString(NodeKind);
            if (Name == null)
            {
                return kindName + "()";
            }

            return kindName + "(" + FormatQName(Name.Value) + ")";
        }

        private static string KindToString(XdmNodeKind kind)
        {
            return kind switch
            {
                XdmNodeKind.Document => "document-node",
                XdmNodeKind.Element => "element",
                XdmNodeKind.Attribute => "attribute",
                XdmNodeKind.Text => "text",
                XdmNodeKind.Comment => "comment",
                XdmNodeKind.ProcessingInstruction => "processing-instruction",
                XdmNodeKind.Namespace => "namespace-node",
                _ => kind.ToString().ToLowerInvariant(),
            };
        }

        internal static string FormatQName(QName name)
        {
            if (name.Prefix.Length > 0)
            {
                return name.Prefix + ":" + name.LocalName;
            }

            if (name.NamespaceUri.Length > 0)
            {
                return "{" + name.NamespaceUri + "}" + name.LocalName;
            }

            return name.LocalName;
        }
    }

    // ── DocumentElementItemType ────────────────────────────────────────────────

    /// <summary>
    /// Matches document nodes that have exactly one element child satisfying an optional
    /// element content test. Corresponds to <c>document-node()</c> or
    /// <c>document-node(element(…))</c>.
    /// </summary>
    internal sealed class DocumentElementItemType : ItemType
    {
        /// <param name="elementContent">
        ///   The element item type the single child element must satisfy,
        ///   or <see langword="null"/> to accept any document node.
        /// </param>
        internal DocumentElementItemType(ItemType? elementContent)
        {
            ElementContent = elementContent;
        }

        /// <summary>
        /// Gets the required child-element item type, or <see langword="null"/> for any document node.
        /// </summary>
        internal ItemType? ElementContent { get; }

        /// <inheritdoc/>
        // XPath 2.0 §2.5.4 — document-node(element(E)) matches a document whose
        // single element child satisfies element test E.
        public override bool Matches(XdmItem item)
        {
            if (item is not XdmNode doc || doc.NodeKind != XdmNodeKind.Document)
            {
                return false;
            }

            if (ElementContent == null)
            {
                return true;
            }

            // Find the single element child of the document node.
            XdmNode? singleElement = null;
            foreach (XdmNode child in doc.Axis(Axis.Child))
            {
                if (child.NodeKind == XdmNodeKind.Element)
                {
                    if (singleElement != null)
                    {
                        // More than one element child — does not match.
                        return false;
                    }

                    singleElement = child;
                }
            }

            if (singleElement == null)
            {
                return false;
            }

            return ElementContent.Matches(singleElement);
        }

        /// <inheritdoc/>
        // XPath 2.0 §2.5.5 — document-node(E1) ⊆ document-node(E2) iff E1 ⊆ E2.
        public override bool IsSubtypeOf(ItemType other)
        {
            if (other is AnyItemType || other is AnyNodeType)
            {
                return true;
            }

            // document-node(E) ⊆ KindItemType(Document, null) always.
            if (other is KindItemType otherKind && otherKind.NodeKind == XdmNodeKind.Document && otherKind.Name == null)
            {
                return true;
            }

            if (other is DocumentElementItemType otherDoc)
            {
                if (otherDoc.ElementContent == null)
                {
                    return true; // any document is subtype of unconstrained document
                }

                if (ElementContent == null)
                {
                    return false; // unconstrained is NOT subtype of constrained
                }

                return ElementContent.IsSubtypeOf(otherDoc.ElementContent);
            }

            return false;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (ElementContent == null)
            {
                return "document-node()";
            }

            return "document-node(" + ElementContent.ToString() + ")";
        }
    }
}
