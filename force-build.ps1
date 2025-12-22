# Force delete and rebuild
$exePath = "C:\NovviaERP\src\NovviaERP\NovviaERP.WPF\bin\Debug\net8.0-windows\NovviaERP.WPF.exe"

# Try to delete
if (Test-Path $exePath) {
    try {
        Remove-Item $exePath -Force -ErrorAction Stop
        Write-Host "EXE deleted"
    } catch {
        Write-Host "Cannot delete EXE: $_"

        # Try rename
        $newName = $exePath + ".old"
        try {
            Rename-Item $exePath $newName -Force -ErrorAction Stop
            Write-Host "EXE renamed to .old"
        } catch {
            Write-Host "Cannot rename either: $_"
            exit 1
        }
    }
}

# Build
Set-Location "C:\NovviaERP\src\NovviaERP\NovviaERP.WPF"
dotnet build 2>&1 | Select-Object -Last 5
