param([Parameter(Mandatory=$true)][string]$Baseline)
$ErrorActionPreference='Stop'
function Read-SceneBlocks([string]$Path) {
    $text=[IO.File]::ReadAllText((Resolve-Path -LiteralPath $Path)).Replace("`r`n","`n")
    $blocks=@{}
    foreach($match in [regex]::Matches($text,'(?ms)^--- !u!(\d+) &(-?\d+)[^\n]*\n.*?(?=^--- !u!|\z)')) {
        $blocks[$match.Groups[2].Value]=@{Class=[int]$match.Groups[1].Value;Text=$match.Value.TrimEnd()}
    }
    return $blocks
}
$before=Read-SceneBlocks $Baseline
$after=Read-SceneBlocks 'Assets/Scenes/Circuit_02.unity'
$types=@(114,54,64,65,135,136,146)
$count=0
foreach($id in $before.Keys) {
    if($types -notcontains $before[$id].Class){continue}
    if(!$after.ContainsKey($id) -or $before[$id].Text -cne $after[$id].Text){throw "Gameplay/road component changed: $id (class $($before[$id].Class))"}
    $count++
}
$waypoints=@{}
foreach($id in $before.Keys) {
    if($before[$id].Class -eq 1 -and $before[$id].Text -match '(?m)^  m_Name: Waypoint_\d+\s*$'){$waypoints[$id]=$true}
}
$transforms=0
foreach($id in $before.Keys) {
    $block=$before[$id]
    if($block.Class -eq 4 -and $block.Text -match 'm_GameObject: \{fileID: (-?\d+)\}' -and $waypoints.ContainsKey($Matches[1])) {
        if(!$after.ContainsKey($id) -or $block.Text -cne $after[$id].Text){throw "Waypoint transform changed: $id"}
        $transforms++
    }
}
if($transforms -ne 132){throw "Unexpected AI waypoint count: $transforms"}
$report="COMPLETE: PASS`n$count original physics/gameplay/ProBuilder components byte-identical after save; 132 waypoint transforms byte-identical.`nBaseline: $Baseline`n"
[IO.File]::WriteAllText((Join-Path (Get-Location) 'Logs/circuit02-sierras-preservation.txt'),$report)
$report
