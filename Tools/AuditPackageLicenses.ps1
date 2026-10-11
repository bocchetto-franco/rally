$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$records=foreach($folder in Get-ChildItem (Join-Path $projectRoot 'Library/PackageCache') -Directory) {
    $metadata=Join-Path $folder.FullName 'package.json'
    if (!(Test-Path $metadata)) { continue }
    $info=Get-Content $metadata -Raw | ConvertFrom-Json
    $license=Get-ChildItem $folder.FullName -File | Where-Object { $_.Name -match '^LICENSE(\.(md|txt))?$' } | Select-Object -First 1
    $notices=Get-ChildItem $folder.FullName -File | Where-Object { $_.Name -match '(?i)third[ -]?party.*notice' -and $_.Extension -ne '.meta' }
    $text=if($license) { Get-Content $license.FullName -Raw } else { '' }
    $terms=if($text -match 'Unity Companion Package License') {'Unity Companion Package License v1.0'} elseif($text -match 'Unity Companion License') {'Unity Companion License'} elseif($text -match 'Unity Package Distribution License') {'Unity Package Distribution License'} elseif($text -match 'MIT License') {'MIT'} elseif($info.name -like 'com.unity.modules.*') {'Unity Editor software terms (built-in module)'} elseif($info.type -eq 'shim') {'Unity Editor shim - no separate license notice locally; verify applicable version terms'} else {'NO LOCAL LICENSE - review required'}
    [pscustomobject]@{Name=$info.name;Version=$info.version;Author=if($info.author.name) {$info.author.name} else {'Unity / copyright parties in notice'};License=$terms;Source='https://docs.unity3d.com/Packages/'+$info.name+'@'+$info.version;LocalLicense=if($license) {$license.FullName.Substring($projectRoot.Length+1).Replace('\','/')} else {''};ThirdPartyNotices=($notices.Name -join '; ')}
}
$records | Sort-Object Name | Export-Csv (Join-Path $projectRoot 'Docs/PackageLicenseInventory.csv') -NoTypeInformation -Encoding utf8
$records | Group-Object License | Select-Object Name,Count | Format-Table
Write-Output ('COMPLETE: PASS - ' + @($records).Count + ' installed package records; missing notices explicitly marked.')
