$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'GamePaths.ps1')
$root=Split-Path $PSScriptRoot -Parent;$game=Get-DpsGamePaths -Root $root
foreach($p in @(Get-Process -Name $game.ProcessName -ErrorAction SilentlyContinue)){if(!$p.Path -or $p.Path.Equals($game.Executable,[StringComparison]::OrdinalIgnoreCase)){throw 'Close the game first.'}}
$statePath=Join-Path $PSScriptRoot 'installed.json';$state=Get-Content -LiteralPath $statePath -Raw|ConvertFrom-Json
$key='BepInEx\plugins\OneWaySpeaker\OneWaySpeaker.dll';$path=Join-Path $root $key
if(Test-Path -LiteralPath $path){
    if((Get-FileHash -LiteralPath $path).Hash -ne $state.files.$key){throw 'Speaker plugin changed; no files removed.'}
    $snapshot=Join-Path $PSScriptRoot ('backup/disabled-speaker-'+[Guid]::NewGuid().ToString('N')+'.dll')
    Copy-Item -LiteralPath $path -Destination $snapshot;Remove-Item -LiteralPath $path -Force
}
$state.speaker=$false;$state|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $statePath -Encoding UTF8
$manifest=Join-Path $root 'SpeakerMod/installed.json'
if(Test-Path -LiteralPath $manifest){$s=Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json;$s.loadingMode='disabled';$s|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $manifest -Encoding UTF8}
Write-Output 'Speaker layer disabled. DPS remains enabled; no game DLLs or personal data changed.'
