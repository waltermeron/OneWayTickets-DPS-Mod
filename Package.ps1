$ErrorActionPreference='Stop'
Import-Module Microsoft.PowerShell.Utility -ErrorAction Stop
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root=Split-Path $PSScriptRoot -Parent
$version='1.0.2'
$zip=Join-Path $root ('再再启程_DPS可视化Mod_v'+$version+'_适配0.8.0.2.zip')
if(Test-Path -LiteralPath $zip){throw 'Release archive already exists.'}
$stage=Join-Path ([IO.Path]::GetTempPath()) ('OneWayDps-Release-'+[Guid]::NewGuid().ToString('N'))
$state=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'installed.json') -Raw | ConvertFrom-Json
if($state.version -ne $version){throw 'Install the matching version before packaging.'}
$managed=Join-Path $root '再再启程_Data/Managed'
foreach($pair in @(@('World.dll','patchedSha256'),@('OneWayDps.dll','modSha256'))){if((Get-FileHash -LiteralPath (Join-Path $managed $pair[0]) -Algorithm SHA256).Hash -ne $state.($pair[1])){throw 'Installed DLL hash mismatch.'}}
if((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'backup/World.dll') -Algorithm SHA256).Hash -ne $state.originalSha256){throw 'Original DLL backup hash mismatch.'}
try {
    foreach($dir in @('DPSMod/backup','DPSMod/build','DPSMod/src','DPSMod/tests','再再启程_Data/Managed')){New-Item -ItemType Directory -Path (Join-Path $stage $dir) -Force | Out-Null}
    foreach($name in @('World.dll','OneWayDps.dll')){Copy-Item -LiteralPath (Join-Path $managed $name) -Destination (Join-Path $stage '再再启程_Data/Managed')}
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'backup/World.dll') -Destination (Join-Path $stage 'DPSMod/backup/World.dll')
    foreach($name in @('README.md','CHANGELOG.md','DAMAGE_AUDIT.md','SPEED_UNITS.md','VALIDATION.md','compatibility.json','installed.json','Uninstall.ps1','Uninstall.cmd','Test.ps1','Build.ps1')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $stage 'DPSMod')}
    Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -File -Filter '*.cs' | Copy-Item -Destination (Join-Path $stage 'DPSMod/src')
    Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -File -Filter '*.cs' | Copy-Item -Destination (Join-Path $stage 'DPSMod/tests')
    Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'build') -File -Filter '*Tests.exe' | Copy-Item -Destination (Join-Path $stage 'DPSMod/build')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '更新日志.txt') -Destination (Join-Path $stage '更新日志.txt')
    $state | Add-Member -NotePropertyName gameVersion -NotePropertyValue '0.8.0.2'
    $state | Add-Member -NotePropertyName archiveLayout -NotePropertyValue 'Extract contents into game root'
    $state | Add-Member -NotePropertyName author -NotePropertyValue 'B站：-_小西瓜_-'
    $state | Add-Member -NotePropertyName authorUid -NotePropertyValue '153345791'
    $state | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage 'DPSMod/package.json') -Encoding UTF8
@'
再再启程 DPS 可视化 Mod v1.0.2
适配游戏 v0.8.0.2（Windows / Unity Mono）

退出游戏，将压缩包全部内容解压至“再再启程.exe”所在目录，允许覆盖同名文件，正常启动游戏即可。
顶部统计区最右侧显示车速，单位为 m/s（米/秒），F5 精简视图同样显示。单位放在数值下方的标签中，保留五列统计对齐。更新内容及此前开发记录见同级“更新日志.txt”。

F1 切换武器明细、实时曲线、奇物增益统计；F2 时间范围；F3 隐藏；F4 清零；F5 精简/完整视图。
拖动顶部移动窗口，拖动右下角调整宽高；窗口设置中可调背景透明度、自动适配屏幕和恢复默认。

卸载：退出游戏后双击 DPSMod\Uninstall.cmd。请保留 DPSMod\backup\World.dll 和 installed.json。
压缩包不包含个人存档和个人窗口设置。仅适配标明的游戏版本；详细说明见 DPSMod\README.md。
'@ | Set-Content -LiteralPath (Join-Path $stage 'DPSMod_解压使用说明.txt') -Encoding UTF8
    [IO.Compression.ZipFile]::CreateFromDirectory($stage,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
    $hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    ($hash+'  '+[IO.Path]::GetFileName($zip)) | Set-Content -LiteralPath ($zip+'.sha256.txt') -Encoding UTF8
    Write-Output ('Created '+$zip)
    Write-Output ('SHA256 '+$hash)
} finally {
    $tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    $absolute=[IO.Path]::GetFullPath($stage)
    if(!$absolute.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($absolute) -notmatch '^OneWayDps-Release-[0-9a-f]{32}$'){throw 'Unsafe staging cleanup path.'}
    if(Test-Path -LiteralPath $absolute){Remove-Item -LiteralPath $absolute -Recurse -Force}
}
