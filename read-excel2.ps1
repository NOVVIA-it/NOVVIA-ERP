Add-Type -AssemblyName 'Microsoft.Office.Interop.Excel'
$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$workbook = $excel.Workbooks.Open('C:\Users\PA\Downloads\Startbestellung zelhealth Dezember 2025 _.xlsx')
$sheet = $workbook.Sheets.Item(1)

$usedRange = $sheet.UsedRange
$rowCount = $usedRange.Rows.Count
$colCount = $usedRange.Columns.Count

Write-Host "=== RAW DATA (erste 15 Zeilen, alle Spalten) ==="
Write-Host ""

for ($row = 1; $row -le [Math]::Min(15, $rowCount); $row++) {
    $rowData = @()
    for ($col = 1; $col -le $colCount; $col++) {
        $value = $sheet.Cells.Item($row, $col).Text
        $rowData += $value
    }
    $line = $rowData -join " | "
    Write-Host "Zeile $row`: $line"
}

Write-Host ""
Write-Host "=== STATISTIK ==="
Write-Host "Gesamtzeilen: $rowCount"
Write-Host "Gesamtspalten: $colCount"

$workbook.Close($false)
$excel.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
