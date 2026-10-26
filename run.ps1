param([switch]$Capture,[switch]$SkipBuild,[ValidateSet('tree-impact','grass','wildlife','buggy','pickup','wagon','car','car-rear','tracks-right','tracks','vegetation-detail','vegetation','junction','world','gameplay','title','challenge','letters','letter-pictures','letter-pictures-2','reward','accessories','decorated','village','river','pond','rabbit','fox','bear')][string]$View='gameplay')
. "$PSScriptRoot/scripts-common.ps1"
if (-not $SkipBuild) { & "$PSScriptRoot/build.ps1" }
$godot = Find-Godot
if ($Capture) { & $godot --path $PSScriptRoot -- --play --capture "--view=$View" }
else { & $godot --path $PSScriptRoot }
Assert-ExitCode 'Spuštění'





