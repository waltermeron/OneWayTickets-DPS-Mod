param([switch]$CheckOnly)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Install.ps1') -CheckOnly:$CheckOnly