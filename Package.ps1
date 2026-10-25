$ErrorActionPreference='Stop'
Import-Module Microsoft.PowerShell.Utility
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root=Split-Path $PSScriptRoot -Parent
$zip=Join-Path $root '再再启程_DPS可视化Mod_v1.1.1_适配0.8.0.3.zip'
if(Test-Path -LiteralPath $zip){throw 'Release archive already exists'}
$state=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'installed.json') -Raw|ConvertFrom-Json
if($state.version -ne '1.1.1'){throw 'Install the matching version before packaging'}
$managed=Join-Path $root '再再启程_Data/Managed'
foreach($entry in @(@('World.dll','patchedSha256'),@('Commons.dll','patchedCommonsSha256'),@('OneWayDps.dll','modSha256'))){if((Get-FileHash -LiteralPath (Join-Path $managed $entry[0])).Hash -ne $state.($entry[1])){throw 'Installed file mismatch'}}
foreach($entry in @(@('World.dll','originalSha256'),@('Commons.dll','originalCommonsSha256'))){if((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot ('backup/'+$entry[0]))).Hash -ne $state.($entry[1])){throw 'Original backup mismatch'}}
$stage=Join-Path ([IO.Path]::GetTempPath()) ('OneWayDps-Release-'+[Guid]::NewGuid().ToString('N'))
try {
    foreach($dir in @('DPSMod/backup','DPSMod/build','DPSMod/src','DPSMod/tests','DPSMod/tools/cecil','再再启程_Data/Managed')){New-Item -ItemType Directory -Path (Join-Path $stage $dir) -Force|Out-Null}
    foreach($name in @('World.dll','Commons.dll','OneWayDps.dll')){Copy-Item -LiteralPath (Join-Path $managed $name) -Destination (Join-Path $stage '再再启程_Data/Managed')}
    foreach($name in @('World.dll','Commons.dll')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('backup/'+$name)) -Destination (Join-Path $stage 'DPSMod/backup')}
    foreach($name in @('README.md','CHANGELOG.md','更新日志.txt','DAMAGE_AUDIT.md','SPEED_UNITS.md','AUXILIARY_DAMAGE.md','辅助伤害物品清单.txt','HISTORY.md','HOTFIX_v1.1.1.md','VALIDATION.md','compatibility.json','installed.json','Uninstall.ps1','Uninstall.cmd','Build.ps1','Test.ps1','Package.ps1')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $stage 'DPSMod')}
    foreach($name in @('Mono.Cecil.dll','Mono.Cecil.nuspec','LICENSE.txt')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('tools/cecil/'+$name)) -Destination (Join-Path $stage 'DPSMod/tools/cecil')}
    foreach($dir in @('src','tests')){Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $dir) -Filter '*.cs' -File|Copy-Item -Destination (Join-Path $stage ('DPSMod/'+$dir))}
    Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'build') -File -Filter '*Tests.exe'|Copy-Item -Destination (Join-Path $stage 'DPSMod/build')
    foreach($name in @('更新日志.txt','辅助伤害物品清单.txt')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $stage $name)}
    $state|Add-Member -NotePropertyName gameVersion -NotePropertyValue '0.8.0.3' -Force
    $state|Add-Member -NotePropertyName archiveLayout -NotePropertyValue 'Extract contents into game root' -Force
    $state|Add-Member -NotePropertyName author -NotePropertyValue 'B站：-_小西瓜_-' -Force
    $state|Add-Member -NotePropertyName authorUid -NotePropertyValue '153345791' -Force
    $state|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $stage 'DPSMod/package.json') -Encoding UTF8
@'
再再启程 DPS 可视化 Mod v1.1.1 热修整合版
仅适配游戏 v0.8.0.3（Steam Build 26195956 / Windows Unity Mono）。

退出游戏，把本包全部内容解压到“再再启程.exe”所在根目录，允许覆盖，勿套一层文件夹。
本版修复 v0.8.0.3 新资源与旧版 Mod 程序集混用时卡在战斗界面、车辆界面异常及部分贴图缺失。请勿再次覆盖 v0.8.0.2 的旧 Mod 包。
如果曾混装旧包或文件仍异常，先用 Steam 验证游戏文件完整性，等恢复完成，再解压本包。
以后游戏更新时需重新适配，不能用旧 Mod 覆盖包恢复更新后的游戏文件。

F1 武器/曲线/奇物/历史；F2 时间范围；F3 隐藏；F4 当前面板清零；F5 精简；F6 辅助伤害合并/分开；F7 历史查询。
历史保存在 DPSMod/history/history.json。本包不含个人存档、历史和窗口设置；安装不清除既有数据。
退出游戏后双击 DPSMod/Uninstall.cmd 可恢复 v0.8.0.3 原始 DLL，保留个人数据。
完整版本记录见“更新日志.txt”，热修说明见 DPSMod/HOTFIX_v1.1.1.md。
'@|Set-Content -LiteralPath (Join-Path $stage 'DPSMod_解压使用说明.txt') -Encoding UTF8
    [IO.Compression.ZipFile]::CreateFromDirectory($stage,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
    $hash=(Get-FileHash -LiteralPath $zip).Hash
    ($hash+'  '+[IO.Path]::GetFileName($zip))|Set-Content -LiteralPath ($zip+'.sha256.txt') -Encoding UTF8
    Write-Output ('Created '+$zip);Write-Output ('SHA256 '+$hash)
}finally{
    $prefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\';$absolute=[IO.Path]::GetFullPath($stage)
    if(!$absolute.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($absolute) -notmatch '^OneWayDps-Release-[0-9a-f]{32}$'){throw 'Unsafe staging cleanup path'}
    if(Test-Path -LiteralPath $absolute){Remove-Item -LiteralPath $absolute -Recurse -Force}
}
