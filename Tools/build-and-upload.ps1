param(
    [switch]$SkipBuild,
    [string]$Version,
    [string]$BuildDir = "Build\Windows"
)

$ErrorActionPreference = "Stop"

if (-not $Version) {
    $Version = $env:PROJECT_VERSION
    if ($Version) { $Version = $Version.TrimStart('v', 'V') }
}

$target = "your-user/goap:windows"

if (-not $SkipBuild) {
    Write-Host "Build step would run here..."
}

if ($Version) {
    butler push $BuildDir $target --userversion $Version
} else {
    butler push $BuildDir $target
}