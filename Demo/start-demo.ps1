# start-demo.ps1
# Avvia l'API Sharp4AI e ngrok per la sessione demo

$ApiProject = Join-Path $PSScriptRoot "Sharp4AI.Demo.Api"
$ApiUrl     = "https://localhost:7100"
$SwaggerUrl = "$ApiUrl/swagger"
$NgrokUrl   = "https://muskrat-loved-openly.ngrok-free.app/api/similarity"

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Sharp4AI Demo - Avvio sessione" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# --- 1. Avvia API ---
Write-Host "[1/3] Avvio API dotnet..." -ForegroundColor Yellow
$apiProcess = Start-Process -FilePath "dotnet" `
    -ArgumentList "run --project `"$ApiProject`"" `
    -PassThru -WindowStyle Normal

Write-Host "      PID API: $($apiProcess.Id)" -ForegroundColor DarkGray

# --- 2. Attendi che la porta risponda ---
Write-Host "[2/3] Attendo che l'API sia pronta sulla porta 7100..." -ForegroundColor Yellow
$maxWait = 30
$waited  = 0
do {
    Start-Sleep -Seconds 2
    $waited += 2
    $ready = Test-NetConnection -ComputerName localhost -Port 7100 -WarningAction SilentlyContinue -InformationLevel Quiet
} while (-not $ready -and $waited -lt $maxWait)

if (-not $ready) {
    Write-Host "ATTENZIONE: API non risponde dopo $maxWait secondi. Continuo comunque." -ForegroundColor Red
} else {
    Write-Host "      API pronta dopo $waited secondi." -ForegroundColor Green
}

# --- 3. Avvia ngrok ---
Write-Host "[3/3] Avvio ngrok..." -ForegroundColor Yellow
$ngrokProcess = Start-Process -FilePath "ngrok" `
    -ArgumentList "start --all" `
    -PassThru -WindowStyle Normal

Start-Sleep -Seconds 3

# --- Riepilogo ---
Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Tutto avviato" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Swagger locale : $SwaggerUrl" -ForegroundColor White
Write-Host "  Endpoint ngrok : $NgrokUrl" -ForegroundColor White
Write-Host ""
Write-Host "  Premi INVIO per aprire Swagger nel browser..."
Read-Host | Out-Null
Start-Process $SwaggerUrl

Write-Host ""
Write-Host "  Per fermare tutto chiudi le finestre di dotnet e ngrok," -ForegroundColor DarkGray
Write-Host "  oppure premi CTRL+C in ciascuna." -ForegroundColor DarkGray
Write-Host ""
