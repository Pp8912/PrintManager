# install-printer.ps1
# Instala la impresora virtual "PrintManager Color" en Windows
# EJECUTAR COMO ADMINISTRADOR

#Requires -RunAsAdministrator

$PrinterName = "PrintManager Color"
$SpoolFolder = "C:\PrintManagerSpool"
$SpoolFile = "$SpoolFolder\output.prn"
$DriverName = "Microsoft PS Class Driver"

Write-Host "=== Instalacion de Impresora Virtual PrintManager ===" -ForegroundColor Cyan

# 1. Crear carpeta spool
if (!(Test-Path $SpoolFolder)) {
    New-Item -ItemType Directory -Path $SpoolFolder -Force | Out-Null
    Write-Host "[OK] Carpeta spool creada: $SpoolFolder" -ForegroundColor Green
}
else {
    Write-Host "[OK] Carpeta spool ya existe: $SpoolFolder" -ForegroundColor Yellow
}

# 2. Verificar que el driver PS esta disponible
$driver = Get-PrinterDriver -Name $DriverName -ErrorAction SilentlyContinue
if (-not $driver) {
    Write-Host "[INFO] Instalando driver '$DriverName'..." -ForegroundColor Yellow
    try {
        Add-PrinterDriver -Name $DriverName
        Write-Host "[OK] Driver instalado." -ForegroundColor Green
    }
    catch {
        Write-Host "[ERROR] No se pudo instalar el driver '$DriverName'." -ForegroundColor Red
        Write-Host "Intente: Add-PrinterDriver -Name 'Microsoft PS Class Driver'" -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "[OK] Driver '$DriverName' disponible." -ForegroundColor Green
}

# 3. Eliminar impresora y puerto existentes si hay
$existing = Get-Printer -Name $PrinterName -ErrorAction SilentlyContinue
if ($existing) {
    Remove-Printer -Name $PrinterName
    Write-Host "[OK] Impresora anterior eliminada." -ForegroundColor Yellow
}

# Limpiar puerto TCP/IP mal creado si existe
$badPort = Get-PrinterPort -Name "PrintManagerPort" -ErrorAction SilentlyContinue
if ($badPort) {
    Remove-PrinterPort -Name "PrintManagerPort" -ErrorAction SilentlyContinue
    Write-Host "[OK] Puerto TCP/IP incorrecto eliminado." -ForegroundColor Yellow
}

# 4. Crear puerto LOCAL registrando en el registry de Windows.
# Un puerto local con nombre de archivo hace que el spooler escriba directamente al archivo.
$portsRegPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Ports"
$portExists = (Get-ItemProperty -Path $portsRegPath -Name $SpoolFile -ErrorAction SilentlyContinue) -ne $null

if (-not $portExists) {
    New-ItemProperty -Path $portsRegPath -Name $SpoolFile -Value "" -PropertyType String -Force | Out-Null
    # Reiniciar el servicio de impresion para que detecte el nuevo puerto
    Restart-Service Spooler -Force
    Start-Sleep -Seconds 2
    Write-Host "[OK] Puerto local creado: $SpoolFile" -ForegroundColor Green
}
else {
    Write-Host "[OK] Puerto local ya existe: $SpoolFile" -ForegroundColor Yellow
}

# 5. Crear la impresora virtual con el puerto local
try {
    Add-Printer -Name $PrinterName -DriverName $DriverName -PortName $SpoolFile
    Write-Host "[OK] Impresora '$PrinterName' instalada." -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] No se pudo crear la impresora: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Instalacion Completada ===" -ForegroundColor Cyan
Write-Host "Impresora: $PrinterName" -ForegroundColor White
Write-Host "Archivo spool: $SpoolFile" -ForegroundColor White
Write-Host ""
Write-Host "Ejecute PrintManager.exe para que vigile la carpeta spool." -ForegroundColor Yellow
Write-Host "Cuando imprima a '$PrinterName', PrintManager procesara el documento." -ForegroundColor Yellow
