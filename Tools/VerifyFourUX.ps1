param([Parameter(Mandatory=$true)][string]$Baseline)
$ErrorActionPreference='Stop'
function Blocks([string]$Path) {
    $content=[IO.File]::ReadAllText((Resolve-Path -LiteralPath $Path)).Replace("`r`n","`n")
    $result=@{}
    foreach($m in [regex]::Matches($content,'(?ms)^--- !u!(\d+) &(-?\d+)[^\n]*\n.*?(?=^--- !u!|\z)')) {
        $result[$m.Groups[2].Value]=@{Class=[int]$m.Groups[1].Value;Text=$m.Value.TrimEnd()}
    }
    return $result
}
function Value([string]$Content,[string]$Pattern) {return [regex]::Match($Content,$Pattern).Groups[1].Value}
$report=[Collections.Generic.List[string]]::new()
foreach($circuit in 'Circuit_01','Circuit_02','Circuit_03') {
    $before=Blocks (Join-Path $Baseline "$circuit.unity")
    $after=Blocks "Assets/Scenes/$circuit.unity"
    $pineIds=[Collections.Generic.HashSet[string]]::new()
    $roots=0
    if($circuit -eq 'Circuit_02') {
        foreach($id in $before.Keys) {
            if($before[$id].Class -eq 1 -and $before[$id].Text -match '(?m)^  m_Name: Isolated mountain pine ') {$pineIds.Add($id)|Out-Null;$roots++}
        }
        if($roots -ne 12){throw "Expected 12 pine roots: $roots"}
        do {
            $changed=$false
            foreach($id in $before.Keys) {
                if($pineIds.Contains($id)){continue}
                $text=$before[$id].Text
                if($before[$id].Class -ne 1) {
                    $go=Value $text 'm_GameObject: \{fileID: (-?\d+)\}'
                    $father=Value $text 'm_Father: \{fileID: (-?\d+)\}'
                    if($pineIds.Contains($go) -or $pineIds.Contains($father)) {$pineIds.Add($id)|Out-Null;$changed=$true}
                } else {
                    $components=[regex]::Matches($text,'component: \{fileID: (-?\d+)\}')
                    if(@($components|Where-Object {$pineIds.Contains($_.Groups[1].Value)}).Count -gt 0){$pineIds.Add($id)|Out-Null;$changed=$true}
                }
            }
        }while($changed)
    }
    $count=0
    foreach($id in $before.Keys) {
        if($pineIds.Contains($id)){if($after.ContainsKey($id)){throw "Pine component still present $id"};continue}
        if(!$after.ContainsKey($id)){throw "Non-pine object removed: ${circuit}/$id"}
        if(@(114,54,64,65,135,136,146,33) -contains $before[$id].Class) {
            if($before[$id].Text -cne $after[$id].Text){throw "Protected component changed: ${circuit}/$id (class $($before[$id].Class))"}
            $count++
        }
        if($before[$id].Class -eq 4 -and $before[$id].Text -cne $after[$id].Text) {
            # Only the parent listing the twelve removed pine children may change.
            $old=$before[$id].Text
            foreach($removed in $pineIds) {$old=[regex]::Replace($old,"(?m)^  - \{fileID: $removed\}\n?",'')}
            if($old -cne $after[$id].Text){throw "Original transform changed: ${circuit}/$id"}
        }
    }
    $report.Add("${circuit}: PASS; $count physics/gameplay/mesh components unchanged; original transforms preserved except removed-pine parent list; pines removed=$roots")
}
$report.Add('COMPLETE: PASS')
[IO.File]::WriteAllLines((Join-Path (Get-Location) 'Logs/four-ux-preservation.txt'),$report)
$report
