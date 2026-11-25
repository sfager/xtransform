# Regenerates tests/XTransform.Tests/Stylesheets/RealWorld/<name>/expected.out using the
# .NET Framework-compatible reference processor System.Xml.Xsl.XslCompiledTransform.
# Only valid for stylesheets whose behaviour is identical under XSLT 1.0 and 2.0.
#
# Usage: pwsh eng/generate-realworld-expected.ps1
$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot '..\tests\XTransform.Tests\Stylesheets\RealWorld' | Resolve-Path

foreach ($dir in Get-ChildItem $root -Directory) {
    $xslt = New-Object System.Xml.Xsl.XslCompiledTransform
    $xslt.Load((Join-Path $dir.FullName 'style.xsl'))

    $settings = $xslt.OutputSettings.Clone()
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)   # UTF-8, no BOM
    $settings.NewLineHandling = [System.Xml.NewLineHandling]::None      # keep LF as written

    $out = Join-Path $dir.FullName 'expected.out'
    $stream = [System.IO.File]::Create($out)
    try {
        $writer = [System.Xml.XmlWriter]::Create($stream, $settings)
        $xslt.Transform((Join-Path $dir.FullName 'input.xml'), $null, $writer)
        $writer.Dispose()
    }
    finally {
        $stream.Dispose()
    }

    Write-Host "$($dir.Name): $((Get-Item $out).Length) bytes"
}
