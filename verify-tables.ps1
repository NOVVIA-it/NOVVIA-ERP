# Encoding: UTF-8
$server = "217.92.173.180,2107\S03NOVVIA"
$database = "Mandant_3"
$user = "sa"
$password = "Am_Lohm" + [char]0x00FC + "hlbach#13"

$connectionString = "Server=$server;Database=$database;User Id=$user;Password=$password;TrustServerCertificate=True;Encrypt=False;"

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $conn.Open()

    Write-Host "NOVVIA Tabellen:" -ForegroundColor Cyan
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' ORDER BY TABLE_NAME"
    $reader = $cmd.ExecuteReader()
    while($reader.Read()) { Write-Host "  $($reader['TABLE_NAME'])" -ForegroundColor Green }
    $reader.Close()

    Write-Host "`nNOVVIA Stored Procedures:" -ForegroundColor Cyan
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT name FROM sys.procedures WHERE name LIKE 'spNOVVIA%' ORDER BY name"
    $reader = $cmd.ExecuteReader()
    while($reader.Read()) { Write-Host "  $($reader['name'])" -ForegroundColor Green }
    $reader.Close()

    $conn.Close()
}
catch {
    Write-Host "Fehler: $($_.Exception.Message)" -ForegroundColor Red
}
