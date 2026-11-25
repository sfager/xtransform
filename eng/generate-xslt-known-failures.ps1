# Regenerates tests/XTransform.Conformance/known-failures/xslt-g3.txt from one or more .trx files
# produced by an XSLT conformance run (dotnet test ... --logger "trx;LogFileName=<file>").
# Every failed test gets a line "set/test  # [stage] first line of the failure reason".
#
# Usage: pwsh eng/generate-xslt-known-failures.ps1 -Trx core.trx,unicode.trx
param(
    [Parameter(Mandatory = $true)][string[]]$Trx,
    [string]$Out = (Join-Path $PSScriptRoot '..\tests\XTransform.Conformance\known-failures\xslt-g3.txt')
)

$ErrorActionPreference = 'Stop'
$entries = @{}
foreach ($file in $Trx) {
    [xml]$doc = Get-Content -Raw -LiteralPath $file
    $ns = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
    $ns.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    foreach ($r in $doc.SelectNodes('//t:UnitTestResult[@outcome="Failed"]', $ns)) {
        $msg = $r.SelectSingleNode('t:Output/t:ErrorInfo/t:Message', $ns)
        $reason = if ($msg) { ($msg.InnerText -split "`n")[0].Trim() } else { 'failed' }
        # Keep the reason to one short line; collapse numbers' noise but keep the error codes.
        $reason = ($reason -replace '\s+', ' ')
        if ($reason.Length -gt 110) { $reason = $reason.Substring(0, 110) + '...' }
        $entries[$r.testName] = $reason
    }
}

$header = @(
    '# xslt-g3.txt - known XSLT conformance misses for gate G3 (XSLT 2.0 filter).',
    '#',
    '# Format: one test-case full name per line ("test-set/test-case"), then "# [stage] reason".',
    '# Listed tests are skipped by the harness; a failing test that is NOT listed fails the run.',
    '# Regenerate with: pwsh eng/generate-xslt-known-failures.ps1 -Trx <core.trx>,<unicode.trx>',
    ''
)
$lines = $entries.GetEnumerator() | Sort-Object Name | ForEach-Object { '{0}  # {1}' -f $_.Name, $_.Value }
Set-Content -LiteralPath $Out -Value ($header + $lines) -Encoding utf8
Write-Host "$($entries.Count) known failures written to $Out"
