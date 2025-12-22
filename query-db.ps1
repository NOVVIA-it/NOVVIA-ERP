# Encoding: UTF-8
param([string]$SqlFile = "")

$server = "217.92.173.180,2107\S03NOVVIA"
$database = "Mandant_3"
$user = "sa"
$password = "Am_Lohm" + [char]0x00FC + "hlbach#13"

$connectionString = "Server=$server;Database=$database;User Id=$user;Password=$password;TrustServerCertificate=True;Encrypt=False;"

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $conn.Open()
    Write-Host "Connected to $database" -ForegroundColor Green

    if ($SqlFile -and (Test-Path $SqlFile)) {
        Write-Host "Executing $SqlFile..." -ForegroundColor Cyan
        $sql = Get-Content $SqlFile -Raw -Encoding UTF8

        # Split by GO statements (handling various formats)
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

                    # Extract PRINT messages from batch
                    if ($batch -match "PRINT\s+'([^']+)'") {
                        Write-Host "  $($matches[1])" -ForegroundColor Yellow
                    }
                }
                catch {
                    Write-Host "Fehler in Batch $batchNum : $($_.Exception.Message)" -ForegroundColor Red
                    # Show first 100 chars of problematic batch
                    $preview = if ($batch.Length -gt 100) { $batch.Substring(0, 100) + "..." } else { $batch }
                    Write-Host "  Batch: $preview" -ForegroundColor Gray
                }
            }
        }
        Write-Host "`n$batchNum Batches ausgefuehrt!" -ForegroundColor Green
    }
    else {
        Write-Host "Usage: .\query-db.ps1 -SqlFile 'path\to\script.sql'" -ForegroundColor Yellow
    }

    $conn.Close()
}
catch {
    Write-Host "Fehler: $($_.Exception.Message)" -ForegroundColor Red
}
