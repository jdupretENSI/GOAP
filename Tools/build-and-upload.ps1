param(
    [switch]$SkipBuild,
    [string]$Version,
    [string]$BuildDir = "Build\Windows"
)

$ErrorActionPreference = "Stop"

# Fall back to the environment variable if the caller didn't pass -Version.
if (-not $Version) {
    $Version = $env:PROJECT_VERSION
}

# Normalize: strip a leading "v"/"V" and any "+sha" build metadata.
# itch.io accepts both, but keeping the folder/version strings identical
# to the CI artifact name avoids confusion.
if ($Version) {
    $Version = $Version.Trim()
    $Version = $Version.TrimStart('v', 'V')
    $plus = $Version.IndexOf('+')
    if ($plus -gt 0) { $Version = $Version.Substring(0, $plus) }
}

$target = "jdupretENSI/goap:windows"

if (-not (Test-Path -LiteralPath $BuildDir)) {
    throw "Build directory not found: $BuildDir"
}

if (-not $SkipBuild) {
    Write-Host "Build step would run here..."
}

if ($Version) {
    butler push "$BuildDir" $target --userversion "$Version"
} else {
    butler push "$BuildDir" $target
}