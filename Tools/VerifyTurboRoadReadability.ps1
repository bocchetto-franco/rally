param([Parameter(Mandatory=$true)][string]$Baseline)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
function Read-Blocks([string]$Path) {
    $blocks = @{}
    foreach ($match in [regex]::Matches([IO.File]::ReadAllText($Path).Replace("`r`n","`n"), '(?ms)^--- !u!(?<type>\d+) &(?<id>-?\d+).*?(?=^--- !u!|\z)')) {
        $blocks[$match.Groups['id'].Value] = @{ Type=$match.Groups['type'].Value; Text=$match.Value }
    }
    return $blocks
}
foreach ($scene in 'Circuit_01','Circuit_02','Circuit_03') {
    $before = Read-Blocks (Join-Path $Baseline "$scene.unity")
    $after = Read-Blocks "Assets/Scenes/$scene.unity"
    if ($before.Count -ne $after.Count) { throw "$scene serialized objects added/removed" }
    $zoneObjects = @{}
    foreach ($block in $before.Values) {
        if ($block.Type -eq '114' -and $block.Text.Contains('guid: 8a752812d60d4b12ad1babc1590e0011')) {
            $zoneObjects[[regex]::Match($block.Text,'m_GameObject: \{fileID: (-?\d+)\}').Groups[1].Value] = $true
        }
    }
    if ($zoneObjects.Count -ne 3) { throw "$scene expected three zones" }
    $roadObject = $null
    $roadName = if ($scene -eq 'Circuit_01') { 'Rally_Road_Start_to_Finish' } else { 'Circuit02_Road' }
    if ($scene -ne 'Circuit_03') {
        $roadObject = @($before.Keys | Where-Object { $before[$_].Type -eq '1' -and $before[$_].Text -match "(?m)^  m_Name: $roadName`$" })
        if ($roadObject.Count -ne 1) { throw "$scene ambiguous road object" }
        $roadObject = $roadObject[0]
    }
    $changed = 0
    foreach ($id in $before.Keys) {
        if (-not $after.ContainsKey($id)) { throw "$scene missing $id" }
        $a = $before[$id].Text; $b = $after[$id].Text
        if ($a -ceq $b) { continue }
        $changed++
        $type = $before[$id].Type
        $owner = [regex]::Match($a,'m_GameObject: \{fileID: (-?\d+)\}').Groups[1].Value
        if (($type -eq '1' -and $zoneObjects.ContainsKey($id)) -or $zoneObjects.ContainsKey($owner)) {
            switch ($type) {
                '1' { $a = $a.Replace('centre lane 1.4m','centre lane 4.4m') }
                '4' {
                    $pattern = '(?m)^  m_Local(Rotation|Position|EulerAnglesHint): .*\n'
                    $a = [regex]::Replace($a,$pattern,''); $b = [regex]::Replace($b,$pattern,'')
                }
                '65' { $a = $a.Replace('m_Size: {x: 2.2, y: 3, z: 8}','m_Size: {x: 5.2, y: 3, z: 10}') }
                '114' { $a = $a.Replace('activationHalfWidth: 0.7','activationHalfWidth: 2.2') }
                default { throw "$scene forbidden zone component change $id type $type" }
            }
        } elseif ($type -eq '23' -and $owner -eq $roadObject) {
            $pattern = '(?m)^  - \{fileID: 2100000, guid: [a-f0-9]+, type: 2\}\n'
            $a = [regex]::Replace($a,$pattern,''); $b = [regex]::Replace($b,$pattern,'')
        }
        if ($a -cne $b) { throw "$scene unexpected change $id type $type" }
    }
    $expected = if ($scene -eq 'Circuit_03') {12} else {13}
    if ($changed -ne $expected) { throw "$scene expected $expected changed blocks, got $changed" }
    "PASS: $scene, $($before.Count) blocks; ONLY three zone poses/names/hitboxes/widths and the allowed road material changed."
}
"COMPLETE: PASS (saved YAML preservation, not a driven lap)"
