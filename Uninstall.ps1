$ErrorActionPreference='Stop'
Import-Module Microsoft.PowerShell.Utility -ErrorAction Stop
$root=Split-Path $PSScriptRoot -Parent
$data=Get-ChildItem -LiteralPath $root -Directory -Filter '*_Data' | Select-Object -First 1
$gameName=$data.Name.Substring(0,$data.Name.Length-5)
if(Get-Process -Name $gameName -ErrorAction SilentlyContinue){throw 'Close the game before uninstalling the mod.'}
$world=Join-Path $data.FullName 'Managed/World.dll'
$mod=Join-Path $data.FullName 'Managed/OneWayDps.dll'
$backup=Join-Path $PSScriptRoot 'backup/World.dll'
$manifest=Join-Path $PSScriptRoot 'installed.json'
if(!(Test-Path -LiteralPath $manifest)){throw 'Install record not found. No files were changed.'}
$state=Get-Content -Raw $manifest | ConvertFrom-Json
if((Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash -ne $state.originalSha256){throw 'Backup hash mismatch. No files were changed.'}
$current=(Get-FileHash -LiteralPath $world -Algorithm SHA256).Hash
if($current -eq $state.originalSha256 -and !(Test-Path -LiteralPath $mod)){Write-Host 'DPS mod is already uninstalled.'; return}
if($current -ne $state.patchedSha256){throw 'Game files have changed since installation. Refusing to overwrite a game update or another mod.'}
if((Test-Path -LiteralPath $mod) -and (Get-FileHash -LiteralPath $mod -Algorithm SHA256).Hash -ne $state.modSha256){throw 'OneWayDps.dll has changed. No files were changed.'}
Copy-Item -LiteralPath $backup -Destination $world -Force
if(Test-Path -LiteralPath $mod){Remove-Item -LiteralPath $mod}
Write-Host 'Original World.dll restored. DPS mod uninstalled. Backup preserved.'

