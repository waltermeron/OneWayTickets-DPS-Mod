$ErrorActionPreference='Stop'
. (Join-Path (Split-Path $PSScriptRoot -Parent) 'GamePaths.ps1')
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('OneWayDps-Paths-'+[Guid]::NewGuid().ToString('N'))
$count=0
function Assert-PathTest([bool]$Value,[string]$Message){if(!$Value){throw $Message};$script:count++}
try{
    foreach($dir in @('再再启程_Data/Managed','One Way Tickets_Data/Managed')){New-Item -ItemType Directory -Path (Join-Path $fixture $dir) -Force|Out-Null;foreach($name in @('World.dll','Commons.dll','OneWayDps.dll')){[IO.File]::WriteAllText((Join-Path $fixture ($dir+'/'+$name)),'fixture')}}
    $core=Join-Path $fixture 'One Way Tickets_Data/Managed/UnityEngine.CoreModule.dll';[IO.File]::WriteAllText($core,'fixture')
    $found=Get-DpsGamePaths -Root $fixture
    Assert-PathTest ($found.DataName -eq 'One Way Tickets_Data') 'Legacy mod remnants selected as game'
    Assert-PathTest ($found.ProcessName -eq 'One Way Tickets') 'Wrong game process name'
    Assert-PathTest ($found.Executable -eq (Join-Path $fixture 'One Way Tickets.exe')) 'Wrong executable path'
    Assert-PathTest ($found.Managed -eq (Join-Path $fixture 'One Way Tickets_Data/Managed')) 'Wrong managed path'
    Remove-Item -LiteralPath $core
    Assert-PathTest ((Get-DpsGamePaths -Root $fixture -DataDirectory 'One Way Tickets_Data').DataName -eq 'One Way Tickets_Data') 'Pinned release checkout cannot resolve'
    $failed=$false;try{Get-DpsGamePaths -Root $fixture|Out-Null}catch{$failed=$true};Assert-PathTest $failed 'Incomplete legacy directory should be rejected'
    $failed=$false;try{Get-DpsGamePaths -Root $fixture -DataDirectory '../outside_Data'|Out-Null}catch{$failed=$true};Assert-PathTest $failed 'Traversal must be rejected'
    $failed=$false;try{Get-DpsGamePaths -Root $fixture -DataDirectory 'missing_Data'|Out-Null}catch{$failed=$true};Assert-PathTest $failed 'Missing pinned directory must fail'
    [IO.File]::WriteAllText($core,'fixture');[IO.File]::WriteAllText((Join-Path $fixture '再再启程_Data/Managed/UnityEngine.CoreModule.dll'),'fixture')
    $failed=$false;try{Get-DpsGamePaths -Root $fixture|Out-Null}catch{$failed=$true};Assert-PathTest $failed 'Ambiguous full installations must fail'
    Assert-PathTest ((Get-DpsGamePaths -Root $fixture -DataDirectory 'One Way Tickets_Data').ProcessName -eq 'One Way Tickets') 'Pinned path ignored when two installations exist'
    Write-Output ('PASS game paths: '+$count+' assertions; renamed executable/data, legacy remnants, release checkout and ambiguity guards')
}finally{
    $prefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\';$absolute=[IO.Path]::GetFullPath($fixture)
    if(!$absolute.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($absolute) -notmatch '^OneWayDps-Paths-[0-9a-f]{32}$'){throw 'Unsafe fixture cleanup path'}
    if(Test-Path -LiteralPath $absolute){Remove-Item -LiteralPath $absolute -Recurse -Force}
}
