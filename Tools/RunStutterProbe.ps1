param([ValidateSet('Baseline','After','Release','Diagnostic')][string]$Build='Baseline', [ValidateSet('Circuit_01','Circuit_02','Circuit_03')][string]$Circuit='Circuit_03', [ValidateSet(1,2)][int]$Players=1, [int]$Seconds=90, [ValidatePattern('^[A-Za-z0-9_-]*$')][string]$Label='', [switch]$SkipPreparation, [switch]$Visible, [switch]$ReferencePorsche, [switch]$Timeline)
$ErrorActionPreference='Stop'
$probeRoot=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $probeRoot ("Build/Stutter_${Build}_20261010/Rally1.exe")
if (!(Test-Path -LiteralPath $exe)) { throw "Missing completed build: $exe" }
if ($Timeline -and $Build -eq 'Release') { throw 'CPU Timeline requires the existing Development build; Release counters are not enough.' }
if (Get-Process Rally1 -ErrorAction SilentlyContinue) { throw 'Close the existing game before measuring; never terminate a user-owned process.' }
$outDir=Join-Path $probeRoot 'Logs/Stutter'
[IO.Directory]::CreateDirectory($outDir)|Out-Null
$name="${Build}_${Circuit}_${Players}P"
if ($Label) { $name += "_$Label" }
$csv=Join-Path $outDir ($name+'.csv')
if (Test-Path -LiteralPath $csv) { throw "Capture exists; use a preserved/renamed previous output: $csv" }
if ($Timeline -and (Test-Path -LiteralPath ($csv+'.raw'))) { throw 'Preserve the previous Timeline capture; use a new label.' }
$log=Join-Path $outDir ($name+'.log')
# Preserve only the exact game's PlayerPrefs, not Editor/system preferences.
$registryName='Software\DefaultCompany\Rally1'
$original=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($registryName)
$snapshot=@()
$existed=$null -ne $original
if ($original) {
    foreach ($key in $original.GetValueNames()) { $snapshot+=@{Name=$key;Value=$original.GetValue($key);Kind=$original.GetValueKind($key)} }
    $original.Close()
}
$snapshot|Export-Clixml -LiteralPath (Join-Path $outDir ($name+'-prefs-backup.xml'))
try {
    if ($ReferencePorsche) {
        # Control the visual model used by the diagnostic driver, not the user's
        # selection. Resolve Unity's existing hashed keys rather than inventing
        # names; all values are restored byte-for-byte in finally.
        $vehicleEntries=@($snapshot | Where-Object { $_.Name -match '^Rally\.SelectedVehicle(Two)?_h[0-9]+$' })
        if ($vehicleEntries.Count -ne 2 -or @($vehicleEntries | Where-Object Kind -ne 'Binary').Count) {
            throw 'ReferencePorsche requires the two existing Unity binary vehicle preferences.'
        }
        $referenceBytes=[Text.Encoding]::UTF8.GetBytes('Porsche 911 SC Rally'+[char]0)
        $temporary=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($registryName,$true)
        try { foreach ($entry in $vehicleEntries) { $temporary.SetValue($entry.Name,$referenceBytes,[Microsoft.Win32.RegistryValueKind]::Binary) } }
        finally { $temporary.Close() }
    }
    $arguments=@('-rally-frame-probe','-rally-probe-circuit',$Circuit,'-rally-probe-players',"$Players",'-rally-probe-seconds',"$Seconds",'-rally-probe-output',$csv,'-logFile',$log,'-screen-width','1280','-screen-height','720','-screen-fullscreen','0')
    if ($Timeline) {
        # Opt-in native Profiler recording, not shipping gameplay logging. Bounded
        # to 6000 frames; its disk/profiling overhead excludes FPS comparisons.
        $arguments+=@('-profiler-enable','-profiler-log-file',($csv+'.raw'),'-profiler-capture-frame-count','6000','-profiler-maxusedmemory','67108864')
    }
    if ($Build -eq 'Baseline' -or $SkipPreparation) { $arguments+='-rally-no-load-prep' }
    # Visible is an interactive game render test, not a background helper. Hidden
    # remains the default for non-render smoke checks; never treat their FPS as valid.
    $style=if ($Visible) {'Normal'} else {'Hidden'}
    $process=Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $probeRoot -WindowStyle $style -PassThru
    Write-Output "Probe started: PID=$($process.Id), $name, output=$csv"
    $probeDeadline=[DateTime]::UtcNow.AddSeconds($Seconds+180)
    while (!$process.HasExited -and [DateTime]::UtcNow -lt $probeDeadline) { Start-Sleep -Seconds 2; $process.Refresh() }
    if (!$process.HasExited) { Stop-Process -Id $process.Id; throw 'Diagnostic process timed out; only this task-created process was terminated.' }
    if (!(Test-Path -LiteralPath $csv)) { throw "Probe produced no capture; inspect $log" }
    Write-Output "CAPTURE COMPLETE: $csv; exit=$($process.ExitCode)"
} finally {
    $restore=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($registryName)
    foreach ($key in $restore.GetValueNames()) {
        if ($snapshot.Name -notcontains $key) { $restore.DeleteValue($key,$false) }
    }
    foreach ($entry in $snapshot) { $restore.SetValue($entry.Name,$entry.Value,$entry.Kind) }
    $restore.Close()
    # Leave an empty exact game key if it did not previously exist; never recursively remove registry trees.
    Write-Output "PlayerPrefs restored from $name-prefs-backup.xml (key existed=$existed)."
}
