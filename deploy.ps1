$ProjectDir = $PSScriptRoot
$BuildDir = Join-Path $ProjectDir "Jellyfin.Plugin.TelJel\bin\Release\net9.0"
$DllName = "Jellyfin.Plugin.TelJel.dll"
$DllPath = Join-Path $BuildDir $DllName
$DirectoryBuildProps = Join-Path $ProjectDir "Directory.Build.props"

$Version = "1.0.0.0"
if (Test-Path $DirectoryBuildProps) {
    [xml]$props = Get-Content -Path $DirectoryBuildProps
    if ($props.Project.PropertyGroup.Version) {
        $Version = $props.Project.PropertyGroup.Version
    }
}

# Common Jellyfin plugin paths on Windows
$PossiblePaths = @(
    "D:\JellyfinServer\programdata\plugins",
    "D:\JellyfinServer\plugins",
    "$env:ProgramData\Jellyfin\Server\plugins",
    "$env:LOCALAPPDATA\jellyfin\plugins"
)

Write-Host "Build path: $DllPath"
Write-Host "Version: $Version"

if (-not (Test-Path $DllPath)) {
    Write-Error "DLL not found! Please run .\build-and-package.ps1 first (or dotnet build -c Release)."
    exit 1
}

$TargetFound = $false

foreach ($Path in $PossiblePaths) {
    if (Test-Path $Path) {
        $PluginDir = Join-Path $Path "TelJel_$Version"
        if (-not (Test-Path $PluginDir)) {
            New-Item -ItemType Directory -Force -Path $PluginDir | Out-Null
        }

        Copy-Item -Path $DllPath -Destination (Join-Path $PluginDir $DllName) -Force

        $MetaSource = Join-Path $ProjectDir "manifest.json"
        # Prefer a simple local meta.json for manual installs
        $MetaDest = Join-Path $PluginDir "meta.json"
        $Meta = @{
            category    = "Notifications"
            changelog   = "Local deploy"
            description = "Rich Telegram notifications when movies and TV episodes are added."
            guid        = "f662aa5a-4148-4c41-b8ff-0e1facacb5dd"
            name        = "TelJel"
            overview    = "Telegram media-added notifications"
            owner       = "Rob"
            targetAbi   = "10.0.0.0"
            timestamp   = ([DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"))
            version     = $Version
            status      = "Active"
            autoUpdate  = $false
            imagePath   = ""
            assemblies  = @()
        } | ConvertTo-Json -Depth 5
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($MetaDest, $Meta, $utf8NoBom)

        Write-Host "Success! Plugin copied to: $PluginDir"
        $TargetFound = $true
    }
}

if (-not $TargetFound) {
    Write-Warning "Could not find Jellyfin plugins directory automatically."
    Write-Host "Please manually copy:"
    Write-Host "  Source: $DllPath"
    Write-Host "  Destination: <Your Jellyfin Install Path>\plugins\TelJel_$Version\$DllName"
    Write-Host "Then restart Jellyfin."
}
