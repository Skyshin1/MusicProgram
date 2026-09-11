param([string]$ReportPath)
$ErrorActionPreference = 'Stop'
if (-not $ReportPath) {
    $ReportPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'Logs/DeepSeaDemo/Waterline-Play.txt'
}
$samples = @{}
foreach ($line in Get-Content -LiteralPath $ReportPath) {
    if ($line -match '^(Up|Down)(\d+) offset=\S+ luma=(\S+) camera=\([^,]+, ([^,]+),[^)]+\) surface=(\S+)') {
        $samples[$Matches[1] + $Matches[2]] = @{
            Luma = [double]::Parse($Matches[3], [cultureinfo]::InvariantCulture)
            EyeY = [double]::Parse($Matches[4], [cultureinfo]::InvariantCulture)
            SurfaceY = [double]::Parse($Matches[5], [cultureinfo]::InvariantCulture)
        }
    }
}
if ($samples.Count -ne 14) { throw 'Expected 14 captured crossing samples.' }
for ($i = 0; $i -lt 7; $i++) {
    if ($samples["Down$i"].Luma -gt .3) { throw "Unfiltered underwater target at Down$i" }
    $up = $samples["Up$i"]
    # Allow the waterline's two-centimetre feather, plus rounded pose logging.
    if ($up.EyeY -lt $up.SurfaceY - .025 -and $up.Luma -gt .3) {
        throw "Above-water target leaked through the underside at Up$i"
    }
}
if ($samples['Up0'].Luma -lt .8) { throw 'Above-water view was incorrectly obscured.' }
if ($samples['Up5'].Luma -gt .3 -or $samples['Up6'].Luma -gt .3) {
    throw 'Submerged upward view remains too transparent.'
}
'PASS: 14 rendered samples; no clear downward crossing frame, submerged upward detail obscured, above-water view preserved.'
