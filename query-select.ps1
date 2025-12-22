# Encoding: UTF-8
param([string]$Query = "")

$server = "217.92.173.180,2107\S03NOVVIA"
$database = "Mandant_3"
$user = "sa"
$password = "Am_Lohm" + [char]0x00FC + "hlbach#13"

$connectionString = "Server=$server;Database=$database;User Id=$user;Password=$password;TrustServerCertificate=True;Encrypt=False;"

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $conn.Open()

    if ($Query) {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $Query
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
        $dataset = New-Object System.Data.DataSet
        $adapter.Fill($dataset) | Out-Null
        $dataset.Tables[0] | Format-Table -AutoSize
    }

    $conn.Close()
}
catch {
    Write-Host "Fehler: $($_.Exception.Message)" -ForegroundColor Red
}
