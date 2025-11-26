# PowerShell script to capture SQL Server bulk copy packets
# Run as Administrator

Write-Host "SQL Server Bulk Copy Packet Capture Script" -ForegroundColor Green
Write-Host "============================================`n" -ForegroundColor Green

# Check if running as admin
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    pause
    exit 1
}

# Check for Wireshark
$wireshark = Get-Command tshark -ErrorAction SilentlyContinue
if ($wireshark) {
    Write-Host "Found Wireshark/tshark at: $($wireshark.Source)" -ForegroundColor Green
    $captureMethod = "wireshark"
} else {
    Write-Host "Wireshark not found, will use netsh trace (built-in)" -ForegroundColor Yellow
    $captureMethod = "netsh"
}

Write-Host "`nCapture method: $captureMethod`n" -ForegroundColor Cyan

# Build the project first
Write-Host "Building .NET project..." -ForegroundColor Cyan
dotnet build
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    pause
    exit 1
}

Write-Host "`nStarting packet capture..." -ForegroundColor Green

if ($captureMethod -eq "wireshark") {
    # Using tshark
    $captureFile = "capture.pcap"
    Write-Host "Capture file: $captureFile" -ForegroundColor Cyan
    
    # Start tshark in background
    $tsharkProcess = Start-Process -FilePath "tshark" -ArgumentList "-i", "1", "-w", $captureFile, "-f", "tcp port 14333" -PassThru -NoNewWindow
    
    Write-Host "Capture started (PID: $($tsharkProcess.Id))" -ForegroundColor Green
    Start-Sleep -Seconds 2
    
    Write-Host "`nRunning bulk copy test..." -ForegroundColor Cyan
    dotnet run
    
    Write-Host "`nStopping capture..." -ForegroundColor Yellow
    Stop-Process -Id $tsharkProcess.Id -Force
    
    Write-Host "`nCapture saved to: $captureFile" -ForegroundColor Green
    Write-Host "You can open this file in Wireshark for analysis" -ForegroundColor Cyan
    
} else {
    # Using netsh
    $captureFile = "capture.etl"
    Write-Host "Capture file: $captureFile" -ForegroundColor Cyan
    Write-Host "Note: ETL files need Microsoft Message Analyzer or etl2pcapng to convert" -ForegroundColor Yellow
    
    netsh trace start capture=yes tracefile=$captureFile overwrite=yes
    
    Write-Host "`nRunning bulk copy test..." -ForegroundColor Cyan
    dotnet run
    
    Write-Host "`nStopping capture..." -ForegroundColor Yellow
    netsh trace stop
    
    Write-Host "`nCapture saved to: $captureFile" -ForegroundColor Green
    Write-Host "Convert to pcap using: etl2pcapng $captureFile output.pcapng" -ForegroundColor Cyan
}

Write-Host "`n============================================" -ForegroundColor Green
Write-Host "Capture complete!" -ForegroundColor Green
Write-Host "============================================`n" -ForegroundColor Green

pause
