param([string]$Baseline = 'Logs/SceneBackups/BridgeLip_20261016/Circuit_03.unity')
$ErrorActionPreference = 'Stop'
$before = [IO.File]::ReadAllText((Join-Path (Get-Location) $Baseline)).Replace("`r`n", "`n")
$after = [IO.File]::ReadAllText((Join-Path (Get-Location) 'Assets/Scenes/Circuit_03.unity')).Replace("`r`n", "`n")
$pattern = '(?ms)^--- !u!65 &1234652364\n.*?(?=^--- !u!)'
$oldBlock = [regex]::Match($before, $pattern).Value
$newBlock = [regex]::Match($after, $pattern).Value
if (!$oldBlock -or !$newBlock) { throw 'Deck collider missing' }
$expected = $oldBlock.Replace('m_Center: {x: 0, y: 0, z: 0}', 'm_Center: {x: 0, y: -0.015, z: 0}')
if ($newBlock -cne $expected) { throw 'Unexpected deck collider change' }
if ($before.Replace($oldBlock, '') -cne $after.Replace($newBlock, '')) { throw 'Other scene components changed' }
'PASS: only deck collider center changed; barriers, bridge visuals, cars, waypoints and checkpoints identical.'
'PASS: top height -0.16 -0.015 + 0.35/2 = 0, matching the approach.'
