$ErrorActionPreference = 'Stop'
$rallyProject = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
Set-Location -LiteralPath $rallyProject
$rallyCli = 'C:\Users\franc\AppData\Local\Unity\bin\unity.exe'
$rallyLogs = Join-Path $rallyProject 'Logs\Gameplay_20261028'
$rallyReport = Join-Path $rallyLogs 'finalization.txt'
function Wait-RallyReport([string]$Path, [scriptblock]$Ready, [int]$Seconds) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            $content = [IO.File]::ReadAllText($Path)
            if ($content -match '(?m)^FAIL(?:ED)?:') { throw "Failed report: $Path" }
            if (& $Ready $content) { return }
        }
        Start-Sleep -Seconds 3
    }
    throw "Timed out waiting for $Path"
}
function Invoke-RallyMenu([string]$Menu) {
    $response = & $rallyCli command menu --project-path $rallyProject --path $Menu --detach --json
    if ($LASTEXITCODE -ne 0) { throw "Unity rejected $Menu : $response" }
    $result = ($response -join "`n") | ConvertFrom-Json
    if (!$result.success) { throw "Unity did not queue $Menu" }
    Add-Content -LiteralPath $rallyReport -Value ("QUEUED: " + $Menu + ' ' + $result.data.jobId)
}
try {
    [IO.File]::WriteAllText($rallyReport, "Final gameplay verification and Windows build`nStarted: $([DateTimeOffset]::Now.ToString('O'))`n")
    Wait-RallyReport (Join-Path $rallyLogs 'play.txt') {
        param($s)
        ($s -match 'COMPLETE: PASS' -and $s -match 'RESTORED:' -and
         ([regex]::Matches($s,'CASE COMPLETE').Count -eq 6) -and
         ([regex]::Matches($s,'turbo HUD belongs to its own viewport').Count -eq 9))
    } 1200
    Add-Content -LiteralPath $rallyReport -Value 'PASS: all six Play cases, both viewports, preferences restored'
    $bakeReport = Join-Path $rallyLogs 'occlusion.txt'
    if (Test-Path -LiteralPath $bakeReport) { throw 'Occlusion report already exists; inspect before restarting this orchestration.' }
    Invoke-RallyMenu 'Tools/Rally/Rebake October Circuits'
    Wait-RallyReport $bakeReport { param($s) $s -match 'COMPLETE: PASS' -and ([regex]::Matches($s,'PASS: Circuit_0').Count -eq 3) } 1800
    Add-Content -LiteralPath $rallyReport -Value 'PASS: occlusion regenerated in all three scenes'
    # Allow the bake callback to finish restoring its original scene setup.
    Start-Sleep -Seconds 5
    & python Tools/VerifyOctoberPreservation.py
    if ($LASTEXITCODE -ne 0) { throw 'Preservation comparison failed' }
    Add-Content -LiteralPath $rallyReport -Value 'PASS: vehicle physics, routes, puddles and boost effect preserved'
    $buildRoot = [IO.Path]::GetFullPath((Join-Path $rallyProject 'Build')) + [IO.Path]::DirectorySeparatorChar
    $current = [IO.Path]::GetFullPath((Join-Path $buildRoot 'Windows'))
    $previous = [IO.Path]::GetFullPath((Join-Path $buildRoot 'Windows_Previous_20261028_Gameplay'))
    if (!$current.StartsWith($buildRoot,[StringComparison]::OrdinalIgnoreCase) -or !$previous.StartsWith($buildRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe build paths' }
    if (Test-Path -LiteralPath $previous) { throw 'Previous build backup already exists; inspect it before replacing anything.' }
    foreach ($game in @(Get-Process Rally -ErrorAction SilentlyContinue)) {
        if ($game.Path -and $game.Path.StartsWith($current+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Rally is running; current build was not moved.' }
    }
    if (Test-Path -LiteralPath $current) {
        Move-Item -LiteralPath $current -Destination $previous
        Add-Content -LiteralPath $rallyReport -Value ("BACKUP: " + $previous)
    }
    Invoke-RallyMenu 'Build/Windows Release'
    $buildLog = Join-Path $current 'BuildLog.txt'
    Wait-RallyReport $buildLog { param($s) $s -match 'Result: Succeeded' -and $s -match '(?m)^Errors: 0\s*$' -and $s -match '(?m)^Bytes: ' } 1800
    Copy-Item -LiteralPath $buildLog -Destination (Join-Path $rallyLogs 'BuildLog.txt')
    $executable = Join-Path $current 'Rally.exe'
    if (!(Test-Path -LiteralPath $executable)) { throw 'Executable missing' }
    Add-Content -LiteralPath $rallyReport -Value ("EXECUTABLE: " + $executable)
    Add-Content -LiteralPath $rallyReport -Value ("SHA256: " + (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash)
    Add-Content -LiteralPath $rallyReport -Value "COMPLETE: PASS`nFinished: $([DateTimeOffset]::Now.ToString('O'))"
} catch {
    Add-Content -LiteralPath $rallyReport -Value ("FAIL: " + $_.Exception.Message)
    throw
}
