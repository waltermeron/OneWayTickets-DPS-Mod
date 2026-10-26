$loaderManifest=Join-Path $PSScriptRoot 'installed.json'
if(Test-Path -LiteralPath $loaderManifest){$loaderState=Get-Content -LiteralPath $loaderManifest -Raw|ConvertFrom-Json;if($loaderState.loadingMode -eq 'bepinex-memory'){& (Join-Path $PSScriptRoot 'UninstallLoader.ps1');return}}
$ErrorActionPreference='Stop'
Import-Module Microsoft.PowerShell.Utility -ErrorAction Stop
$root=Split-Path $PSScriptRoot -Parent
$manifest=Join-Path $PSScriptRoot 'installed.json'
if(!(Test-Path -LiteralPath $manifest)){throw 'Install record not found. No files were changed.'}
$state=Get-Content -Raw $manifest | ConvertFrom-Json
. (Join-Path $PSScriptRoot 'GamePaths.ps1')
$game=Get-DpsGamePaths -Root $root -DataDirectory $state.gameDataDirectory
if(Get-Process -Name $game.ProcessName -ErrorAction SilentlyContinue){throw 'Close the game before uninstalling the mod.'}
$mod=Join-Path $game.Managed 'OneWayDps.dll'
$assemblies=@(@{Name='World.dll';Original=$state.originalSha256;Patched=$state.patchedSha256})
if($state.originalCommonsSha256){$assemblies+=@{Name='Commons.dll';Original=$state.originalCommonsSha256;Patched=$state.patchedCommonsSha256}}
$already=$true
foreach($assembly in $assemblies){
    $assembly.Path=Join-Path $game.Managed $assembly.Name
    $assembly.Backup=Join-Path $PSScriptRoot ('backup/'+$assembly.Name)
    if((Get-FileHash -LiteralPath $assembly.Backup).Hash -ne $assembly.Original){throw 'Original assembly backup mismatch. No files were changed.'}
    $current=(Get-FileHash -LiteralPath $assembly.Path).Hash
    if($current -ne $assembly.Original){$already=$false}
    if($current -ne $assembly.Original -and $current -ne $assembly.Patched){throw 'Game files changed after installation. Refusing to overwrite a game update or another mod.'}
}
if($already -and !(Test-Path -LiteralPath $mod)){Write-Host 'DPS mod is already uninstalled.';return}
if((Test-Path -LiteralPath $mod) -and (Get-FileHash -LiteralPath $mod -Algorithm SHA256).Hash -ne $state.modSha256){throw 'OneWayDps.dll has changed. No files were changed.'}
$rollback=Join-Path ([IO.Path]::GetTempPath()) ('OneWayDps-Uninstall-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $rollback | Out-Null
try {
    foreach($assembly in $assemblies){Copy-Item -LiteralPath $assembly.Path -Destination (Join-Path $rollback $assembly.Name)}
    if(Test-Path -LiteralPath $mod){Copy-Item -LiteralPath $mod -Destination (Join-Path $rollback 'OneWayDps.dll')}
    try {
        foreach($assembly in $assemblies){Copy-Item -LiteralPath $assembly.Backup -Destination $assembly.Path -Force;if((Get-FileHash -LiteralPath $assembly.Path).Hash -ne $assembly.Original){throw 'Restore verification failed'}}
        if(Test-Path -LiteralPath $mod){Remove-Item -LiteralPath $mod}
    } catch {
        foreach($assembly in $assemblies){Copy-Item -LiteralPath (Join-Path $rollback $assembly.Name) -Destination $assembly.Path -Force}
        if(Test-Path -LiteralPath (Join-Path $rollback 'OneWayDps.dll')){Copy-Item -LiteralPath (Join-Path $rollback 'OneWayDps.dll') -Destination $mod -Force}
        throw
    }
    Write-Host 'Original game assemblies restored. History, preferences and backups preserved.'
} finally {
    $prefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    $absolute=[IO.Path]::GetFullPath($rollback)
    if(!$absolute.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($absolute) -notmatch '^OneWayDps-Uninstall-[0-9a-f]{32}$'){throw 'Unsafe rollback cleanup path'}
    if(Test-Path -LiteralPath $absolute){Remove-Item -LiteralPath $absolute -Recurse -Force}
}

