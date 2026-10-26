function Get-DpsGamePaths {
    param([Parameter(Mandatory=$true)][string]$Root,[string]$DataDirectory)
    $absoluteRoot=[IO.Path]::GetFullPath($Root).TrimEnd('\')
    if($DataDirectory){
        if($DataDirectory -ne [IO.Path]::GetFileName($DataDirectory) -or !$DataDirectory.EndsWith('_Data')){throw 'Invalid game data directory in manifest'}
        $data=Get-Item -LiteralPath (Join-Path $absoluteRoot $DataDirectory) -ErrorAction Stop
        if(!$data.PSIsContainer){throw 'Game data path must be a directory'}
    }else{
        $candidates=@(Get-ChildItem -LiteralPath $absoluteRoot -Directory -Filter '*_Data'|Where-Object {
            (Test-Path -LiteralPath (Join-Path $_.FullName 'Managed/World.dll')) -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'Managed/Commons.dll')) -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'Managed/UnityEngine.CoreModule.dll'))
        })
        if($candidates.Count -ne 1){throw 'Cannot uniquely identify the Unity game data directory; specify it in compatibility.json'}
        $data=$candidates[0]
    }
    $managed=Join-Path $data.FullName 'Managed'
    foreach($name in @('World.dll','Commons.dll')){if(!(Test-Path -LiteralPath (Join-Path $managed $name))){throw ('Missing game assembly: '+$name)}}
    $processName=$data.Name.Substring(0,$data.Name.Length-5)
    [pscustomobject]@{DataName=$data.Name;DataDirectory=$data.FullName;Managed=$managed;ProcessName=$processName;Executable=Join-Path $absoluteRoot ($processName+'.exe')}
}
