$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root=Split-Path $PSScriptRoot -Parent
$zip=Join-Path $root 'OneWayTickets_DPSMod_v1.2.0_BepInEx_x64.zip'
if(Test-Path -LiteralPath $zip){throw 'Release archive already exists'}
$stage=Join-Path $PSScriptRoot ('build/package-'+[Guid]::NewGuid().ToString('N'))
try{
    $mod=Join-Path $stage 'DPSMod';New-Item -ItemType Directory -Path $mod,(Join-Path $mod 'build/v1.2'),(Join-Path $mod 'vendor/source') -Force|Out-Null
    foreach($dir in @('src','loader','tests','payload','licenses','tools')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $dir) -Destination (Join-Path $mod $dir) -Recurse}
    foreach($name in @('README.md','LOADER_v1.2.md','THIRD_PARTY_NOTICES.md','CHANGELOG.md','更新日志.txt','VALIDATION.md','HISTORY.md','DAMAGE_AUDIT.md','SPEED_UNITS.md','AUXILIARY_DAMAGE.md','辅助伤害物品清单.txt','compatibility.json','GamePaths.ps1','Build.ps1','BuildLoader.ps1','Test.ps1','Install.ps1','安装或迁移.cmd','Repair.ps1','修复更新.cmd','Uninstall.ps1','UninstallLoader.ps1','Uninstall.cmd','DisableSpeaker.ps1','Package.ps1')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $mod}
    foreach($name in @('MemoryPatchTests.exe','LoaderRegressionTests.exe','OneWayDps.Preloader.dll','Mono.Cecil.dll','BepInEx.dll')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('build/v1.2/'+$name)) -Destination (Join-Path $mod 'build/v1.2')}
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'vendor/source/UnityDoorstop-v4.6.0-source.zip') -Destination (Join-Path $mod 'vendor/source')
    foreach($name in @('更新日志.txt','辅助伤害物品清单.txt')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $stage}
@"
再再启程 DPS Mod v1.2（1.2.0）独立加载器版

退出游戏，把本包解压到游戏根目录，再双击 DPSMod/安装或迁移.cmd。
本包不会通过解压覆盖游戏 DLL；安装器会先检查、备份并迁移旧版静态补丁。
正常启动游戏即可。已安装 v1.2 后，兼容的小更新会在下次启动重新挂接；接口变化仍需适配。
请保留旧安装记录和原始备份。旧文件混装或缺少备份时先用 Steam 验证游戏文件，再安装。
存档、DPSMod/history 和 settings.json 保留。包内不含任何个人数据、游戏 DLL、音箱歌曲或贴图。

F1 视图；F2 时间范围；F3 隐藏；F4 清零；F5 精简；F6 辅助伤害；F7 历史。
日志：BepInEx/LogOutput.log。详细安装、共存与卸载见 DPSMod/LOADER_v1.2.md。
"@|Set-Content -LiteralPath (Join-Path $stage '安装说明.txt') -Encoding UTF8
    @{version='1.2.0';loadingMode='bepinex-memory';loader='BepInEx 5.4.23.6 x64';testedGameVersion='0.8.0.3';testedSteamBuild='26211032';includesGameAssemblies=$false;includesPersonalData=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $mod 'package.json') -Encoding UTF8
    foreach($f in Get-ChildItem -LiteralPath $stage -File -Recurse){if($f.Name -in @('World.dll','Commons.dll','Camp.dll','history.json','settings.json','installed.json') -or $f.Extension -in @('.dat','.mp3')){throw ('Private/game file in package: '+$f.FullName)}}
    [IO.Compression.ZipFile]::CreateFromDirectory($stage,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
    ((Get-FileHash -LiteralPath $zip).Hash+'  '+[IO.Path]::GetFileName($zip))|Set-Content -LiteralPath ($zip+'.sha256.txt') -Encoding UTF8
    Write-Output $zip;Write-Output (Get-FileHash -LiteralPath $zip).Hash
}finally{
    $base=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'build')).TrimEnd('\')+'\';$absolute=[IO.Path]::GetFullPath($stage)
    if(!$absolute.StartsWith($base,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($absolute) -notmatch '^package-[0-9a-f]{32}$'){throw 'Unsafe package cleanup path'}
    if(Test-Path -LiteralPath $absolute){Remove-Item -LiteralPath $absolute -Recurse -Force}
}