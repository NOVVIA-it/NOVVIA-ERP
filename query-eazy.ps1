param([string]$Query)
$password = "Am_Lohm" + [char]0x00FC + "hlbach#13"
$connStr = "Server=217.92.173.180,2107\S03NOVVIA;Database=eazybusiness;User Id=sa;Password=$password;TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = $Query
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
$dataset = New-Object System.Data.DataSet
$adapter.Fill($dataset) | Out-Null
$dataset.Tables[0] | Format-Table -AutoSize
$conn.Close()
