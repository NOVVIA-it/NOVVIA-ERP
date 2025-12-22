Add-Type -AssemblyName 'Microsoft.Office.Interop.Excel'
$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$workbook = $excel.Workbooks.Open('C:\Users\PA\Downloads\Startbestellung zelhealth Dezember 2025 _.xlsx')
$sheet = $workbook.Sheets.Item(1)

Write-Host "=== EXCEL DATEI INHALT ==="
Write-Host "Sheet Name: $($sheet.Name)"
Write-Host ""

$usedRange = $sheet.UsedRange
$rowCount = $usedRange.Rows.Count
$colCount = $usedRange.Columns.Count
Write-Host "Zeilen: $rowCount, Spalten: $colCount"
Write-Host ""

Write-Host "=== SPALTEN (Zeile 1) ==="
for ($col = 1; $col -le $colCount; $col++) {
    $value = $sheet.Cells.Item(1, $col).Text
    if ($value) {
        Write-Host "  Spalte $col`: $value"
    }
}

Write-Host ""
Write-Host "=== ERSTE 5 DATENZEILEN ==="
for ($row = 2; $row -le [Math]::Min(6, $rowCount); $row++) {
    Write-Host "Zeile $row`:"
    for ($col = 1; $col -le [Math]::Min(10, $colCount); $col++) {
        $header = $sheet.Cells.Item(1, $col).Text
        $value = $sheet.Cells.Item($row, $col).Text
        if ($header -and $value) {
            Write-Host "  $header`: $value"
        }
    }
    Write-Host ""
}

$workbook.Close($false)
$excel.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
