if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process powershell "-NoExit -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

Set-Location $PSScriptRoot

$pluginDir  = "E:\ProgramData\Jellyfin\Server\plugins\BulkDownload"
$projectDir = "Jellyfin.Plugin.BulkDownload"
$dll        = "$projectDir\bin\Release\net9.0\Jellyfin.Plugin.BulkDownload.dll"

# Read version from csproj
$csproj  = Get-Content "$projectDir\Jellyfin.Plugin.BulkDownload.csproj" -Raw
$version = [regex]::Match($csproj, '<Version>([^<]+)</Version>').Groups[1].Value

Write-Host "Building v$version..."
dotnet build $projectDir -c Release
if (-not $?) { Write-Error "Build failed"; exit 1 }

Write-Host "Stopping Jellyfin..."
Stop-Service -Name "JellyfinServer" -ErrorAction SilentlyContinue
$stopped = (Get-Service -Name "JellyfinServer" -ErrorAction SilentlyContinue |
    Where-Object { $_.Status -eq "Stopped" }) -ne $null
if (-not $stopped) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt 30) {
        Start-Sleep -Seconds 3
        $svc = Get-Service -Name "JellyfinServer" -ErrorAction SilentlyContinue
        if ($svc -and $svc.Status -eq "Stopped") { $stopped = $true; break }
    }
}
if (-not $stopped) { Write-Error "Jellyfin did not stop in time"; exit 1 }

New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null

Copy-Item $dll -Destination $pluginDir -Force

Write-Host "Deployed v$version to $pluginDir"

Write-Host "Starting Jellyfin..."
Start-Service -Name "JellyfinServer" -ErrorAction SilentlyContinue
$started = $false
$sw = [System.Diagnostics.Stopwatch]::StartNew()
while ($sw.Elapsed.TotalSeconds -lt 30) {
    Start-Sleep -Seconds 3
    $svc = Get-Service -Name "JellyfinServer" -ErrorAction SilentlyContinue
    if ($svc -and $svc.Status -eq "Running") { $started = $true; break }
}
if (-not $started) { Write-Warning "Jellyfin may not have started yet - check manually." }
Write-Host "Done."
Read-Host "Press Enter to close"
