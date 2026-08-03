param(
    [string]$Version,
    [string]$Changelog = "Automated package build",
    [string]$TargetAbi = "10.0.0.0",
    [switch]$SkipManifestUpdate
)

# Build and Package Script for Jellyfin.Plugin.TelJel
$ErrorActionPreference = "Stop"

$ProjectDir = $PSScriptRoot
$ProjectFile = Join-Path $ProjectDir "Jellyfin.Plugin.TelJel\Jellyfin.Plugin.TelJel.csproj"
$DirectoryBuildProps = Join-Path $ProjectDir "Directory.Build.props"
$ManifestFile = Join-Path $ProjectDir "manifest.json"
$BuildDir = Join-Path $ProjectDir "Jellyfin.Plugin.TelJel\bin\Release\net9.0"
$DllName = "Jellyfin.Plugin.TelJel.dll"
$ZipName = "Jellyfin.Plugin.TelJel.zip"
$ZipPath = Join-Path $ProjectDir $ZipName
$RepoName = "Jellyfin.Plugin.TelJel"
$PluginGuid = "f662aa5a-4148-4c41-b8ff-0e1facacb5dd"

if (-not (Test-Path $ProjectFile)) {
    throw "Project file not found at: $ProjectFile"
}

if (-not $Version) {
    if (Test-Path $DirectoryBuildProps) {
        [xml]$props = Get-Content -Path $DirectoryBuildProps
        $Version = $props.Project.PropertyGroup.Version
    }
}
if (-not $Version) {
    throw "No version specified and no <Version> found in Directory.Build.props."
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Building Jellyfin.Plugin.TelJel" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor White

# Stamp version fields so assembly/package metadata stays in sync.
if (Test-Path $DirectoryBuildProps) {
    [xml]$props = Get-Content -Path $DirectoryBuildProps
    $props.Project.PropertyGroup.Version = $Version
    $props.Project.PropertyGroup.AssemblyVersion = $Version
    $props.Project.PropertyGroup.FileVersion = $Version
    $props.Save($DirectoryBuildProps)
    Write-Host "Updated Directory.Build.props version fields." -ForegroundColor Green
}

# Clean previous build
if (Test-Path $BuildDir) {
    Remove-Item -Path $BuildDir -Recurse -Force
}

# Build the project in Release mode
Write-Host "`nBuilding project in Release mode..." -ForegroundColor Yellow
dotnet build $ProjectFile --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

# Check if DLL exists
$DllPath = Join-Path $BuildDir $DllName
if (-not (Test-Path $DllPath)) {
    Write-Error "DLL not found at: $DllPath"
    exit 1
}

Write-Host "`nBuild successful!" -ForegroundColor Green

# Stage only what Jellyfin needs in the release zip
Write-Host "`nCreating zip package..." -ForegroundColor Yellow
$StageDir = Join-Path $ProjectDir ".package-stage"
if (Test-Path $StageDir) {
    Remove-Item -Path $StageDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $StageDir | Out-Null

Copy-Item -Path $DllPath -Destination (Join-Path $StageDir $DllName) -Force

$MetaPath = Join-Path $StageDir "meta.json"
$MetaJson = @"
{
  "category": "Notifications",
  "changelog": $($Changelog | ConvertTo-Json),
  "description": "Rich Telegram notifications when movies and TV episodes are added. Route to Telegram groups linked to Jellyfin libraries.",
  "guid": "$PluginGuid",
  "name": "TelJel",
  "overview": "Telegram media-added notifications",
  "owner": "Rob",
  "targetAbi": "$TargetAbi",
  "timestamp": "$([DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ'))",
  "version": "$Version",
  "status": "Active",
  "autoUpdate": true,
  "imagePath": "",
  "assemblies": []
}
"@
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($MetaPath, $MetaJson.Trim() + "`n", $utf8NoBom)

# Remove old zip if exists
if (Test-Path $ZipPath) {
    Remove-Item -Path $ZipPath -Force
}

Compress-Archive -Path "$StageDir\*" -DestinationPath $ZipPath -Force
Remove-Item -Path $StageDir -Recurse -Force

if (-not (Test-Path $ZipPath)) {
    Write-Error "Failed to create zip package!"
    exit 1
}

$ZipItem = Get-Item $ZipPath
$ZipSize = $ZipItem.Length / 1KB
$MD5 = (Get-FileHash -Path $ZipPath -Algorithm MD5).Hash.ToLowerInvariant()
$SHA256 = (Get-FileHash -Path $ZipPath -Algorithm SHA256).Hash
$Timestamp = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
$SourceUrl = "https://github.com/retrorat1/$RepoName/releases/download/v$Version/$ZipName"

Write-Host "`nPackage created successfully!" -ForegroundColor Green
Write-Host "  Location: $ZipPath" -ForegroundColor White
Write-Host "  Size: $([math]::Round($ZipSize, 2)) KB" -ForegroundColor White
Write-Host "`nChecksums:" -ForegroundColor Yellow
Write-Host "  MD5:    $MD5" -ForegroundColor White
Write-Host "  SHA256: $SHA256" -ForegroundColor Gray

if (-not $SkipManifestUpdate) {
    if (-not (Test-Path $ManifestFile)) {
        throw "manifest.json not found at: $ManifestFile"
    }

    $manifestRaw = Get-Content -Path $ManifestFile -Raw
    $manifestParsed = $manifestRaw | ConvertFrom-Json
    $manifest = @($manifestParsed)
    if (-not $manifest -or $manifest.Count -lt 1) {
        throw "manifest.json format invalid: expected root array with at least one plugin object."
    }

    $plugin = $manifest[0]
    if ($null -eq $plugin.PSObject.Properties["versions"]) {
        $plugin | Add-Member -NotePropertyName versions -NotePropertyValue @()
    }

    # Remove duplicate entry for this version if present, then prepend fresh one.
    $existing = @(@($plugin.versions) | Where-Object { $_ -and $_.version -ne $Version })
    $newEntry = [PSCustomObject]@{
        version   = $Version
        changelog = $Changelog
        targetAbi = $TargetAbi
        sourceUrl = $SourceUrl
        checksum  = $MD5
        timestamp = $Timestamp
    }
    $plugin.versions = @($newEntry) + $existing
    $manifest[0] = $plugin

    $manifestJson = ConvertTo-Json -InputObject @($manifest) -Depth 100
    [System.IO.File]::WriteAllText($ManifestFile, $manifestJson, $utf8NoBom)
    Write-Host "`nUpdated manifest.json with new top version entry." -ForegroundColor Green
    Write-Host "  sourceUrl : $SourceUrl" -ForegroundColor White
    Write-Host "  checksum  : $MD5" -ForegroundColor White
    Write-Host "  timestamp : $Timestamp" -ForegroundColor White
}
else {
    Write-Host "`nSkipped manifest update as requested." -ForegroundColor Yellow
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Build and Package Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "  1. Create GitHub release tag v$Version" -ForegroundColor White
Write-Host "  2. Upload $ZipName as the release asset" -ForegroundColor White
Write-Host "  3. Commit/push updated manifest.json" -ForegroundColor White
Write-Host "  4. Add repo URL on Jellyfin:" -ForegroundColor White
Write-Host "     https://raw.githubusercontent.com/retrorat1/$RepoName/main/manifest.json" -ForegroundColor Gray
