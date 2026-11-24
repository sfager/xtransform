<?xml version="1.0" encoding="UTF-8"?>
<!-- Plain-text report from a catalog: text output, string functions, recursion for padding. -->
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="text"/>
  <xsl:param name="width" select="12"/>

  <xsl:template match="/catalog">
    <xsl:text>CATALOG: </xsl:text>
    <xsl:value-of select="translate(@name, 'abcdefghijklmnopqrstuvwxyz', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ')"/>
    <xsl:text>&#10;</xsl:text>
    <xsl:apply-templates select="item"/>
    <xsl:text>TOTAL ITEMS: </xsl:text>
    <xsl:value-of select="count(item)"/>
    <xsl:text>&#10;</xsl:text>
  </xsl:template>

  <xsl:template match="item">
    <xsl:call-template name="pad">
      <xsl:with-param name="text" select="@code"/>
    </xsl:call-template>
    <xsl:value-of select="concat(substring-before(title, ' - '), '|', substring-after(title, ' - '))"/>
    <xsl:if test="contains(@tags, 'new')"><xsl:text> [NEW]</xsl:text></xsl:if>
    <xsl:text>&#10;</xsl:text>
  </xsl:template>

  <xsl:template name="pad">
    <xsl:param name="text"/>
    <xsl:choose>
      <xsl:when test="string-length($text) &lt; $width">
        <xsl:call-template name="pad">
          <xsl:with-param name="text" select="concat($text, '.')"/>
        </xsl:call-template>
      </xsl:when>
      <xsl:otherwise><xsl:value-of select="$text"/></xsl:otherwise>
    </xsl:choose>
  </xsl:template>
</xsl:stylesheet>
