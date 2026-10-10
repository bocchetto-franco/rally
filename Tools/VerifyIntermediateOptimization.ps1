param([Parameter(Mandatory=$true)][string]$Baseline)
$ErrorActionPreference='Stop'
function Read-SceneBlocks([string]$Path) {
    $sceneText=[IO.File]::ReadAllText((Resolve-Path -LiteralPath $Path)).Replace("`r`n","`n")
    $blocks=@{}
    foreach($match in [regex]::Matches($sceneText,'(?ms)^--- !u!(\d+) &(-?\d+)[^\n]*\n.*?(?=^--- !u!|\z)')) {
        $blocks[$match.Groups[2].Value]=@{Class=[int]$match.Groups[1].Value;Text=$match.Value.TrimEnd()}
    }
    return $blocks
}
$reportLines=[Collections.Generic.List[string]]::new()
foreach($circuit in 'Circuit_01','Circuit_02','Circuit_03') {
    $before=Read-SceneBlocks (Join-Path $Baseline "Assets/Scenes/$circuit.unity")
    $after=Read-SceneBlocks "Assets/Scenes/$circuit.unity"
    $protectedTypes=@(4,33,114,54,64,65,135,136,146)
    $protected=0
    foreach($id in $before.Keys) {
        if($before[$id].Class -eq 1) {
            if(!$after.ContainsKey($id)){throw "GameObject removed in ${circuit}: $id"}
            $oldObject=[regex]::Replace($before[$id].Text,'(?m)^  m_StaticEditorFlags:.*\n?','')
            $newObject=[regex]::Replace($after[$id].Text,'(?m)^  m_StaticEditorFlags:.*\n?','')
            if($oldObject -cne $newObject){throw "Unexpected GameObject/name/active-state change in ${circuit}: $id"}
            continue
        }
        if($protectedTypes -notcontains $before[$id].Class){continue}
        if(!$after.ContainsKey($id) -or $before[$id].Text -cne $after[$id].Text){throw "Protected component changed in ${circuit}: $id (class $($before[$id].Class))"}
        $protected++
    }
    $reportLines.Add("${circuit}: PASS; $protected original transforms/meshes/physics/gameplay/ProBuilder components byte-identical.")
}
$reportLines.Add('COMPLETE: PASS')
[IO.File]::WriteAllLines((Join-Path (Get-Location) 'Logs/intermediate-preservation.txt'),$reportLines)
$reportLines
