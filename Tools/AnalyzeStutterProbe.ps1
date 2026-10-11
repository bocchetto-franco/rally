param([Parameter(Mandatory=$true)][string[]]$Csv)
$ErrorActionPreference='Stop'
function Percentile($values, [double]$p) {
    $sorted=@($values | Sort-Object)
    if (!$sorted.Count) { return $null }
    return $sorted[[math]::Min($sorted.Count-1,[math]::Floor(($sorted.Count-1)*$p))]
}
foreach ($path in $Csv) {
    $rows=@(Import-Csv -LiteralPath $path)
    if (!$rows.Count) { throw "Empty capture: $path" }
    $meta=Get-Content -LiteralPath ($path+'.meta.txt') -Raw
    Write-Output ([IO.Path]::GetFileName($path))
    Write-Output $meta
    Write-Output ("Capture ends at {0}s; first/last frames {1}/{2}. Check this against requested duration; a full buffer is not full-lap evidence." -f $rows[-1].seconds,$rows[0].frame,$rows[-1].frame)
    foreach ($phase in @('False','True')) {
        $set=@($rows | Where-Object race_ready -eq $phase)
        if (!$set.Count) { continue }
        $times=@($set | ForEach-Object {[double]$_.wall_ms})
        $shader=@($set | Where-Object {[long]$_.previous_shader_ns -gt 0})
        $collections=@($set | Where-Object {[int]$_.gc_collections -gt 0})
        [pscustomobject]@{Phase=if($phase -eq 'True'){'Race'}else{'Load/initialization'};Frames=$set.Count;
            P50ms=[math]::Round((Percentile $times .5),2);P95ms=[math]::Round((Percentile $times .95),2);P99ms=[math]::Round((Percentile $times .99),2);
            MaxMs=($times|Measure-Object -Maximum).Maximum;Over50ms=@($times|Where-Object {$_ -gt 50}).Count;
            Over100ms=@($times|Where-Object {$_ -gt 100}).Count;Unfocused=@($set|Where-Object focused -eq 'False').Count;
            ShaderSamples=$shader.Count;GCCollections=$collections.Count;MaxCheckpoint=($set|ForEach-Object {[int]$_.checkpoint}|Measure-Object -Maximum).Maximum;
            MaxSpeed=($set|ForEach-Object {[double]$_.speed_kph}|Measure-Object -Maximum).Maximum}
        if ($rows[0].PSObject.Properties.Name -contains 'previous_batches') {
            Write-Output ("Batch counter positive samples: {0}/{1}; a valid all-zero counter is not usable for draw-call claims." -f @($set|Where-Object {[long]$_.previous_batches -gt 0}).Count,$set.Count)
        }
        if ($rows[0].PSObject.Properties.Name -contains 'previous_present_ns') {
            Write-Output ("Present positive samples: {0}/{1}; verify alongside focus, visible window and framebuffer evidence." -f @($set|Where-Object {[long]$_.previous_present_ns -gt 0}).Count,$set.Count)
        }
    }
    Write-Output 'Largest frames (recorders refer to the previous frame; correlation is not proof of causality):'
    $rows|Sort-Object {[double]$_.wall_ms} -Descending|Select-Object -First 15 seconds,wall_ms,previous_main_ns,previous_render_ns,previous_gc_bytes,previous_shader_ns,previous_upload_ns,gc_collections,checkpoint,speed_kph,focused,race_ready|Format-Table -AutoSize
    $detailColumns=@($rows[0].PSObject.Properties.Name | Where-Object {$_ -like 'previous_*_ns' -and $_ -notin @('previous_main_ns','previous_render_ns','previous_shader_ns','previous_upload_ns')})
    if ($detailColumns.Count) {
        Write-Output 'Largest race frames: top recorded regions in ms (nested/overlapping; do not sum):'
        foreach ($row in @($rows|Where-Object race_ready -eq 'True'|Sort-Object {[double]$_.wall_ms} -Descending|Select-Object -First 12)) {
            $regions=@(foreach ($column in $detailColumns) { if ([long]$row.$column -gt 0) { [pscustomobject]@{Name=$column;Ms=[math]::Round([long]$row.$column/1000000.0,3)} } })
            $largest=($regions|Sort-Object Ms -Descending|Select-Object -First 5|ForEach-Object {"$($_.Name)=$($_.Ms)"}) -join '; '
            Write-Output ("t={0}s wall={1}ms cp={2} focused={3}: {4}" -f $row.seconds,$row.wall_ms,$row.checkpoint,$row.focused,$largest)
        }
    }
}
