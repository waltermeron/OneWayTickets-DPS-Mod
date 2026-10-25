$ErrorActionPreference='Stop'
$modRoot=Split-Path $PSScriptRoot -Parent
$root=Split-Path $modRoot -Parent
if(Get-Process -Name '再再启程' -ErrorAction SilentlyContinue){throw 'Close the game before installing'}
$managed=Join-Path $root '再再启程_Data/Managed'
$compat=Get-Content -LiteralPath (Join-Path $modRoot 'compatibility.json') -Raw|ConvertFrom-Json
if($compat.version -ne '1.1.1' -or $compat.gameVersion -ne '0.8.0.3'){throw 'Unexpected compatibility manifest'}
foreach($dependency in $compat.requiredAssemblies.PSObject.Properties){if((Get-FileHash -LiteralPath (Join-Path $managed $dependency.Name)).Hash -ne $dependency.Value){throw 'Base game dependencies changed'}}
$previous=Get-Content -LiteralPath (Join-Path $modRoot 'installed.json') -Raw|ConvertFrom-Json
foreach($pair in @(@('World.dll','originalWorldSha256','patchedSha256'),@('Commons.dll','originalCommonsSha256','patchedCommonsSha256'))){
    $hash=(Get-FileHash -LiteralPath (Join-Path $managed $pair[0])).Hash
    if($hash -ne $compat.($pair[1]) -and $hash -ne $previous.($pair[2])){throw ('Unexpected current assembly: '+$pair[0])}
    if((Get-FileHash -LiteralPath (Join-Path $modRoot ('backup/'+$pair[0]))).Hash -ne $compat.($pair[1])){throw 'Original backup mismatch'}
}
$rollback=Join-Path $PSScriptRoot ('HotfixRollback-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $rollback|Out-Null
$files=@('World.dll','Commons.dll','OneWayDps.dll');$manifests=@('installed.json','package.json')
foreach($name in $files){if(Test-Path -LiteralPath (Join-Path $managed $name)){Copy-Item -LiteralPath (Join-Path $managed $name) -Destination $rollback}}
foreach($name in $manifests){if(Test-Path -LiteralPath (Join-Path $modRoot $name)){Copy-Item -LiteralPath (Join-Path $modRoot $name) -Destination $rollback}}
try{
    foreach($name in $files){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $managed $name) -Force;if((Get-FileHash -LiteralPath (Join-Path $managed $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $name)).Hash){throw 'Installed hash verification failed'}}
    $state=[ordered]@{version='1.1.1';originalSha256=$compat.originalWorldSha256;patchedSha256=(Get-FileHash -LiteralPath (Join-Path $managed 'World.dll')).Hash;modSha256=(Get-FileHash -LiteralPath (Join-Path $managed 'OneWayDps.dll')).Hash;installedAt=[DateTimeOffset]::Now.ToString('o');originalCommonsSha256=$compat.originalCommonsSha256;patchedCommonsSha256=(Get-FileHash -LiteralPath (Join-Path $managed 'Commons.dll')).Hash;gameVersion='0.8.0.3';steamBuildId='26195956'}
    $state|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $modRoot 'installed.json') -Encoding UTF8
    $state['author']='B站：-_小西瓜_-';$state['authorUid']='153345791';$state['archiveLayout']='Extract contents into game root'
    $state|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $modRoot 'package.json') -Encoding UTF8
    Write-Output 'Installed v1.1.1 for game v0.8.0.3. Saves, history and preferences preserved.'
}catch{
    foreach($name in $files){if(Test-Path -LiteralPath (Join-Path $rollback $name)){Copy-Item -LiteralPath (Join-Path $rollback $name) -Destination (Join-Path $managed $name) -Force}}
    foreach($name in $manifests){if(Test-Path -LiteralPath (Join-Path $rollback $name)){Copy-Item -LiteralPath (Join-Path $rollback $name) -Destination (Join-Path $modRoot $name) -Force}}
    throw
}finally{
    $prefix=[IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')+'\';$absolute=[IO.Path]::GetFullPath($rollback)
    if(!$absolute.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($absolute) -notmatch '^HotfixRollback-[0-9a-f]{32}$'){throw 'Unsafe rollback cleanup path'}
    if(Test-Path -LiteralPath $absolute){Remove-Item -LiteralPath $absolute -Recurse -Force}
}
