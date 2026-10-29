param([string]$Baseline = 'Logs/FinalAudit/BeforeMeshFlags')
$ErrorActionPreference = 'Stop'
$rallyProject = Split-Path $PSScriptRoot -Parent
$rallyBaseline = if ([IO.Path]::IsPathRooted($Baseline)) { $Baseline } else { Join-Path $rallyProject $Baseline }
$targets = [ordered]@{
    Circuit_01 = @('714919144', '1981460150')
    Circuit_02 = @('302953956', '1502178921')
    Circuit_03 = @('640217566', '2013749343')
}
$encoding = [Text.UTF8Encoding]::new($false, $true)
$hash = [Security.Cryptography.SHA256]::Create()
try {
    foreach ($scene in $targets.Keys) {
        $beforeBytes = [IO.File]::ReadAllBytes((Join-Path $rallyBaseline "$scene.unity"))
        $afterBytes = [IO.File]::ReadAllBytes((Join-Path $rallyProject "Assets/Scenes/$scene.unity"))
        # GetString keeps a UTF-8 BOM, if present; GetBytes therefore preserves it too.
        $expected = $encoding.GetString($beforeBytes)
        foreach ($id in $targets[$scene]) {
            $pattern = '(?ms)^--- !u!43 &' + [regex]::Escape($id) + '[^\r\n]*\r?\n.*?(?=^--- !u!|\z)'
            $matches = [regex]::Matches($expected, $pattern)
            if ($matches.Count -ne 1) { throw "$scene mesh $id is missing or duplicated" }
            $mesh = $matches[0].Value
            $flag = '(?m)^  m_PreBakeTriangleCollisionMesh: 0(?=\r?$)'
            if ([regex]::Matches($mesh, $flag).Count -ne 1) { throw "$scene mesh $id does not have exactly one old triangle-bake flag" }
            $newMesh = [regex]::Replace($mesh, $flag, '  m_PreBakeTriangleCollisionMesh: 1')
            $expected = $expected.Substring(0, $matches[0].Index) + $newMesh + $expected.Substring($matches[0].Index + $mesh.Length)
        }
        $expectedBytes = $encoding.GetBytes($expected)
        $expectedHash = [BitConverter]::ToString($hash.ComputeHash($expectedBytes))
        $actualHash = [BitConverter]::ToString($hash.ComputeHash($afterBytes))
        if ($expectedBytes.Length -ne $afterBytes.Length -or $expectedHash -cne $actualHash) {
            throw "$scene has changes beyond its two explicitly allowed triangle-prebake flags"
        }
        "PASS: $scene, only Mesh triangle-prebake flags $($targets[$scene] -join ', ') changed 0 -> 1; all other bytes preserved."
    }
    'COMPLETE: PASS (six mesh metadata flags only; no geometry, collider, vehicle or gameplay changes)'
}
finally { $hash.Dispose() }
