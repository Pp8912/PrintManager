# uninstall-printer.ps1
# Desinstala la impresora virtual "PrintManager Color"
# EJECUTAR COMO ADMINISTRADOR

#Requires -RunAsAdministrator

$PrinterName = "PrintManager Color"
$SpoolFolder = "C:\PrintManagerSpool"
$SpoolFile = "$SpoolFolder\output.prn"

Write-Host "=== Desinstalacion de Impresora Virtual PrintManager ===" -ForegroundColor Cyan

# 1. Eliminar impresora
$printer = Get-Printer -Name $PrinterName -ErrorAction SilentlyContinue
if ($printer) {
    Remove-Printer -Name $PrinterName
    Write-Host "[OK] Impresora '$PrinterName' eliminada." -ForegroundColor Green
}
else {
    Write-Host "[--] Impresora '$PrinterName' no encontrada." -ForegroundColor Yellow
}

# 2. Eliminar puerto local del registry
$portsRegPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Ports"
$portExists = (Get-ItemProperty -Path $portsRegPath -Name $SpoolFile -ErrorAction SilentlyContinue) -ne $null
if ($portExists) {
    Remove-ItemProperty -Path $portsRegPath -Name $SpoolFile -Force
    Restart-Service Spooler -Force
    Write-Host "[OK] Puerto local eliminado." -ForegroundColor Green
}
else {
    Write-Host "[--] Puerto local no encontrado." -ForegroundColor Yellow
}

# 3. Eliminar puerto TCP/IP si existe (de instalacion anterior)
$badPort = Get-PrinterPort -Name "PrintManagerPort" -ErrorAction SilentlyContinue
if ($badPort) {
    Remove-PrinterPort -Name "PrintManagerPort" -ErrorAction SilentlyContinue
    Write-Host "[OK] Puerto TCP/IP incorrecto eliminado." -ForegroundColor Green
}

# 4. Limpiar carpeta spool
if (Test-Path $SpoolFolder) {
    Remove-Item -Path $SpoolFolder -Recurse -Force
    Write-Host "[OK] Carpeta spool eliminada: $SpoolFolder" -ForegroundColor Green
}
else {
    Write-Host "[--] Carpeta spool no encontrada." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Desinstalacion Completada ===" -ForegroundColor Cyan
