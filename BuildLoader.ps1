param([switch]$WithSpeaker)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'GamePaths.ps1')
$game=Get-DpsGamePaths -Root $root
$managed=$game.Managed
$csc='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$core=Join-Path $PSScriptRoot 'vendor/BepInEx-5.4.23.6/BepInEx/core'
if(!(Test-Path -LiteralPath $core)){$core=Join-Path $PSScriptRoot 'payload/BepInEx/core'}
$out=Join-Path $PSScriptRoot 'build/v1.2'
New-Item -ItemType Directory -Path $out -Force|Out-Null
$names=@('mscorlib','System','System.Core','netstandard','Newtonsoft.Json','World','AutoCodes','Commons','Foundations','UnityEngine.CoreModule','UnityEngine.TextRenderingModule','UnityEngine.IMGUIModule','UnityEngine.JSONSerializeModule')
$refs=@($names|ForEach-Object{'/reference:'+(Join-Path $managed ($_+'.dll'))})
$sources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File|Where-Object Name -NotIn @('PatchWorld.cs','CheckBase.cs')|ForEach-Object FullName)
& $csc /nologo /noconfig /codepage:65001 /target:library /nostdlib+ /optimize+ ('/out:'+(Join-Path $out 'OneWayDps.dll')) $refs $sources
if($LASTEXITCODE -ne 0){throw 'DPS runtime compilation failed'}
foreach($n in @('Mono.Cecil.dll','BepInEx.dll')){Copy-Item -LiteralPath (Join-Path $core $n) -Destination $out -Force}
& $csc /nologo /codepage:65001 /target:library ('/reference:'+(Join-Path $core 'Mono.Cecil.dll')) ('/reference:'+(Join-Path $core 'BepInEx.dll')) ('/out:'+(Join-Path $out 'OneWayDps.Preloader.dll')) (Join-Path $PSScriptRoot 'src/PatchWorld.cs') (Join-Path $PSScriptRoot 'loader/SpeakerMemoryPatch.cs') (Join-Path $PSScriptRoot 'loader/Preloader.cs') (Join-Path $PSScriptRoot 'loader/AssemblyInfo.cs')
if($LASTEXITCODE -ne 0){throw 'Preloader compilation failed'}
& $csc /nologo /codepage:65001 /target:library ('/reference:'+(Join-Path $core 'BepInEx.dll')) ('/reference:'+(Join-Path $managed 'netstandard.dll')) ('/reference:'+(Join-Path $managed 'UnityEngine.dll')) ('/reference:'+(Join-Path $managed 'UnityEngine.CoreModule.dll')) ('/reference:'+(Join-Path $out 'OneWayDps.Preloader.dll')) ('/out:'+(Join-Path $out 'OneWayDps.Loader.dll')) (Join-Path $PSScriptRoot 'loader/Plugin.cs') (Join-Path $PSScriptRoot 'loader/AssemblyInfo.cs')
if($LASTEXITCODE -ne 0){throw 'Plugin compilation failed'}
& $csc /nologo ('/reference:'+(Join-Path $core 'Mono.Cecil.dll')) ('/reference:'+(Join-Path $out 'OneWayDps.Preloader.dll')) ('/out:'+(Join-Path $out 'MemoryPatchTests.exe')) (Join-Path $PSScriptRoot 'loader/MemoryPatchTests.cs')
if($LASTEXITCODE -ne 0){throw 'Memory tests compilation failed'}
& $csc /nologo ('/reference:'+(Join-Path $core 'Mono.Cecil.dll')) ('/reference:'+(Join-Path $out 'OneWayDps.Preloader.dll')) ('/out:'+(Join-Path $out 'LoaderRegressionTests.exe')) (Join-Path $PSScriptRoot 'loader/LoaderRegressionTests.cs')
if($LASTEXITCODE -ne 0){throw 'Loader regression compilation failed'}
if($WithSpeaker){
    $names=@('mscorlib','System','System.Core','netstandard','World','AutoCodes','Commons','Foundations','UnityEngine.CoreModule','UnityEngine.AudioModule','UnityEngine.ImageConversionModule','UnityEngine.UI','UnityEngine.UIModule','UnityEngine.UnityWebRequestModule','UnityEngine.UnityWebRequestAudioModule')
    $refs=@($names|ForEach-Object{'/reference:'+(Join-Path $managed ($_+'.dll'))})
    & $csc /nologo /noconfig /codepage:65001 /target:library /nostdlib+ /optimize+ ('/out:'+(Join-Path $out 'OneWaySpeaker.dll')) $refs (Join-Path $root 'SpeakerMod/src/Speaker.cs')
    if($LASTEXITCODE -ne 0){throw 'Optional speaker compilation failed'}
}
Write-Output 'Built DPS v1.2.0 independent memory loader. No game assemblies replaced.'
$payload=Join-Path $PSScriptRoot 'payload'
foreach($d in @('BepInEx/core','BepInEx/patchers','BepInEx/plugins/OneWayDps')){New-Item -ItemType Directory -Path (Join-Path $payload $d) -Force|Out-Null}
Get-ChildItem -LiteralPath $core -File|Copy-Item -Destination (Join-Path $payload 'BepInEx/core') -Force
$vendor=Split-Path (Split-Path $core -Parent) -Parent
foreach($n in @('winhttp.dll','doorstop_config.ini','.doorstop_version')){Copy-Item -LiteralPath (Join-Path $vendor $n) -Destination $payload -Force}
Copy-Item -LiteralPath (Join-Path $out 'OneWayDps.Preloader.dll') -Destination (Join-Path $payload 'BepInEx/patchers') -Force
foreach($n in @('OneWayDps.dll','OneWayDps.Loader.dll')){Copy-Item -LiteralPath (Join-Path $out $n) -Destination (Join-Path $payload 'BepInEx/plugins/OneWayDps') -Force}
