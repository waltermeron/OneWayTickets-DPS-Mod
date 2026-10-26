$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'GamePaths.ps1')
$root=Split-Path $PSScriptRoot -Parent;$game=Get-DpsGamePaths -Root $root
foreach($p in @(Get-Process -Name $game.ProcessName -ErrorAction SilentlyContinue)){if(!$p.Path -or $p.Path.Equals($game.Executable,[StringComparison]::OrdinalIgnoreCase)){throw 'Close the game before uninstalling.'}}
$state=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'installed.json') -Raw|ConvertFrom-Json
if($state.loadingMode -ne 'bepinex-memory'){throw 'This uninstall entry requires the v1.2 loader installation.'}
if(Test-Path -LiteralPath (Join-Path $root 'BepInEx/plugins/OneWaySpeaker/OneWaySpeaker.dll')){throw 'Uninstall the speaker layer first with SpeakerMod/卸载.cmd; it shares the DPS preloader.'}
$names=@('BepInEx/patchers/OneWayDps.Preloader.dll','BepInEx/plugins/OneWayDps/OneWayDps.dll','BepInEx/plugins/OneWayDps/OneWayDps.Loader.dll')
foreach($n in $names){$path=Join-Path $root $n;$key=$n.Replace('/','\');if((Test-Path -LiteralPath $path) -and (Get-FileHash -LiteralPath $path).Hash -ne $state.files.$key){throw ('Plugin changed, no files removed: '+$n)}}
$snapshot=Join-Path $PSScriptRoot ('backup/loader-uninstall-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $snapshot|Out-Null
foreach($n in $names){if(Test-Path -LiteralPath (Join-Path $root $n)){Copy-Item -LiteralPath (Join-Path $root $n) -Destination $snapshot}}
try{foreach($n in $names){$path=Join-Path $root $n;if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path -Force}}}catch{foreach($n in $names){$source=Join-Path $snapshot ([IO.Path]::GetFileName($n));if(Test-Path -LiteralPath $source){Copy-Item -LiteralPath $source -Destination (Join-Path $root $n) -Force}};throw}
$state|Add-Member -NotePropertyName enabled -NotePropertyValue $false -Force
$state|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $PSScriptRoot 'installed.json') -Encoding UTF8
Write-Output 'DPS disabled. Official game DLLs, other BepInEx plugins, history, settings and saves were not changed. Shared loader files are retained.'
