<?xml version="1.0" encoding="UTF-8"?>
<!-- DITA task topic to HTML: modes, priorities, named templates, parameters. -->
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="xml" omit-xml-declaration="yes"/>

  <xsl:template match="/task">
    <article class="task" id="{@id}">
      <h1><xsl:apply-templates select="title" mode="heading"/></h1>
      <xsl:apply-templates select="shortdesc"/>
      <xsl:apply-templates select="taskbody"/>
    </article>
  </xsl:template>

  <xsl:template match="title" mode="heading"><xsl:value-of select="."/></xsl:template>

  <xsl:template match="shortdesc"><p class="shortdesc"><xsl:apply-templates/></p></xsl:template>

  <xsl:template match="taskbody">
    <xsl:apply-templates select="prereq"/>
    <ol class="steps">
      <xsl:apply-templates select="steps/step"/>
    </ol>
    <xsl:apply-templates select="result"/>
  </xsl:template>

  <xsl:template match="prereq">
    <xsl:call-template name="labelled-block">
      <xsl:with-param name="label" select="'Before you begin'"/>
      <xsl:with-param name="class" select="'prereq'"/>
    </xsl:call-template>
  </xsl:template>

  <xsl:template match="result">
    <xsl:call-template name="labelled-block">
      <xsl:with-param name="label" select="'Result'"/>
      <xsl:with-param name="class" select="'result'"/>
    </xsl:call-template>
  </xsl:template>

  <xsl:template name="labelled-block">
    <xsl:param name="label"/>
    <xsl:param name="class" select="'block'"/>
    <section class="{$class}">
      <h2><xsl:value-of select="$label"/></h2>
      <xsl:apply-templates/>
    </section>
  </xsl:template>

  <xsl:template match="step">
    <li class="step">
      <xsl:if test="@importance = 'required'"><xsl:attribute name="data-required">yes</xsl:attribute></xsl:if>
      <span class="cmd"><xsl:apply-templates select="cmd"/></span>
      <xsl:apply-templates select="info"/>
    </li>
  </xsl:template>

  <xsl:template match="info"><div class="info"><xsl:apply-templates/></div></xsl:template>
  <xsl:template match="p"><p><xsl:apply-templates/></p></xsl:template>
  <xsl:template match="uicontrol"><b class="ui"><xsl:apply-templates/></b></xsl:template>
  <xsl:template match="codeph"><code><xsl:apply-templates/></code></xsl:template>
</xsl:stylesheet>
