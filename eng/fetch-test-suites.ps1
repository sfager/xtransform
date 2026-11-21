# Clones the pinned W3C test suites into .testsuites/ (git-ignored).
# Usage: pwsh eng/fetch-test-suites.ps1
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$pins = @{}
Get-Content (Join-Path $PSScriptRoot 'test-suites.env') |
    Where-Object { $_ -match '^\s*([A-Z0-9_]+)=(.+)$' } |
    ForEach-Object { $pins[$Matches[1]] = $Matches[2].Trim() }

$dest = Join-Path $root '.testsuites'
New-Item -ItemType Directory -Force -Path $dest | Out-Null

function Fetch-Suite([string]$Name, [string]$Repo, [string]$Commit) {
    $dir = Join-Path $dest $Name
    if (-not (Test-Path (Join-Path $dir '.git'))) {
        git init -q $dir
        git -C $dir remote add origin $Repo
    }
    $head = (git -C $dir rev-parse HEAD 2>$null)
    if ($head -ne $Commit) {
        Write-Host "Fetching $Name @ $($Commit.Substring(0, 12))"
        git -C $dir fetch -q --depth 1 origin $Commit
        git -C $dir checkout -q --detach FETCH_HEAD
    } else {
        Write-Host "$Name already at $($Commit.Substring(0, 12))"
    }
}

Fetch-Suite 'qt3tests' $pins['QT3_REPO'] $pins['QT3_COMMIT']
Fetch-Suite 'xslt30-test' $pins['XSLT_REPO'] $pins['XSLT_COMMIT']
