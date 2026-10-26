#requires -Version 5.1
<#
.SYNOPSIS
Builds Windows x64 release ZIPs for all four overlays.
.DESCRIPTION
Requires Windows and the .NET 10 SDK or newer. Publishes framework-dependent Release
builds, includes licenses, and writes SHA-256 checksums.
Users must have the .NET 10 Desktop Runtime (x64) installed to run the overlays.
The ZIPs are ready to upload as GitHub release assets; nothing is uploaded.
.PARAMETER Version
Optional release version, such as 1.2.3 or 1.2.3-rc.1. Sets the application
version and adds it to asset filenames. Otherwise, project versions are used.
.PARAMETER OutputDirectory
Destination for ZIPs and checksums. Relative paths resolve from the repository
root. Defaults to artifacts/releases. Existing assets with the same names are
replaced only after all four overlays have published and been packaged.
.EXAMPLE
.\build-release.ps1
.EXAMPLE
.\build-release.ps1 -Version 1.0.0
#>
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$')]
    [string] $Version,

    [ValidateNotNullOrEmpty()]
    [string] $OutputDirectory = 'artifacts/releases'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($env:OS -ne 'Windows_NT') {
    throw 'Release builds require Windows.'
}

$dotnet = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $dotnet) {
    throw 'Install the .NET 10 SDK or newer and ensure dotnet is on PATH.'
}

$sdkVersion = & $dotnet.Source --version
if ($LASTEXITCODE -ne 0 -or $sdkVersion -notmatch '^(\d+)\.' -or [int] $Matches[1] -lt 10) {
    throw 'Release builds require the .NET 10 SDK or newer.'
}

if (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $PSScriptRoot $OutputDirectory
}
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
[void] [System.IO.Directory]::CreateDirectory($outputPath)
$outputPath = (Resolve-Path -LiteralPath $outputPath).ProviderPath
$stagingDirectory = Join-Path $outputPath ('.build-' + [guid]::NewGuid().ToString('N'))
[void] [System.IO.Directory]::CreateDirectory($stagingDirectory)

$overlays = @('InputOverlayMnK', 'InputOverlayPS4', 'InputOverlayPS5', 'InputOverlayXbox')
$versionSuffix = if ($Version) { "-$Version" } else { '' }
$assetNames = @()
$checksums = @()

try {
    # Use ZIP-standard forward slashes even under Windows PowerShell 5.1.
    [System.AppContext]::SetSwitch('Switch.System.IO.Compression.ZipFile.UseBackslash', $false)
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    foreach ($overlay in $overlays) {
        Write-Host "Publishing $overlay (Release, win-x64, framework-dependent)..."
        $projectDirectory = Join-Path $PSScriptRoot "src/$overlay"
        $publishDirectory = Join-Path $stagingDirectory $overlay
        $publishArguments = @(
            'publish', (Join-Path $projectDirectory "$overlay.csproj"),
            '--configuration', 'Release',
            '--runtime', 'win-x64',
            '--self-contained', 'false',
            '--output', $publishDirectory,
            '--nologo',
            '-p:PublishSingleFile=false',
            '-p:PublishTrimmed=false',
            '-p:DebugType=None',
            '-p:DebugSymbols=false'
        )
        if ($Version) {
            $publishArguments += "-p:Version=$Version"
        }

        & $dotnet.Source @publishArguments
        if ($LASTEXITCODE -ne 0) {
            throw "Publishing $overlay failed (exit code $LASTEXITCODE)."
        }

        $requiredFiles = @("$overlay.exe", "$overlay.dll", "$overlay.deps.json", "$overlay.runtimeconfig.json")
        if ($overlay -eq 'InputOverlayXbox') {
            $requiredFiles += @('GameInputRedist.dll', 'GameInputRawInputProxy.exe', 'GameInput.LICENSE.txt', 'GameInput.NOTICE.txt')
        }
        foreach ($requiredFile in $requiredFiles) {
            if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory $requiredFile) -PathType Leaf)) {
                throw "Published $overlay is missing $requiredFile."
            }
        }

        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination $publishDirectory

        $assetName = "$overlay$versionSuffix-win-x64.zip"
        $archivePath = Join-Path $stagingDirectory $assetName
        [System.IO.Compression.ZipFile]::CreateFromDirectory(
            $publishDirectory, $archivePath, [System.IO.Compression.CompressionLevel]::Optimal, $true
        )
        $assetNames += $assetName
        $hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
        $checksums += "$hash *$assetName"
    }

    $checksumName = "SHA256SUMS$versionSuffix.txt"
    [System.IO.File]::WriteAllText(
        (Join-Path $stagingDirectory $checksumName),
        (($checksums -join "`n") + "`n"),
        [System.Text.Encoding]::ASCII
    )
    $assetNames += $checksumName
    foreach ($assetName in $assetNames) {
        Move-Item -LiteralPath (Join-Path $stagingDirectory $assetName) -Destination (Join-Path $outputPath $assetName) -Force
    }
}
finally {
    # Delete only this run's staging directory, after verifying its absolute parent.
    $resolvedStagingDirectory = (Resolve-Path -LiteralPath $stagingDirectory).ProviderPath
    if ([System.IO.Path]::GetDirectoryName($resolvedStagingDirectory) -ne $outputPath) {
        throw "Refusing to remove a staging directory outside $outputPath."
    }
    Remove-Item -LiteralPath $resolvedStagingDirectory -Recurse -Force
}

Write-Host "Release assets ready in ${outputPath}:"
foreach ($assetName in $assetNames) {
    Write-Host "  $assetName"
}
