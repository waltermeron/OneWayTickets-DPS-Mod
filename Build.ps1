$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$managed=Join-Path $root '再再启程_Data/Managed'
$compatibility=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'compatibility.json') -Raw|ConvertFrom-Json
foreach($entry in @(@('World.dll','originalWorldSha256'),@('Commons.dll','originalCommonsSha256'))){
    if((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot ('backup/'+$entry[0]))).Hash -ne $compatibility.($entry[1])){throw 'Original assembly backup does not match compatibility.json. Restore matching originals before building.'}
}
$installedPath=Join-Path $PSScriptRoot 'installed.json'
$installed=if(Test-Path -LiteralPath $installedPath){Get-Content -LiteralPath $installedPath -Raw|ConvertFrom-Json}else{$null}
foreach($entry in @(@('World.dll','originalWorldSha256','patchedSha256'),@('Commons.dll','originalCommonsSha256','patchedCommonsSha256'))){
    $currentHash=(Get-FileHash -LiteralPath (Join-Path $managed $entry[0])).Hash
    if($currentHash -ne $compatibility.($entry[1]) -and (!$installed -or $currentHash -ne $installed.($entry[2]))){throw ('Game update detected: '+$entry[0]+'. Obtain current official backups and adapt before building.')}
}
if($compatibility.requiredAssemblies){foreach($dependency in $compatibility.requiredAssemblies.PSObject.Properties){
    if((Get-FileHash -LiteralPath (Join-Path $managed $dependency.Name)).Hash -ne $dependency.Value){throw ('Game version mismatch: '+$dependency.Name+'. Adapt the mod to this game update before building.')}
}}
$names=@('mscorlib','System','System.Core','netstandard','Newtonsoft.Json','World','AutoCodes','Commons','Foundations','UnityEngine.CoreModule','UnityEngine.TextRenderingModule','UnityEngine.IMGUIModule','UnityEngine.JSONSerializeModule')
$references=@($names | ForEach-Object {'/reference:'+(Join-Path $managed ($_+'.dll'))})
$sources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -File -Filter '*.cs' | Where-Object Name -ne 'PatchWorld.cs' | ForEach-Object FullName)
$csc='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'build') -Force | Out-Null
& $csc /nologo /noconfig /codepage:65001 /target:library /nostdlib+ /optimize+ ('/out:'+(Join-Path $PSScriptRoot 'build/OneWayDps.dll')) $references $sources
if($LASTEXITCODE -ne 0){throw 'Mod compilation failed'}
$cecil=Join-Path $PSScriptRoot 'tools/cecil/Mono.Cecil.dll'
if(!(Test-Path -LiteralPath $cecil)){throw 'Missing bundled build dependency tools/cecil/Mono.Cecil.dll'}
Copy-Item -LiteralPath $cecil -Destination (Join-Path $PSScriptRoot 'build/Mono.Cecil.dll') -Force
& $csc /nologo /codepage:65001 ('/reference:'+$cecil) ('/out:'+(Join-Path $PSScriptRoot 'build/PatchWorld.exe')) (Join-Path $PSScriptRoot 'src/PatchWorld.cs')
if($LASTEXITCODE -ne 0){throw 'Patch tool compilation failed'}
& (Join-Path $PSScriptRoot 'build/PatchWorld.exe') (Join-Path $PSScriptRoot 'backup/World.dll') (Join-Path $PSScriptRoot 'build/OneWayDps.dll') (Join-Path $PSScriptRoot 'build/World.dll') (Join-Path $PSScriptRoot 'backup/Commons.dll') (Join-Path $PSScriptRoot 'build/Commons.dll')
if($LASTEXITCODE -ne 0){throw 'Game patch generation failed'}
