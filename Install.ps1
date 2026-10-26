param([switch]$CheckOnly)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'GamePaths.ps1')
$root=Split-Path $PSScriptRoot -Parent;$game=Get-DpsGamePaths -Root $root
function Assert-GameClosed {foreach($p in @(Get-Process -Name $game.ProcessName -ErrorAction SilentlyContinue)){if(!$p.Path -or [IO.Path]::GetFullPath($p.Path).Equals([IO.Path]::GetFullPath($game.Executable),[StringComparison]::OrdinalIgnoreCase)){throw 'Close this game installation before installing.'}}}
Assert-GameClosed
$payload=Join-Path $PSScriptRoot 'payload'
if(!(Test-Path -LiteralPath $payload)){throw 'Missing loader payload. Build with BuildLoader.ps1 or extract the complete v1.2 package.'}
$tools=Join-Path $PSScriptRoot 'build/v1.2';$managed=$game.Managed
$manifest=Join-Path $PSScriptRoot 'installed.json'
$old=if(Test-Path -LiteralPath $manifest){Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json}else{$null}
$speakerManifest=Join-Path $root 'SpeakerMod/installed.json'
$speaker=if(Test-Path -LiteralPath $speakerManifest){Get-Content -LiteralPath $speakerManifest -Raw|ConvertFrom-Json}else{$null}
$speakerDll=Join-Path $root 'BepInEx/plugins/OneWaySpeaker/OneWaySpeaker.dll'
$withSpeaker=($speaker -and $speaker.version -eq '1.3.0' -and $speaker.loadingMode -ne 'disabled') -or (Test-Path -LiteralPath $speakerDll)
if($speaker -and $speaker.version -ne '1.3.0'){throw 'Unsupported legacy speaker version. Uninstall or adapt that layer first.'}
$work=Join-Path $PSScriptRoot ('build/install-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $work 'Managed'),(Join-Path $work 'proof') -Force|Out-Null
$changes=@();$inputs=@{}
function Add-Change([string]$From,[string]$To,[bool]$Delete=$false){$script:changes+=@{From=$From;To=$To;Delete=$Delete}}
try{
    foreach($f in Get-ChildItem -LiteralPath $managed -File -Filter '*.dll'){$inputs[$f.Name]=(Get-FileHash -LiteralPath $f.FullName).Hash;Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $work 'Managed')}
    foreach($pair in @(@('World.dll','originalSha256','patchedSha256'),@('Commons.dll','originalCommonsSha256','patchedCommonsSha256'))){
        $n=$pair[0];$source=Join-Path $managed $n
        $legacy=$old -and $old.loadingMode -ne 'bepinex-memory'
        $ours=$legacy -and $inputs[$n] -eq $old.($pair[2])
        $layer=$speaker -and $speaker.loadingMode -ne 'bepinex-memory' -and $inputs[$n] -eq $speaker.files.$n.after
        if($ours -or $layer){
            if(!$legacy -or ($layer -and $speaker.files.$n.before -ne $old.($pair[2]))){throw 'Legacy layer records disagree. Verify Steam files first.'}
            $source=Join-Path $PSScriptRoot ('backup/'+$n)
            if((Get-FileHash -LiteralPath $source).Hash -ne $old.($pair[1])){throw 'Legacy original backup hash mismatch. No files changed.'}
            Add-Change $source (Join-Path $managed $n)
        }
        Copy-Item -LiteralPath $source -Destination (Join-Path $work ('Managed/'+$n)) -Force
    }
    if($speaker -and $speaker.loadingMode -ne 'bepinex-memory' -and $inputs['Camp.dll'] -eq $speaker.files.'Camp.dll'.after){
        $camp=Join-Path $root 'SpeakerMod/backup/Camp.dll'
        if((Get-FileHash -LiteralPath $camp).Hash -ne $speaker.files.'Camp.dll'.before){throw 'Legacy speaker Camp backup mismatch'}
        Copy-Item -LiteralPath $camp -Destination (Join-Path $work 'Managed/Camp.dll') -Force
        Add-Change $camp (Join-Path $managed 'Camp.dll')
    }
    $audio=$null
    if($withSpeaker){
        $audio=if(Test-Path -LiteralPath (Join-Path $tools 'OneWaySpeaker.dll')){Join-Path $tools 'OneWaySpeaker.dll'}elseif(Test-Path -LiteralPath $speakerDll){$speakerDll}else{Join-Path $root 'SpeakerMod/build/OneWaySpeaker.dll'}
        if(!(Test-Path -LiteralPath $audio)){throw 'Speaker runtime missing. Preserve the existing speaker plugin or build with -WithSpeaker.'}
    }
    $args=@((Join-Path $work 'Managed'),(Join-Path $payload 'BepInEx/plugins/OneWayDps/OneWayDps.dll'),(Join-Path $work 'proof'))
    if($audio){$args+=$audio}
    & (Join-Path $tools 'MemoryPatchTests.exe') @args
    if($LASTEXITCODE -ne 0){throw 'Compatibility check failed. Game files were not changed. Verify Steam files if old static hooks remain.'}
    foreach($f in Get-ChildItem -LiteralPath $payload -File -Recurse){
        $relative=$f.FullName.Substring($payload.Length+1);$target=Join-Path $root $relative
        # Do not replace a different loader or an existing configuration belonging to another install.
        if((Test-Path -LiteralPath $target) -and $relative -notlike 'BepInEx\plugins\OneWayDps\*' -and $relative -ne 'BepInEx\patchers\OneWayDps.Preloader.dll'){
            $hash=(Get-FileHash -LiteralPath $target).Hash
            if($hash -ne (Get-FileHash -LiteralPath $f.FullName).Hash -and (!$old -or !$old.files -or $hash -ne $old.files.$relative)){throw ('Different shared loader/config file already exists: '+$relative)}
        }
        Add-Change $f.FullName $target
    }
    if($audio){Add-Change $audio $speakerDll}
    # Remove known legacy helper assemblies from Managed after the preloader payload is ready.
    foreach($n in @('OneWayDps.dll','OneWaySpeaker.dll')){
        $target=Join-Path $managed $n
        if(Test-Path -LiteralPath $target){
            $legacyAudio=Join-Path $root 'SpeakerMod/build/OneWaySpeaker.dll'
            $hash=$inputs[$n];$known=($n -eq 'OneWayDps.dll' -and $old -and ($hash -eq $old.modSha256 -or ($speaker -and $hash -eq $speaker.files.'OneWayDps.dll'.after))) -or ($n -eq 'OneWaySpeaker.dll' -and $speaker -and (Test-Path -LiteralPath $legacyAudio) -and $hash -eq (Get-FileHash -LiteralPath $legacyAudio).Hash)
            if(!$known){throw ('Unknown legacy helper DLL; refusing to delete: '+$n)}
            Add-Change '' $target $true
        }
    }
    if($CheckOnly){Write-Output 'v1.2 migration check passed. No install files changed.';return}
    Assert-GameClosed
    foreach($n in $inputs.Keys){if((Get-FileHash -LiteralPath (Join-Path $managed $n)).Hash -ne $inputs[$n]){throw 'Game files changed during validation. Retry after Steam finishes.'}}
    $next=[ordered]@{version='1.2.0';loadingMode='bepinex-memory';loader='BepInEx 5.4.23.6 x64';gameDataDirectory=$game.DataName;gameExecutable=[IO.Path]::GetFileName($game.Executable);installedAt=[DateTimeOffset]::Now.ToString('o');speaker=$withSpeaker;files=[ordered]@{};originalGameFiles=[ordered]@{}}
    foreach($change in $changes){if(!$change.Delete){$relative=$change.To.Substring($root.Length+1);if(!$relative.StartsWith($game.DataName+'\')){$next.files[$relative]=(Get-FileHash -LiteralPath $change.From).Hash}}}
    foreach($n in @('World.dll','Commons.dll','Camp.dll')){if(Test-Path -LiteralPath (Join-Path $work ('Managed/'+$n))){$next.originalGameFiles[$n]=(Get-FileHash -LiteralPath (Join-Path $work ('Managed/'+$n))).Hash}}
    $next|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $work 'installed.json') -Encoding UTF8
    Add-Change (Join-Path $work 'installed.json') $manifest
    if($speaker){
        $speaker|Add-Member -NotePropertyName loadingMode -NotePropertyValue 'bepinex-memory' -Force
        $speaker|Add-Member -NotePropertyName pluginPath -NotePropertyValue 'BepInEx/plugins/OneWaySpeaker/OneWaySpeaker.dll' -Force
        $speaker|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $work 'speaker-installed.json') -Encoding UTF8
        Add-Change (Join-Path $work 'speaker-installed.json') $speakerManifest
    }
    $snapshot=Join-Path $PSScriptRoot ('backup/loader-install-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $snapshot|Out-Null
    $i=0;foreach($change in $changes){$change.Backup=Join-Path $snapshot ([string]$i++);$change.Existed=Test-Path -LiteralPath $change.To;if($change.Existed){Copy-Item -LiteralPath $change.To -Destination $change.Backup}}
    $changes|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $snapshot 'rollback-map.json') -Encoding UTF8
    try{
        foreach($change in $changes){
            if($change.Delete){Remove-Item -LiteralPath $change.To -Force;continue}
            New-Item -ItemType Directory -Path (Split-Path $change.To -Parent) -Force|Out-Null
            Copy-Item -LiteralPath $change.From -Destination $change.To -Force
            if((Get-FileHash -LiteralPath $change.From).Hash -ne (Get-FileHash -LiteralPath $change.To).Hash){throw 'Installed hash mismatch'}
        }
    }catch{
        foreach($change in $changes){if($change.Existed){if(!(Test-Path -LiteralPath $change.To) -or (Get-FileHash -LiteralPath $change.Backup).Hash -ne (Get-FileHash -LiteralPath $change.To).Hash){Copy-Item -LiteralPath $change.Backup -Destination $change.To -Force}}elseif(Test-Path -LiteralPath $change.To){Remove-Item -LiteralPath $change.To -Force}};throw
    }
    Write-Output ('Installed DPS v1.2.0 via BepInEx. Game DLLs restored to clean originals; future launches patch memory only. Speaker='+$withSpeaker)
    Write-Output ('Rollback snapshot: '+$snapshot)
}finally{
    $base=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'build')).TrimEnd('\')+'\';$absolute=[IO.Path]::GetFullPath($work)
    if(!$absolute.StartsWith($base,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($absolute) -notmatch '^install-[0-9a-f]{32}$'){throw 'Unsafe staging cleanup path'}
    if(Test-Path -LiteralPath $absolute){Remove-Item -LiteralPath $absolute -Recurse -Force}
}
