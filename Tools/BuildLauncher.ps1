param([Parameter(Mandatory=$true)][string]$BuildDirectory)
$ErrorActionPreference='Stop'
$buildRoot=(Resolve-Path -LiteralPath $BuildDirectory).Path
if(!(Test-Path -LiteralPath (Join-Path $buildRoot 'Tablefront.exe'))){throw 'Select the folder containing Tablefront.exe'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$icon=Join-Path $PSScriptRoot '..\Assets\Branding\Tablefront.ico'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll ('/win32icon:'+$icon) ('/out:'+(Join-Path $buildRoot 'StartTablefront.exe')) (Join-Path $PSScriptRoot 'TablefrontLauncher.cs')
if($LASTEXITCODE -ne 0){throw 'Tablefront launcher build failed'}
Get-Item -LiteralPath (Join-Path $buildRoot 'StartTablefront.exe')
