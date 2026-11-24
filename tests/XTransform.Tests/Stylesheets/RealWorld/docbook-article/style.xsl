<?xml version="1.0" encoding="UTF-8"?>
<!-- DocBook-style article subset to XHTML-ish markup: sections, lists, inline markup, xrefs. -->
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="xml" omit-xml-declaration="yes" indent="no"/>
  <xsl:param name="base-level" select="1"/>

  <xsl:template match="/article">
    <html>
      <head><title><xsl:value-of select="info/title"/></title></head>
      <body>
        <h1><xsl:value-of select="info/title"/></h1>
        <p class="author"><xsl:value-of select="info/author/personname/firstname"/><xsl:text> </xsl:text><xsl:value-of select="info/author/personname/surname"/></p>
        <xsl:apply-templates select="section"/>
      </body>
    </html>
  </xsl:template>

  <xsl:template match="section">
    <xsl:variable name="depth" select="count(ancestor::section) + $base-level"/>
    <div class="section" id="{@xml:id}">
      <xsl:element name="h{$depth + 1}"><xsl:value-of select="title"/></xsl:element>
      <xsl:apply-templates select="*[not(self::title)]"/>
    </div>
  </xsl:template>

  <xsl:template match="para"><p><xsl:apply-templates/></p></xsl:template>

  <xsl:template match="itemizedlist"><ul><xsl:apply-templates/></ul></xsl:template>
  <xsl:template match="orderedlist"><ol><xsl:apply-templates/></ol></xsl:template>
  <xsl:template match="listitem"><li><xsl:apply-templates/></li></xsl:template>
  <xsl:template match="listitem/para"><xsl:apply-templates/></xsl:template>

  <xsl:template match="emphasis">
    <xsl:choose>
      <xsl:when test="@role = 'bold'"><strong><xsl:apply-templates/></strong></xsl:when>
      <xsl:otherwise><em><xsl:apply-templates/></em></xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <xsl:template match="code"><code><xsl:apply-templates/></code></xsl:template>

  <xsl:template match="xref">
    <xsl:variable name="target" select="//section[@xml:id = current()/@linkend]"/>
    <a href="#{@linkend}"><xsl:value-of select="$target/title"/></a>
  </xsl:template>

  <xsl:template match="programlisting"><pre><xsl:value-of select="."/></pre></xsl:template>
</xsl:stylesheet>
