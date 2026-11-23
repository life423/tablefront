$ErrorActionPreference='Stop'
$testRoot=$PSScriptRoot
$runtimeRoot=Join-Path (Split-Path -Parent $testRoot) 'Assets\Scripts'
$referencePack=Get-ChildItem -LiteralPath 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref' -Directory | Where-Object {$_.Name -like '9.*'} | Sort-Object Name -Descending | Select-Object -First 1
$referenceArgs=Get-ChildItem -LiteralPath (Join-Path $referencePack.FullName 'ref\net9.0') -Filter '*.dll' | ForEach-Object {'/reference:'+ $_.FullName}
$coreModule='C:\Program Files\Unity\Hub\Editor\6000.6.10f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll'
$outputDll=Join-Path $testRoot 'tactical-tests.dll'
& dotnet 'C:\Program Files\dotnet\sdk\9.0.304\Roslyn\bincore\csc.dll' /nologo /noconfig /nostdlib /target:exe ('/out:'+$outputDll) @referenceArgs ('/reference:'+$coreModule) (Join-Path $runtimeRoot 'BattleSimulation.cs') (Join-Path $runtimeRoot 'TablefrontBattlefields.cs') (Join-Path $testRoot 'NeuralCommanderStub.cs') (Join-Path $testRoot 'VerifyTactics.cs')
if($LASTEXITCODE -ne 0){throw 'Tactical verification compile failed'}
Copy-Item -LiteralPath $coreModule -Destination (Join-Path $testRoot 'UnityEngine.CoreModule.dll') -Force
'{"runtimeOptions":{"tfm":"net9.0","framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"}}}' | Set-Content -LiteralPath (Join-Path $testRoot 'tactical-tests.runtimeconfig.json')
$result=& dotnet $outputDll
$exitCode=$LASTEXITCODE
$result | ForEach-Object { Write-Output $_ }
[IO.File]::WriteAllText((Join-Path $testRoot 'tactical-results.txt'),(($result -join "`n")+"`n"),[Text.UTF8Encoding]::new($false))
if($exitCode -ne 0){throw 'Tactical verification failed'}
