$ErrorActionPreference='Stop'
$csc='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $csc /nologo ('/out:'+(Join-Path $PSScriptRoot 'build/Tests.exe')) (Join-Path $PSScriptRoot 'tests/Tests.cs') (Join-Path $PSScriptRoot 'src/Hooks.cs') (Join-Path $PSScriptRoot 'src/DamageMeter.cs') (Join-Path $PSScriptRoot 'src/CombatStats.cs') (Join-Path $PSScriptRoot 'src/HudPreferences.cs')
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
& (Join-Path $PSScriptRoot 'build/Tests.exe')
if($LASTEXITCODE -ne 0){throw 'Tests failed'}
& $csc /nologo /codepage:65001 ('/out:'+(Join-Path $PSScriptRoot 'build/DamageAccountingTests.exe')) (Join-Path $PSScriptRoot 'tests/DamageAccountingTests.cs') (Join-Path $PSScriptRoot 'src/Hooks.cs') (Join-Path $PSScriptRoot 'src/DamageMeter.cs') (Join-Path $PSScriptRoot 'src/CombatStats.cs')
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

