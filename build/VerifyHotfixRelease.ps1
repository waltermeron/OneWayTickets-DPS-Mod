$ErrorActionPreference='Stop'
Import-Module Microsoft.PowerShell.Utility
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$zip=Join-Path $root '再再启程_DPS可视化Mod_v1.1.1_适配0.8.0.3.zip'
$temp=Join-Path ([IO.Path]::GetTempPath()) ('OneWayDps-Verify-'+[Guid]::NewGuid().ToString('N'))
$managed=Join-Path $root '再再启程_Data/Managed';$hashes=@{}
foreach($name in @('World.dll','Commons.dll','OneWayDps.dll')){$hashes[$name]=(Get-FileHash -LiteralPath (Join-Path $managed $name)).Hash}
try{
    [IO.Compression.ZipFile]::ExtractToDirectory($zip,$temp)
    $count=0
    foreach($file in Get-ChildItem -LiteralPath $temp -File -Recurse){
        $relative=$file.FullName.Substring($temp.Length+1)
        if($relative -in @('DPSMod\package.json','DPSMod_解压使用说明.txt')){continue}
        $source=if($relative -in @('更新日志.txt','辅助伤害物品清单.txt')){Join-Path $root ('DPSMod/'+$relative)}else{Join-Path $root $relative}
        if(!(Test-Path -LiteralPath $source) -or (Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash){throw ('Archive mismatch: '+$relative)}
        $count++
    }
    foreach($private in @('SaveData','DPSMod/history','DPSMod/settings.json','DPSMod/backup/v0.8.0.2','DPSMod/backup/v1.1.1-before-hotfix')){if(Test-Path -LiteralPath (Join-Path $temp $private)){throw 'Personal or legacy files included'}}
    $manifest=Get-Content -LiteralPath (Join-Path $temp 'DPSMod/package.json') -Raw|ConvertFrom-Json
    if($manifest.version -ne '1.1.1' -or $manifest.gameVersion -ne '0.8.0.3' -or $manifest.patchedSha256 -ne $hashes['World.dll'] -or $manifest.patchedCommonsSha256 -ne $hashes['Commons.dll'] -or $manifest.modSha256 -ne $hashes['OneWayDps.dll']){throw 'Manifest mismatch'}
    $usage=Get-Content -LiteralPath (Join-Path $temp 'DPSMod_解压使用说明.txt') -Raw
    if($usage -notmatch 'v1\.1\.1' -or $usage -notmatch 'v0\.8\.0\.3' -or $usage -notmatch 'F7'){throw 'Usage incomplete'}
    # Supply unchanged game dependencies only to the isolated developer-test checkout.
    $refs=@('mscorlib','System','System.Core','netstandard','Newtonsoft.Json','AutoCodes','Commons','Foundations','Init','UnityEngine.CoreModule','UnityEngine.TextRenderingModule','UnityEngine.IMGUIModule','UnityEngine.JSONSerializeModule')
    foreach($name in $refs){if($name -ne 'Commons'){Copy-Item -LiteralPath (Join-Path $managed ($name+'.dll')) -Destination (Join-Path $temp ('再再启程_Data/Managed/'+$name+'.dll'))}}
    & (Join-Path $temp 'DPSMod/Build.ps1')
    if($LASTEXITCODE -ne 0){throw 'Extracted source build failed'}
    & (Join-Path $temp 'DPSMod/Test.ps1')
    if($LASTEXITCODE -ne 0){throw 'Extracted tests failed'}
    foreach($case in @(@('AutoCodes.dll','Game version mismatch'),@('World.dll','Game update detected'))){
        $path=Join-Path $temp ('再再启程_Data/Managed/'+$case[0]);$stream=[IO.File]::Open($path,[IO.FileMode]::Append);$stream.WriteByte(0);$stream.Dispose()
        $rejected=$false;try{& (Join-Path $temp 'DPSMod/Build.ps1')}catch{if($_.Exception.Message -notmatch $case[1]){throw};$rejected=$true}
        if(!$rejected){throw 'Build guard accepted changed game files'}
        Copy-Item -LiteralPath (Join-Path $managed $case[0]) -Destination $path -Force
    }
    $history=Join-Path $temp 'DPSMod/history';New-Item -ItemType Directory -Path $history|Out-Null
    [IO.File]::WriteAllText((Join-Path $history 'history.json'),'preserve-user-history')
    & (Join-Path $temp 'DPSMod/Uninstall.ps1')
    foreach($pair in @(@('World.dll','originalSha256'),@('Commons.dll','originalCommonsSha256'))){if((Get-FileHash -LiteralPath (Join-Path $temp ('再再启程_Data/Managed/'+$pair[0]))).Hash -ne $manifest.($pair[1])){throw 'Uninstall did not restore current originals'}}
    if(Test-Path -LiteralPath (Join-Path $temp '再再启程_Data/Managed/OneWayDps.dll')){throw 'Mod DLL remains after uninstall'}
    if([IO.File]::ReadAllText((Join-Path $history 'history.json')) -ne 'preserve-user-history'){throw 'Uninstall changed history'}
    foreach($name in $hashes.Keys){if((Get-FileHash -LiteralPath (Join-Path $managed $name)).Hash -ne $hashes[$name]){throw 'Real install changed during isolated tests'}}
    Write-Output ('Release verified: '+$count+' copied files + 2 generated files; extracted build/tests; update guards; isolated v0.8.0.3 uninstall; installed files preserved.')
}finally{
    $prefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\';$absolute=[IO.Path]::GetFullPath($temp)
    if(!$absolute.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($absolute) -notmatch '^OneWayDps-Verify-[0-9a-f]{32}$'){throw 'Unsafe verification cleanup path'}
    if(Test-Path -LiteralPath $absolute){Remove-Item -LiteralPath $absolute -Recurse -Force}
}
