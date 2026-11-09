<#
.SYNOPSIS
    Builds the three StandUP installers into installer\Output.

.DESCRIPTION
    lite      Needs the .NET 10 Desktop Runtime and the Windows App SDK 2 runtime on the PC. Smallest download.
    standard  Bundles the Windows App SDK; needs only the .NET 10 Desktop Runtime.
    full      Native AOT build with the Windows App SDK bundled; runs on a clean PC with nothing else installed.
              Native AOT needs the MSVC linker (Visual Studio "Desktop development with C++"). Pass -NoAot to
              build a regular self-contained full flavor on machines without it.
#>
param(
    [string]$Dotnet = 'dotnet',
    [string]$Iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    [ValidateSet('lite', 'standard', 'full')]
    [string[]]$Flavors = @('lite', 'standard', 'full'),
    [switch]$NoAot
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Iscc)) { $Iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" }
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'StandUP.csproj'

$settings = @{
    lite     = @('-p:SelfContained=false', '-p:WindowsAppSDKSelfContained=false')
    standard = @('-p:SelfContained=false', '-p:WindowsAppSDKSelfContained=true')
    full     = @('-p:SelfContained=true', '-p:WindowsAppSDKSelfContained=true') + @(if (-not $NoAot) { '-p:PublishAot=true' })
}

foreach ($flavor in $Flavors) {
    $publish = Join-Path $root "publish\$flavor"
    if (Test-Path $publish) { [System.IO.Directory]::Delete($publish, $true) }
    Write-Host "Publishing $flavor..."
    & $Dotnet publish $project -c Release -r win-x64 -o $publish -nologo -v q @($settings[$flavor])
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $flavor" }
    & $Iscc /Q "/DFlavor=$flavor" "/DPublishDir=$publish" (Join-Path $root 'installer\StandUP.iss')
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed for $flavor" }
}

foreach ($flavor in $Flavors) {
    Get-ChildItem (Join-Path $root 'installer\Output') -Filter "StandUP-Setup-*-$flavor.exe" |
        Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
}
