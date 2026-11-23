param([string]$PolicyPath)
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
$engineRoot = 'C:\Program Files\Unity\Hub\Editor\6000.6.10f1\Editor\Data\Managed\UnityEngine'
$coreModule = Join-Path $engineRoot 'UnityEngine.CoreModule.dll'
$jsonModule = Join-Path $engineRoot 'UnityEngine.JSONSerializeModule.dll'
$outputDll = Join-Path $testRoot 'neural-tests.dll'
& dotnet 'C:\Program Files\dotnet\sdk\9.0.304\Roslyn\bincore\csc.dll' /nologo /noconfig /nostdlib /target:exe ('/out:' + $outputDll) @referenceArgs ('/reference:' + $coreModule) ('/reference:' + $jsonModule) (Join-Path $runtimeRoot 'BattleSimulation.cs') (Join-Path $runtimeRoot 'TablefrontBattlefields.cs') (Join-Path $runtimeRoot 'TablefrontBridge.cs') (Join-Path $runtimeRoot 'NeuralCommander.cs') (Join-Path $testRoot 'VerifyNeural.cs')
if ($LASTEXITCODE -ne 0) { throw 'Neural verification compile failed' }
Copy-Item -LiteralPath $coreModule -Destination (Join-Path $testRoot 'UnityEngine.CoreModule.dll') -Force
Copy-Item -LiteralPath $jsonModule -Destination (Join-Path $testRoot 'UnityEngine.JSONSerializeModule.dll') -Force
'{"runtimeOptions":{"tfm":"net9.0","framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"}}}' | Set-Content -LiteralPath (Join-Path $testRoot 'neural-tests.runtimeconfig.json')
if (!$PolicyPath) { $PolicyPath = Join-Path $stagingRoot 'Training\unity-trained\commander-policy.json' }
& dotnet $outputDll $PolicyPath | Tee-Object -FilePath (Join-Path $testRoot 'neural-runtime-results.txt')
if ($LASTEXITCODE -ne 0) { throw 'Neural runtime verification failed' }
