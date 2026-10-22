param([string]$BaselineAfterFiveBlocks)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$report = [System.Collections.Generic.List[string]]::new()
function Assert-Check([bool]$passed, [string]$message) {
    if (-not $passed) { throw $message }
    $report.Add("PASS: $message")
}
foreach ($number in '01','02','03') {
    $path = "Assets/Scenes/Circuit_$number.unity"
    $before = (git show "HEAD:$path") -join "`n"
    $comparison = if ($BaselineAfterFiveBlocks) { Join-Path $BaselineAfterFiveBlocks "Circuit_$number.unity" } else { Join-Path $PWD $path }
    $after = [IO.File]::ReadAllText($comparison).Replace("`r`n", "`n").TrimEnd("`n")
    foreach ($pair in @(@('maximumSteerAngle: 44','maximumSteerAngle: 46'), @('steeringResponse: 60','steeringResponse: 66'), @('maxSteerAngle: 36','maxSteerAngle: 46'), @('steerSpeed: 60','steerSpeed: 66'))) {
        Assert-Check (([regex]::Matches($before, [regex]::Escape($pair[0]))).Count -eq 1) "$path unique player field $($pair[0])"
        $before = $before.Replace($pair[0], $pair[1])
    }
    if ($number -eq '03') {
        $pattern = '(?s)(--- !u!65 &1496176210\nBoxCollider:.*?  m_ProvidesContacts: 0\n  m_Enabled: )1'
        Assert-Check (([regex]::Matches($before, $pattern)).Count -eq 1) 'exactly one targeted old bridge boundary'
        $before = [regex]::Replace($before, $pattern, '${1}0')
    }
    Assert-Check ($before -ceq $after) "$path ONLY player steering fields and the specified bridge wall changed; all other scene data preserved"
}
$bot = [IO.File]::ReadAllText((Join-Path $PWD 'Assets/Scripts/RallyBotController.cs'))
$menu = [IO.File]::ReadAllText((Join-Path $PWD 'Assets/Scripts/RallyMenuController.cs'))
Assert-Check (-not ($menu -match 'RallyBotDifficulty\.(Medium|Hard)' -or $bot -match 'RallyBotDifficulty\.(Medium|Hard)')) 'no Medium/Hard runtime difficulty paths'
Assert-Check ($menu.Contains('string[] difficulties = { "FÁCIL" };')) 'one Easy menu option'
Assert-Check ($bot.Contains('easySpeedMultiplier = .7225f') -and $bot.Contains('playerContactBrakeSeconds = .75f')) 'Easy tuning and 0.75 second yielding are serialized'
$dust = [IO.File]::ReadAllText((Join-Path $PWD 'Assets/Scripts/RallyVehicleDust.cs'))
Assert-Check ($dust.Contains('void Update()') -and $dust.Contains('controller.frontLeftWheel.steerAngle + controller.frontRightWheel.steerAngle') -and $dust.Contains('emission.rateOverTime = new ParticleSystem.MinMaxCurve') -and $dust.Contains('main.startSize = new ParticleSystem.MinMaxCurve(particleSize')) 'dust reads individual live wheel angle and writes runtime emission/initial size'
$report.Add('Static preservation only. Runtime menu/contacts/bridge driving still require a responsive, unlocked Unity session.')
if ($BaselineAfterFiveBlocks) {
    & (Join-Path $PSScriptRoot 'VerifyTurboRoadReadability.ps1') -Baseline $BaselineAfterFiveBlocks | ForEach-Object { $report.Add($_) }
    $report.Add('Current scenes additionally contain the independently authorized turbo/road-material changes, verified separately above.')
}
$report.Add('COMPLETE: PASS (static scope checks only)')
[IO.Directory]::CreateDirectory((Join-Path $PWD 'Logs')) | Out-Null
[IO.File]::WriteAllLines((Join-Path $PWD 'Logs/five-blocks-preservation.txt'), $report)
$report
