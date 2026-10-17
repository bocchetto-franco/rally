param([string]$Baseline='Logs/SceneBackups/Turbo_20261016_181541')
$ErrorActionPreference='Stop'
function Blocks([string]$Path) {
    $content=[IO.File]::ReadAllText($Path).Replace("`r`n","`n")
    $result=@{}
    foreach ($block in [regex]::Matches($content,'(?ms)^--- !u!(?<type>\d+) &(?<id>-?\d+).*?(?=^--- !u!|\z)')) {
        $result[$block.Groups['id'].Value]=@{Type=$block.Groups['type'].Value;Text=$block.Value}
    }
    return $result
}
foreach ($name in @('Circuit_01','Circuit_02','Circuit_03')) {
    $before=Blocks (Join-Path $Baseline ($name+'.unity'))
    $after=Blocks ('Assets/Scenes/'+$name+'.unity')
    foreach ($id in $before.Keys) {
        if (!$after.ContainsKey($id)) { throw "$name original component removed: $id" }
        if ($before[$id].Type -eq '1660057539') {
            $oldRoots=@([regex]::Matches($before[$id].Text,'fileID: (-?\d+)') | ForEach-Object {$_.Groups[1].Value})
            $newRoots=@([regex]::Matches($after[$id].Text,'fileID: (-?\d+)') | ForEach-Object {$_.Groups[1].Value})
            if ($newRoots.Count -ne $oldRoots.Count+1 -or (Compare-Object $oldRoots $newRoots | Where-Object SideIndicator -eq '<=')) { throw "$name original root list changed" }
        } elseif ($before[$id].Text -cne $after[$id].Text) { throw "$name original component changed: type=$($before[$id].Type), fileID=$id" }
    }
    $zones=@($after.Values | Where-Object { $_.Type -eq '114' -and $_.Text.Contains('guid: 8a752812d60d4b12ad1babc1590e0011') })
    if ($zones.Count -ne 3) { throw "$name does not have exactly three turbo zones" }
    foreach ($zone in $zones) {
        if (!$zone.Text.Contains('activationHalfWidth: 0.7') -or !$zone.Text.Contains('durationSeconds: 2.25')) { throw "$name unexpected serialized turbo parameters" }
    }
    "PASS: $name, $($before.Count) original serialized blocks preserved, only one new root and three new turbo zones."
}
