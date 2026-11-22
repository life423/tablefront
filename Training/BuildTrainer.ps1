$ErrorActionPreference = 'Stop'
$trainingRoot = $PSScriptRoot
$targetDir = Join-Path $trainingRoot 'bin'
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
$sdkRoot = 'C:\Program Files\dotnet\sdk\9.0.304'
$referenceRoot = Get-ChildItem -LiteralPath 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref' -Directory | Where-Object { $_.Name -like '9.*' } | Sort-Object Name -Descending | Select-Object -First 1
$referenceDir = Join-Path $referenceRoot.FullName 'ref\net9.0'
$coreModule = 'C:\Program Files\Unity\Hub\Editor\6000.6.10f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll'
$compilerArguments = @('/nologo', '/noconfig', '/nostdlib+', '/optimize+', '/target:exe', '/langversion:latest', "/out:$targetDir\Tablefront.Training.dll")
$compilerArguments += Get-ChildItem -LiteralPath $referenceDir -Filter '*.dll' | ForEach-Object { '/reference:' + $_.FullName }
$compilerArguments += '/reference:' + $coreModule
$runtimeSource = Join-Path $trainingRoot '..\Assets\Scripts\BattleSimulation.cs'
if (!(Test-Path -LiteralPath $runtimeSource)) { $runtimeSource = Join-Path $trainingRoot '..\Runtime\BattleSimulation.cs' }
$compilerArguments += @((Join-Path $trainingRoot 'Program.cs'), (Join-Path $trainingRoot 'ExternalPolicyStub.cs'), $runtimeSource)
& 'C:\Program Files\dotnet\dotnet.exe' (Join-Path $sdkRoot 'Roslyn\bincore\csc.dll') @compilerArguments
if ($LASTEXITCODE -ne 0) { throw 'Trainer compilation failed.' }
Copy-Item -LiteralPath $coreModule -Destination (Join-Path $targetDir 'UnityEngine.CoreModule.dll') -Force
'{"runtimeOptions":{"tfm":"net9.0","framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"}}}' | Set-Content -LiteralPath (Join-Path $targetDir 'Tablefront.Training.runtimeconfig.json')
Write-Output (Join-Path $targetDir 'Tablefront.Training.dll')
