# Downloads the Destiny Data Compendium (a view-only public Google Sheet) into a dated folder
# next to this script and zips it, ready to hand to a Loopsmith session: compendium-<date>\ and
# compendium-<date>.zip (both gitignored).
#
#   powershell -ExecutionPolicy Bypass -File .\get-compendium.ps1              # tabs only (small)
#   powershell -ExecutionPolicy Bypass -File .\get-compendium.ps1 -WithImages  # also the sheet's images
#
# Needs Docker Desktop (preferred) or Python 3.10+. Keep the result private: the Compendium is one
# person's donation-supported work — don't publish the raw files.
param([switch]$WithImages)
$ErrorActionPreference = 'Stop'

$here  = Split-Path -Parent $MyInvocation.MyCommand.Path
$sheet = 'https://docs.google.com/spreadsheets/d/1WaxvbLx7UoSZaBqdFr1u32F2uWVLo-CJunJB4nlGUE4/edit'
$name  = "compendium-$(Get-Date -Format 'yyyy-MM-dd')"
$out   = Join-Path $here $name
$zip   = "$out.zip"

$dumpArgs = @($sheet, $name)
if (-not $WithImages) { $dumpArgs += '--no-images' }

Push-Location $here
try {
    $dockerRunning = $false
    if (Get-Command docker -ErrorAction SilentlyContinue) {
        docker info *> $null
        $dockerRunning = ($LASTEXITCODE -eq 0)
        if (-not $dockerRunning) { Write-Host "Docker is installed but not running - using Python instead (or start Docker Desktop and re-run)." }
        else {
            docker image inspect python:3.12-slim *> $null
            if ($LASTEXITCODE -ne 0) { docker pull -q python:3.12-slim *> $null }
            if ($LASTEXITCODE -ne 0) {
                $dockerRunning = $false
                Write-Host "Docker can't download python:3.12-slim (network or proxy settings?) - using Python instead."
            }
        }
    }
    if ($dockerRunning) {
        Write-Host "Running sheet_dump.py in Docker (python:3.12-slim)..."
        docker run --rm -v "${here}:/w" -w /w -e PIP_DISABLE_PIP_VERSION_CHECK=1 python:3.12-slim `
            sh -c "pip install -q --no-warn-script-location requests beautifulsoup4 && python sheet_dump.py $($dumpArgs -join ' ')"
    }
    else {
        $python = @('py', 'python', 'python3') | Where-Object { Get-Command $_ -ErrorAction SilentlyContinue } | Select-Object -First 1
        if (-not $python) { throw 'Install Docker Desktop or Python 3.10+ (python.org) first, then run this script again.' }
        Write-Host "Running sheet_dump.py with $python..."
        & $python -m pip install -q --disable-pip-version-check requests beautifulsoup4
        & $python (Join-Path $here 'sheet_dump.py') @dumpArgs
    }
    if ($LASTEXITCODE -ne 0) { throw "sheet_dump.py failed (exit code $LASTEXITCODE). See $out\sheet_dump.log" }
    if (-not (Test-Path (Join-Path $out 'tabs.json'))) { throw "No tabs.json in $out - the download did not complete." }

    Compress-Archive -Path $out -DestinationPath $zip -Force
    Write-Host ""
    Write-Host "Done: $zip"
    Write-Host "Send that zip to the Loopsmith session (it stays out of git)."
}
finally {
    Pop-Location
}
