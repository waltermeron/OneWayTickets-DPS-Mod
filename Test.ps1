$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'GamePaths.ps1')
$compatibility=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'compatibility.json') -Raw|ConvertFrom-Json
$game=Get-DpsGamePaths -Root (Split-Path $PSScriptRoot -Parent) -DataDirectory $compatibility.gameDataDirectory
& (Join-Path $PSScriptRoot 'tests/GamePathsTests.ps1')
$csc='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $csc /nologo ('/out:'+(Join-Path $PSScriptRoot 'build/Tests.exe')) (Join-Path $PSScriptRoot 'tests/Tests.cs') (Join-Path $PSScriptRoot 'src/Hooks.cs') (Join-Path $PSScriptRoot 'tests/HistoryRecorderStub.cs') (Join-Path $PSScriptRoot 'src/DamageAttribution.cs') (Join-Path $PSScriptRoot 'src/DamageMeter.cs') (Join-Path $PSScriptRoot 'src/CombatStats.cs') (Join-Path $PSScriptRoot 'src/HudPreferences.cs')
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
& (Join-Path $PSScriptRoot 'build/Tests.exe')
if($LASTEXITCODE -ne 0){throw 'Tests failed'}
& $csc /nologo /codepage:65001 ('/out:'+(Join-Path $PSScriptRoot 'build/DamageAccountingTests.exe')) (Join-Path $PSScriptRoot 'tests/DamageAccountingTests.cs') (Join-Path $PSScriptRoot 'src/Hooks.cs') (Join-Path $PSScriptRoot 'tests/HistoryRecorderStub.cs') (Join-Path $PSScriptRoot 'src/DamageAttribution.cs') (Join-Path $PSScriptRoot 'src/DamageMeter.cs') (Join-Path $PSScriptRoot 'src/CombatStats.cs')
if($LASTEXITCODE -ne 0){throw 'Damage accounting test compilation failed'}
& (Join-Path $PSScriptRoot 'build/DamageAccountingTests.exe')
if($LASTEXITCODE -ne 0){throw 'Damage accounting tests failed'}
& $csc /nologo /codepage:65001 ('/out:'+(Join-Path $PSScriptRoot 'build/RelicStatsTests.exe')) (Join-Path $PSScriptRoot 'tests/RelicStatsTests.cs') (Join-Path $PSScriptRoot 'src/RelicStats.cs')
if($LASTEXITCODE -ne 0){throw 'Relic statistics test compilation failed'}
& (Join-Path $PSScriptRoot 'build/RelicStatsTests.exe')
if($LASTEXITCODE -ne 0){throw 'Relic statistics tests failed'}
& $csc /nologo /codepage:65001 ('/out:'+(Join-Path $PSScriptRoot 'build/VehicleSpeedTests.exe')) (Join-Path $PSScriptRoot 'tests/VehicleSpeedTests.cs') (Join-Path $PSScriptRoot 'src/VehicleSpeed.cs')
if($LASTEXITCODE -ne 0){throw 'Vehicle speed test compilation failed'}
& (Join-Path $PSScriptRoot 'build/VehicleSpeedTests.exe')
if($LASTEXITCODE -ne 0){throw 'Vehicle speed tests failed'}


& $csc /nologo /codepage:65001 ('/out:'+(Join-Path $PSScriptRoot 'build/AuxiliaryDamageTests.exe')) (Join-Path $PSScriptRoot 'tests/AuxiliaryDamageTests.cs') (Join-Path $PSScriptRoot 'src/Hooks.cs') (Join-Path $PSScriptRoot 'tests/HistoryRecorderStub.cs') (Join-Path $PSScriptRoot 'src/DamageAttribution.cs') (Join-Path $PSScriptRoot 'src/DamageMeter.cs') (Join-Path $PSScriptRoot 'src/CombatStats.cs')
if($LASTEXITCODE -ne 0){throw 'Auxiliary attribution test compilation failed'}
& (Join-Path $PSScriptRoot 'build/AuxiliaryDamageTests.exe')
if($LASTEXITCODE -ne 0){throw 'Auxiliary attribution tests failed'}
$cecil=Join-Path $PSScriptRoot 'tools/cecil/Mono.Cecil.dll'
Copy-Item -LiteralPath $cecil -Destination (Join-Path $PSScriptRoot 'build/Mono.Cecil.dll') -Force
& $csc /nologo /codepage:65001 ('/reference:'+$cecil) ('/out:'+(Join-Path $PSScriptRoot 'build/PatchTests.exe')) (Join-Path $PSScriptRoot 'tests/PatchTests.cs')
if($LASTEXITCODE -ne 0){throw 'Patch verification compilation failed'}
$candidate=Join-Path $PSScriptRoot 'build/v1.2/pure-proof/World.dll'
if(!(Test-Path -LiteralPath $candidate)){$candidate=Join-Path $game.Managed 'World.dll'}
$commonCandidate=Join-Path $PSScriptRoot 'build/v1.2/pure-proof/Commons.dll'
if(!(Test-Path -LiteralPath $commonCandidate)){$commonCandidate=Join-Path $game.Managed 'Commons.dll'}
& (Join-Path $PSScriptRoot 'build/PatchTests.exe') (Join-Path $PSScriptRoot 'backup/World.dll') $candidate (Join-Path $PSScriptRoot 'backup/Commons.dll') $commonCandidate
if($LASTEXITCODE -ne 0){throw 'Game patch verification failed'}
& $csc /nologo /codepage:65001 /reference:System.Web.Extensions.dll ('/out:'+(Join-Path $PSScriptRoot 'build/RunHistoryTests.exe')) (Join-Path $PSScriptRoot 'tests/RunHistoryTests.cs') (Join-Path $PSScriptRoot 'src/RunHistory.cs') (Join-Path $PSScriptRoot 'src/CombatStats.cs') (Join-Path $PSScriptRoot 'src/DamageMeter.cs')
if($LASTEXITCODE -ne 0){throw 'Run history test compilation failed'}
& (Join-Path $PSScriptRoot 'build/RunHistoryTests.exe')
if($LASTEXITCODE -ne 0){throw 'Run history tests failed'}
$managed=$game.Managed
& $csc /nologo /codepage:65001 /nowarn:1701 ('/reference:'+(Join-Path $managed 'Newtonsoft.Json.dll')) ('/reference:'+(Join-Path $managed 'netstandard.dll')) ('/out:'+(Join-Path $PSScriptRoot 'build/HistoryJsonTests.exe')) (Join-Path $PSScriptRoot 'tests/HistoryJsonTests.cs') (Join-Path $PSScriptRoot 'src/HistoryJson.cs') (Join-Path $PSScriptRoot 'src/RunHistory.cs')
if($LASTEXITCODE -ne 0){throw 'Production history JSON test compilation failed'}
Copy-Item -LiteralPath (Join-Path $managed 'Newtonsoft.Json.dll') -Destination (Join-Path $PSScriptRoot 'build/Newtonsoft.Json.dll') -Force
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'build/HistoryJsonTests.runtimeconfig.json'),'{"runtimeOptions":{"tfm":"net8.0","rollForward":"LatestMajor","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}')
& dotnet (Join-Path $PSScriptRoot 'build/HistoryJsonTests.exe')
if($LASTEXITCODE -ne 0){throw 'Production history JSON tests failed'}
