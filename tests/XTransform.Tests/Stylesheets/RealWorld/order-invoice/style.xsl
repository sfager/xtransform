<?xml version="1.0" encoding="UTF-8"?>
<!-- Integration stylesheet: orders feed to invoice documents. Keys, params, recursion for totals. -->
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="xml" omit-xml-declaration="yes"/>
  <xsl:key name="customer" match="customer" use="@id"/>
  <xsl:key name="product" match="product" use="@sku"/>
  <xsl:param name="currency" select="'EUR'"/>

  <xsl:template match="/feed">
    <invoices currency="{$currency}" count="{count(orders/order)}">
      <xsl:apply-templates select="orders/order"/>
    </invoices>
  </xsl:template>

  <xsl:template match="order">
    <xsl:variable name="cust" select="key('customer', @customer)"/>
    <invoice no="INV-{@id}">
      <billTo><xsl:value-of select="$cust/name"/></billTo>
      <country><xsl:value-of select="$cust/@country"/></country>
      <xsl:for-each select="line">
        <xsl:variable name="p" select="key('product', @sku)"/>
        <item sku="{@sku}" qty="{@qty}">
          <description><xsl:value-of select="$p"/></description>
          <amount><xsl:value-of select="@qty * $p/@price"/></amount>
        </item>
      </xsl:for-each>
      <units><xsl:value-of select="sum(line/@qty)"/></units>
      <total>
        <xsl:call-template name="running-total">
          <xsl:with-param name="lines" select="line"/>
        </xsl:call-template>
      </total>
    </invoice>
  </xsl:template>

  <xsl:template name="running-total">
    <xsl:param name="lines"/>
    <xsl:param name="acc" select="0"/>
    <xsl:choose>
      <xsl:when test="not($lines)"><xsl:value-of select="$acc"/></xsl:when>
      <xsl:otherwise>
        <xsl:variable name="first" select="$lines[1]"/>
        <xsl:call-template name="running-total">
          <xsl:with-param name="lines" select="$lines[position() &gt; 1]"/>
          <xsl:with-param name="acc" select="$acc + $first/@qty * key('product', $first/@sku)/@price"/>
        </xsl:call-template>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>
</xsl:stylesheet>
