<#
.SYNOPSIS
    Uploads a built player to itch.io via butler.

.DESCRIPTION
    Called by BuildRunner.BuildAndPublish() from the Unity editor menu.
    Can also be run manually:

        .\Tools\upload-itchio.ps1 -Version 0.3.1
        .\Tools\upload-itchio.ps1 -Version 0.3.1 -BuildDir "Build\Windows\0.3.1"

    Expects BUTLER_API_KEY in the environment (or a previous `butler login`).
    The script fails (non-zero exit) on any butler error, so it is safe to
    chain from CI or from the editor.

.NOTES
    Keep this filename in sync with BuildRunner.ScriptRelativePath.
#>
param(
    [Parameter(Mandatory = $false)]
    [string]$Version,

    [string]$BuildDir = "Build\Windows",

    [string]$Target = "jdupretENSI/goap:windows"
)

$ErrorActionPreference = "Stop"

# ---- Resolve version (CLI arg wins, then PROJECT_VERSION) -----------------
if (-not $Version) {
    $Version = $env:PROJECT_VERSION
}

# Normalize: strip a leading "v"/"V" and any "+sha" build metadata. The
# folder/version strings are kept identical to the CI artifact name so the
# itch.io version tag matches the tag that triggered the workflow.
if ($Version) {
    $Version = $Version.Trim()
    $Version = $Version.TrimStart('v', 'V')
    $plus = $Version.IndexOf('+')
    if ($plus -gt 0) { $Version = $Version.Substring(0, $plus) }
}

# ---- Preconditions --------------------------------------------------------
if (-not (Test-Path -LiteralPath $BuildDir -PathType Container)) {
    throw "Build directory not found or not a directory: $BuildDir"
}

if (-not (Get-Command butler -ErrorAction SilentlyContinue)) {
    throw "butler CLI not found on PATH. Install from https://itchio.itch.io/butler"
}

if (-not $env:BUTLER_API_KEY) {
    # Fall back to a local `butler login` if the user has one; otherwise bail
    # with a message rather than letting the server return a 401.
    $creds = Join-Path $env:APPDATA "itch\butler_creds"
    if (-not (Test-Path -LiteralPath $creds)) {
        throw "BUTLER_API_KEY is not set and no local butler login was found. Set the env var or run 'butler login'."
    }
}

# ---- Push -----------------------------------------------------------------
$pushArgs = @("push", $BuildDir, $Target, "--ignore", "build-info.txt")

if ($Version) {
    Write-Host "Pushing '$BuildDir' to '$Target' as version $Version..."
    $pushArgs += @("--userversion", $Version)
} else {
    Write-Host "Pushing '$BuildDir' to '$Target' (no explicit version)..."
}

& butler @pushArgs

if ($LASTEXITCODE -ne 0) {
    throw "butler push failed with exit code $LASTEXITCODE"
}

Write-Host "Upload complete: $Target @ $Version"
