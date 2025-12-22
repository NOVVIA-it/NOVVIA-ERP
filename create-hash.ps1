$password = 'novvia'
$md5 = [System.Security.Cryptography.MD5]::Create()
$bytes = [System.Text.Encoding]::UTF8.GetBytes($password)
$hash = $md5.ComputeHash($bytes)
$md5Hash = [BitConverter]::ToString($hash).Replace('-','').ToLower()
Write-Host "MD5: $md5Hash"

$sha1 = [System.Security.Cryptography.SHA1]::Create()
$sha1Hash = [BitConverter]::ToString($sha1.ComputeHash($bytes)).Replace('-','').ToLower()
Write-Host "SHA1: $sha1Hash"
