$ErrorActionPreference = 'Stop'
$testRoot = $PSScriptRoot
$stagingRoot = Split-Path -Parent $testRoot
$runtimeRoot = Join-Path $stagingRoot 'Assets\Scripts'
if (!(Test-Path -LiteralPath $runtimeRoot)) { $runtimeRoot = Join-Path $stagingRoot 'Runtime' }
$env:DOTNET_CLI_HOME = Join-Path $testRoot '.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$referencePack = Get-ChildItem -LiteralPath 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref' -Directory | Sort-Object Name -Descending | Select-Object -First 1
$referenceArgs = Get-ChildItem -LiteralPath (Join-Path $referencePack.FullName 'ref\net9.0') -Filter '*.dll' | ForEach-Object { '/reference:' + $_.FullName }
$coreModule = 'C:\Program Files\Unity\Hub\Editor\6000.6.10f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll'
$outputDll = Join-Path $testRoot 'simulation-tests.dll'
& dotnet 'C:\Program Files\dotnet\sdk\9.0.304\Roslyn\bincore\csc.dll' /nologo /noconfig /nostdlib /target:exe ('/out:' + $outputDll) @referenceArgs ('/reference:' + $coreModule) (Join-Path $runtimeRoot 'BattleSimulation.cs') (Join-Path $runtimeRoot 'TablefrontBattlefields.cs') (Join-Path $runtimeRoot 'TablefrontBridge.cs') (Join-Path $runtimeRoot 'TablefrontTactics.cs') (Join-Path $testRoot 'NeuralCommanderStub.cs') (Join-Path $testRoot 'VerifySimulation.cs')
if ($LASTEXITCODE -ne 0) { throw 'Mechanics compile failed' }
Copy-Item -LiteralPath $coreModule -Destination (Join-Path $testRoot 'UnityEngine.CoreModule.dll') -Force
'{"runtimeOptions":{"tfm":"net9.0","framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"}}}' | Set-Content -LiteralPath (Join-Path $testRoot 'simulation-tests.runtimeconfig.json')
& dotnet $outputDll | Tee-Object -FilePath (Join-Path $testRoot 'mechanics-results.txt')
if ($LASTEXITCODE -ne 0) { throw 'Mechanics verification failed' }
