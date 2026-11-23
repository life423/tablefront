param(
    [Parameter(Mandatory=$true)][string]$BuildDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$CompilerPath,
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='0.5.0'
)
$ErrorActionPreference='Stop'
$buildRoot=(Resolve-Path -LiteralPath $BuildDirectory).Path
$required=@('StartTablefront.exe','Tablefront.exe','UnityPlayer.dll','UnityCrashHandler64.exe',
    'dstorage.dll','dstoragecore.dll','D3D12','MonoBleedingEdge','Tablefront_Data')
foreach($relativePath in $required) {
    if(!(Test-Path -LiteralPath (Join-Path $buildRoot $relativePath))) {
        throw "Incomplete game build: missing $relativePath. Build the Unity player and Tools/BuildLauncher.ps1 first."
    }
}
if(!$CompilerPath) {
    $command=Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if($command) { $CompilerPath=$command.Source }
    else {
        $candidates=@(
            (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
        )
        $CompilerPath=$candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
}
if(!$CompilerPath -or !(Test-Path -LiteralPath $CompilerPath)) {
    throw 'Install Inno Setup 7 (or 6.7+) from https://jrsoftware.org/inno-setup/downloads, or pass -CompilerPath to ISCC.exe.'
}
$outputRoot=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
& $CompilerPath "/DBuildDirectory=$buildRoot" "/DOutputDirectory=$outputRoot" "/DAppVersion=$Version" (Join-Path $PSScriptRoot 'Tablefront.iss')
if($LASTEXITCODE -ne 0) { throw "Installer compiler exited $LASTEXITCODE" }
$installer=Join-Path $outputRoot "Tablefront-Setup-v$Version.exe"
Get-Item -LiteralPath $installer | Select-Object FullName,Length
Get-FileHash -LiteralPath $installer -Algorithm SHA256
