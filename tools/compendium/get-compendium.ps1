# Downloads the Destiny Data Compendium (a view-only public Google Sheet) into a dated folder
# next to this script and zips it, ready to hand to a Loopsmith session: compendium-<date>\ and
# compendium-<date>.zip (both gitignored).
#
#   powershell -ExecutionPolicy Bypass -File .\get-compendium.ps1              # tabs only (small)
#   powershell -ExecutionPolicy Bypass -File .\get-compendium.ps1 -WithImages  # also the sheet's images
#
# Runs the C# download (Loopsmith.CompendiumDownload) from this clone: in Docker's .NET 10 SDK image
# (preferred), or with a local .NET 10 SDK. Keep the result private: the Compendium is one person's
# donation-supported work — don't publish the raw files.
param([switch]$WithImages)
$ErrorActionPreference = 'Stop'

$here  = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo  = Split-Path -Parent (Split-Path -Parent $here)
$sheet = 'https://docs.google.com/spreadsheets/d/1WaxvbLx7UoSZaBqdFr1u32F2uWVLo-CJunJB4nlGUE4/edit'
$image = 'mcr.microsoft.com/dotnet/sdk:10.0'
$name  = "compendium-$(Get-Date -Format 'yyyy-MM-dd')"
$out   = Join-Path $here $name
$zip   = "$out.zip"

$dumpArgs = @($sheet, "tools/compendium/$name", '--zip')
if (-not $WithImages) { $dumpArgs += '--no-images' }

Push-Location $repo
try {
    $dockerRunning = $false
    if (Get-Command docker -ErrorAction SilentlyContinue) {
        docker info *> $null
        $dockerRunning = ($LASTEXITCODE -eq 0)
        if (-not $dockerRunning) { Write-Host "Docker is installed but not running - using the local .NET SDK instead (or start Docker Desktop and re-run)." }
        else {
            docker image inspect $image *> $null
            if ($LASTEXITCODE -ne 0) { docker pull -q $image *> $null }
            if ($LASTEXITCODE -ne 0) {
                $dockerRunning = $false
                Write-Host "Docker can't download $image (network or proxy settings?) - using the local .NET SDK instead."
            }
        }
    }
    if ($dockerRunning) {
        Write-Host "Running the Compendium download in Docker ($image)..."
        # Builds into the container's /tmp, so the clone's bin\ and obj\ stay Windows'.
        docker run --rm -v "${repo}:/w" -w /w -e DOTNET_NOLOGO=1 -e DOTNET_CLI_TELEMETRY_OPTOUT=1 $image `
            dotnet run --project tools/compendium/Loopsmith.CompendiumDownload --artifacts-path /tmp/artifacts -- @dumpArgs
    }
    else {
        if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install Docker Desktop or the .NET 10 SDK first, then run this script again.' }
        Write-Host "Running the Compendium download with the local .NET SDK..."
        dotnet run --project (Join-Path $here 'Loopsmith.CompendiumDownload') -- @dumpArgs
    }
    if ($LASTEXITCODE -ne 0) { throw "The Compendium download failed (exit code $LASTEXITCODE). See $out\download.log" }
    if (-not (Test-Path (Join-Path $out 'tabs.json'))) { throw "No tabs.json in $out - the download did not complete." }

    Write-Host ""
    Write-Host "Done: $zip"
    Write-Host "Send that zip to the Loopsmith session (it stays out of git)."
}
finally {
    Pop-Location
}
