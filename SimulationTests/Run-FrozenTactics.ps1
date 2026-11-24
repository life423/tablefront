$ErrorActionPreference='Stop'
$testRoot=$PSScriptRoot
$repo=Split-Path -Parent $testRoot
$runtimeRoot=Join-Path $repo 'Assets\Scripts'
$referencePack=Get-ChildItem -LiteralPath 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref' -Directory | Where-Object {$_.Name -like '9.*'} | Sort-Object Name -Descending | Select-Object -First 1
$referenceArgs=Get-ChildItem -LiteralPath (Join-Path $referencePack.FullName 'ref\net9.0') -Filter '*.dll' | ForEach-Object {'/reference:'+ $_.FullName}
$engine='C:\Program Files\Unity\Hub\Editor\6000.6.10f1\Editor\Data\Managed\UnityEngine'
$core=Join-Path $engine 'UnityEngine.CoreModule.dll'
$json=Join-Path $engine 'UnityEngine.JSONSerializeModule.dll'
$outputDll=Join-Path $testRoot 'frozen-tactical-tests.dll'
& dotnet 'C:\Program Files\dotnet\sdk\9.0.304\Roslyn\bincore\csc.dll' /nologo /noconfig /nostdlib /target:exe ('/out:'+$outputDll) @referenceArgs ('/reference:'+$core) ('/reference:'+$json) (Join-Path $runtimeRoot 'BattleSimulation.cs') (Join-Path $runtimeRoot 'TablefrontBattlefields.cs') (Join-Path $runtimeRoot 'TablefrontBridge.cs') (Join-Path $runtimeRoot 'TablefrontTactics.cs') (Join-Path $runtimeRoot 'NeuralCommander.cs') (Join-Path $testRoot 'VerifyTacticalPolicy.cs')
if($LASTEXITCODE -ne 0){throw 'Frozen tactical verification compile failed'}
Copy-Item -LiteralPath $core -Destination (Join-Path $testRoot 'UnityEngine.CoreModule.dll') -Force
Copy-Item -LiteralPath $json -Destination (Join-Path $testRoot 'UnityEngine.JSONSerializeModule.dll') -Force
'{"runtimeOptions":{"tfm":"net9.0","framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"}}}' | Set-Content -LiteralPath (Join-Path $testRoot 'frozen-tactical-tests.runtimeconfig.json')
& dotnet $outputDll (Join-Path $repo 'Assets\Resources\commander-policy.json') (Join-Path $testRoot 'frozen-tactical-results.json')
if($LASTEXITCODE -ne 0){throw 'Frozen tactical verification failed'}
