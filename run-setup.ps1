# Setup NOVVIA tables for MSV3
$server = "217.92.173.180,2107\S03NOVVIA"
$database = "Mandant_3"
$user = "sa"
$password = "Am_Lohm" + [char]0x00FC + "hlbach#13"
$connectionString = "Server=$server;Database=$database;User Id=$user;Password=$password;TrustServerCertificate=True;Encrypt=False;"

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $conn.Open()
    Write-Host "Connected to $database" -ForegroundColor Green

    # Check existing NOVVIA tables
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' ORDER BY TABLE_NAME"
    $reader = $cmd.ExecuteReader()

    Write-Host "`nExisting NOVVIA tables:" -ForegroundColor Cyan
    $tables = @()
    while($reader.Read()) {
        $tableName = $reader["TABLE_NAME"]
        $tables += $tableName
        Write-Host "  - $tableName"
    }
    $reader.Close()

    # Check if MSV3Lieferant exists
    if ($tables -contains "MSV3Lieferant") {
        Write-Host "`nMSV3Lieferant already exists!" -ForegroundColor Green
    } else {
        Write-Host "`nMSV3Lieferant missing - creating base tables..." -ForegroundColor Yellow

        # Run setup script
        $sqlFile = "C:\NovviaERP\src\NovviaERP\Scripts\Setup-Einkauf-NOVVIA.sql"
        $sql = Get-Content $sqlFile -Raw -Encoding UTF8
        $batches = $sql -split '(?m)^\s*GO\s*$'

        $batchNum = 0
        foreach ($batch in $batches) {
            $batch = $batch.Trim()
            if ($batch -and $batch.Length -gt 5) {
                $batchNum++
                try {
                    $cmd = $conn.CreateCommand()
                    $cmd.CommandText = $batch
                    $cmd.CommandTimeout = 120
                    $result = $cmd.ExecuteNonQuery()
                    if ($batch -match "PRINT\s+'([^']+)'") {
                        Write-Host "  $($matches[1])" -ForegroundColor Yellow
                    }
                }
                catch {
                    Write-Host "Error in batch $batchNum : $($_.Exception.Message)" -ForegroundColor Red
                }
            }
        }
        Write-Host "$batchNum batches executed" -ForegroundColor Green
    }

    $conn.Close()
}
catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
