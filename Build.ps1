$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$withSpeaker=Test-Path -LiteralPath (Join-Path $root 'SpeakerMod/src/Speaker.cs')
& (Join-Path $PSScriptRoot 'BuildLoader.ps1') -WithSpeaker:$withSpeaker