param([switch]$VerifySources)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$creditsPath = Join-Path $projectRoot 'CREDITS.md'
$catalog = @()
foreach ($section in ([regex]::Split([IO.File]::ReadAllText($creditsPath), '(?m)^### ' ) | Select-Object -Skip 1)) {
    $assetName = ($section -split '\r?\n')[0]
    $fields = @{}
    foreach ($key in @('Autor','Fuente','Licencia','Rutas')) {
        $match = [regex]::Match($section, '(?m)^- ' + $key + ': (.+)$')
        $fields[$key] = $match.Groups[1].Value.Trim()
    }
    foreach ($path in [regex]::Matches($fields.Rutas, '`([^`]+)`')) {
        $catalog += [pscustomobject]@{Path=$path.Groups[1].Value; Asset=$assetName; Author=$fields.Autor; Source=$fields.Fuente; License=$fields.Licencia}
    }
}
$catalog = $catalog | Sort-Object { $_.Path.Length } -Descending
$extensions = @('.fbx','.blend','.obj','.dae','.glb','.gltf','.png','.jpg','.jpeg','.tga','.psd','.hdr','.exr','.wav','.ogg','.mp3','.flac','.ttf','.otf','.shader','.shadergraph','.shadersubgraph','.cginc','.hlsl','.asset','.mat','.terrainlayer')
$inventory = foreach ($file in Get-ChildItem (Join-Path $projectRoot 'Assets') -File -Recurse) {
    if ($file.Extension.ToLowerInvariant() -notin $extensions) { continue }
    $path = $file.FullName.Substring($projectRoot.Length + 1).Replace('\','/')
    $entry = $catalog | Where-Object { $path -eq $_.Path -or $path.StartsWith($_.Path + '/', [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
    $status = if (!$entry) { 'UNMAPPED' } elseif ($entry.License -match 'NO CLARA') { 'UNCLEAR' } elseif ($entry.Asset -eq 'Geometría, terreno, UI y derivados de Rally') { 'LOCAL_DERIVATIVE' } else { 'DOCUMENTED' }
    # Generated TMP atlases inherit their source font; do not blanket-license them as Unity.
    if ($path -match 'TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/(Anton|Bangers|Oswald|Roboto|Unity|Electronic Highway Sign)') {
        $fontName=$Matches[1]
        $entry=$catalog | Where-Object { $_.Path -like "*/Fonts/$fontName*" } | Select-Object -First 1
        $status=if ($fontName -eq 'Electronic Highway Sign') { 'UNCLEAR' } else { 'DOCUMENTED' }
    }
    [pscustomobject]@{Path=$path;Asset=$entry.Asset;Author=$entry.Author;Source=$entry.Source;License=$entry.License;Status=$status}
}
$docs = Join-Path $projectRoot 'Docs'
[IO.Directory]::CreateDirectory($docs) | Out-Null
$inventory | Sort-Object Path | Export-Csv (Join-Path $docs 'AssetLicenseInventory.csv') -NoTypeInformation -Encoding utf8
$inventory | Group-Object Status | Select-Object Name,Count | Format-Table
$unmapped = @($inventory | Where-Object Status -eq 'UNMAPPED')
if ($unmapped.Count) { $unmapped | Select-Object -ExpandProperty Path; throw 'Media/native assets missing from CREDITS.md scope.' }
if ($VerifySources) {
    $evidence = [ordered]@{checked='2026-10-10';polyhaven=@();sketchfab=@();jrs=@()}
    foreach ($id in @('dry_ground_01','gravelly_sand','rock_boulder_dry','wild_rooibos_bush','grass_medium_01','namaqualand_rocks_01','quiver_tree_01','searsia_lucida','forest_floor','mud_forest','mossy_rock','kloofendal_overcast','rocky_terrain','aerial_grass_rock','rock_face_03','rocky_trail_02','muddy_tracks')) {
        $info=Invoke-RestMethod ('https://api.polyhaven.com/info/' + $id)
        $evidence.polyhaven += @{id=$id;name=$info.name;authors=$info.authors;url='https://polyhaven.com/a/'+$id;licensePolicy='https://polyhaven.com/license'}
    }
    foreach ($id in @('ac3c7013434e403e8faff87948caf422','402697365eaa41a68a543b71d0e0bfa8','85614131e0dc4613a948472aaa935fc7')) {
        $model=Invoke-RestMethod ('https://api.sketchfab.com/v3/models/' + $id)
        $evidence.sketchfab += @{id=$id;name=$model.name;author=$model.user.displayName;license=$model.license;url=$model.viewerUrl}
    }
    $upstream=Invoke-RestMethod 'https://api.github.com/repos/Jermesa-Studio/JRS_Vehicle_Physics_Controller/git/trees/main?recursive=1'
    $evidence.jrsCommitTree=$upstream.sha
    Push-Location $projectRoot
    try {
        foreach ($record in $inventory | Where-Object { $_.Path.StartsWith('Assets/JS Vehicle Physics Controller/') -and [IO.Path]::GetExtension($_.Path) -notin @('.asset','.mat') }) {
            $entry=$upstream.tree | Where-Object path -eq $record.Path | Select-Object -First 1
            $blob=git hash-object -- $record.Path
            $evidence.jrs += @{path=$record.Path;localBlob=$blob;upstreamBlob=$entry.sha;matches=($blob -eq $entry.sha)}
        }
    } finally { Pop-Location }
    $evidence | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $docs 'CreditsEvidence.json') -Encoding utf8
}
Write-Output ('COMPLETE: PASS - ' + $inventory.Count + ' files inventoried; unclear entries are not authorized by this report.')
