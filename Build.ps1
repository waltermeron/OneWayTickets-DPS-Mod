$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$managed=Join-Path $root '再再启程_Data/Managed'
$names=@('mscorlib','System','System.Core','netstandard','World','AutoCodes','Commons','Foundations','UnityEngine.CoreModule','UnityEngine.TextRenderingModule','UnityEngine.IMGUIModule','UnityEngine.JSONSerializeModule')
$references=@($names | ForEach-Object {'/reference:'+(Join-Path $managed ($_+'.dll'))})
$sources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -File -Filter '*.cs' | Where-Object Name -ne 'PatchWorld.cs' | ForEach-Object FullName)
$csc='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'build') -Force | Out-Null
& $csc /nologo /noconfig /codepage:65001 /target:library /nostdlib+ /optimize+ ('/out:'+(Join-Path $PSScriptRoot 'build/OneWayDps.dll')) $references $sources
if($LASTEXITCODE -ne 0){throw 'Mod compilation failed'}
