param([string]$Archive)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$modRoot=Split-Path $PSScriptRoot -Parent;$root=Split-Path $modRoot -Parent
if(!$Archive){$Archive=Join-Path $root 'OneWayTickets_DPSMod_v1.2.0_BepInEx_x64.zip'}
. (Join-Path $modRoot 'GamePaths.ps1')
$game=Get-DpsGamePaths -Root $root
$fixture=Join-Path $modRoot ('build/verify-loader-'+[Guid]::NewGuid().ToString('N'))
try{
    [IO.Compression.ZipFile]::ExtractToDirectory($Archive,$fixture)
    $fm=Join-Path $fixture 'DPSMod';$fg=Join-Path $fixture ($game.DataName+'/Managed')
    New-Item -ItemType Directory -Path $fg,(Join-Path $fm 'history'),(Join-Path $fixture 'SaveData') -Force|Out-Null
    Get-ChildItem -LiteralPath $game.Managed -File -Filter '*.dll'|Copy-Item -Destination $fg
    $gameHashes=@{};foreach($n in @('World.dll','Commons.dll','Camp.dll')){$gameHashes[$n]=(Get-FileHash -LiteralPath (Join-Path $fg $n)).Hash}
    $private=@((Join-Path $fm 'settings.json'),(Join-Path $fm 'history/history.json'),(Join-Path $fixture 'SaveData/save_00.dat'))
    foreach($p in $private){[IO.File]::WriteAllText($p,'preserve-personal-data')}
    $privateHashes=@{};foreach($p in $private){$privateHashes[$p]=(Get-FileHash -LiteralPath $p).Hash}
    & (Join-Path $fm 'Install.ps1')
    if($LASTEXITCODE -ne 0){throw 'Fresh loader install failed'}
    & (Join-Path $fm 'Install.ps1') -CheckOnly
    if($LASTEXITCODE -ne 0){throw 'Installed loader recheck failed'}
    $other=Join-Path $fixture 'BepInEx/plugins/other-plugin.keep';[IO.File]::WriteAllText($other,'other-plugin')
    & (Join-Path $fm 'Uninstall.ps1')
    if((Test-Path -LiteralPath (Join-Path $fixture 'BepInEx/patchers/OneWayDps.Preloader.dll')) -or (Test-Path -LiteralPath (Join-Path $fixture 'BepInEx/plugins/OneWayDps/OneWayDps.dll'))){throw 'Uninstall left DPS hooks enabled'}
    if([IO.File]::ReadAllText($other) -ne 'other-plugin'){throw 'Uninstall changed another plugin'}
    # Reproduce the actual previous static DPS + speaker installation, with its own originals.
    $legacy=Join-Path $modRoot 'backup/v1.2-before-migration'
    if(Test-Path -LiteralPath $legacy){
        New-Item -ItemType Directory -Path (Join-Path $fm 'backup'),(Join-Path $fixture 'SpeakerMod/build') -Force|Out-Null
        foreach($n in @('World.dll','Commons.dll','Camp.dll','OneWayDps.dll','OneWaySpeaker.dll')){Copy-Item -LiteralPath (Join-Path $legacy ('managed/'+$n)) -Destination $fg -Force}
        foreach($n in @('World.dll','Commons.dll')){Copy-Item -LiteralPath (Join-Path $legacy ('originals/'+$n)) -Destination (Join-Path $fm 'backup')}
        Copy-Item -LiteralPath (Join-Path $legacy 'installed.json') -Destination (Join-Path $fm 'installed.json') -Force
        Copy-Item -LiteralPath (Join-Path $legacy 'speaker/installed.json') -Destination (Join-Path $fixture 'SpeakerMod/installed.json')
        New-Item -ItemType Directory -Path (Join-Path $fixture 'SpeakerMod/backup')|Out-Null
        Copy-Item -LiteralPath (Join-Path $legacy 'originals/Camp.dll') -Destination (Join-Path $fixture 'SpeakerMod/backup/Camp.dll')
        Copy-Item -LiteralPath (Join-Path $legacy 'managed/OneWaySpeaker.dll') -Destination (Join-Path $fixture 'SpeakerMod/build/OneWaySpeaker.dll')
        & (Join-Path $fm 'Install.ps1')
        if($LASTEXITCODE -ne 0){throw 'Legacy combined migration failed'}
        foreach($n in @('OneWayDps.dll','OneWaySpeaker.dll')){if(Test-Path -LiteralPath (Join-Path $fg $n)){throw 'Legacy helper DLL remains in Managed'}}
        if(!(Test-Path -LiteralPath (Join-Path $fixture 'BepInEx/plugins/OneWaySpeaker/OneWaySpeaker.dll'))){throw 'Speaker migration missing'}
        & (Join-Path $fm 'DisableSpeaker.ps1')
        & (Join-Path $fm 'Uninstall.ps1')
    }
    $native=Join-Path $fixture 'winhttp.dll';$bytes=[IO.File]::ReadAllBytes($native);[IO.File]::WriteAllText($native,'unknown-shared-loader')
    $rejected=$false;try{& (Join-Path $fm 'Install.ps1') -CheckOnly}catch{if($_.Exception.Message -notmatch 'Different shared loader'){throw};$rejected=$true};if(!$rejected){throw 'Unknown shared loader was accepted'}
    [IO.File]::WriteAllBytes($native,$bytes)
    foreach($n in $gameHashes.Keys){if((Get-FileHash -LiteralPath (Join-Path $fg $n)).Hash -ne $gameHashes[$n]){throw ('Official DLL changed: '+$n)}}
    foreach($p in $private){if((Get-FileHash -LiteralPath $p).Hash -ne $privateHashes[$p]){throw 'Private data changed'}}
    Write-Output 'PASS v1.2 release: clean install, recheck, combined legacy migration, speaker disable, DPS uninstall, shared-loader rejection, personal data and official DLLs preserved.'
}finally{
    $base=[IO.Path]::GetFullPath((Join-Path $modRoot 'build')).TrimEnd('\')+'\';$absolute=[IO.Path]::GetFullPath($fixture)
    if(!$absolute.StartsWith($base,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($absolute) -notmatch '^verify-loader-[0-9a-f]{32}$'){throw 'Unsafe fixture cleanup path'}
    if(Test-Path -LiteralPath $absolute){Remove-Item -LiteralPath $absolute -Recurse -Force}
}
