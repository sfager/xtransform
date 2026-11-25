<?xml version="1.0" encoding="UTF-8"?>
<!-- Identity transform with overrides: strips secrets, renames elements, keeps comments and PIs. -->
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="xml" omit-xml-declaration="yes"/>

  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- Drop anything that looks like a secret. -->
  <xsl:template match="@password|@token|secret"/>

  <!-- Rename legacy elements. -->
  <xsl:template match="connection">
    <endpoint>
      <xsl:apply-templates select="@*|node()"/>
    </endpoint>
  </xsl:template>

  <!-- Mark the sanitised file. -->
  <xsl:template match="/settings">
    <settings sanitised="true">
      <xsl:apply-templates select="@*|node()"/>
    </settings>
  </xsl:template>
</xsl:stylesheet>
