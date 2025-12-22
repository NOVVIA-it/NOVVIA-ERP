param([string]$Password)

Write-Host "Testing password: $Password"
Write-Host ""

# UTF8 Encoding
$utf8 = [System.Text.Encoding]::UTF8

# MD5
$md5 = [System.Security.Cryptography.MD5]::Create()
$md5Hash = [BitConverter]::ToString($md5.ComputeHash($utf8.GetBytes($Password))).Replace("-","")
Write-Host "MD5 (UTF8):     $md5Hash"

# SHA1
$sha1 = [System.Security.Cryptography.SHA1]::Create()
$sha1Hash = [BitConverter]::ToString($sha1.ComputeHash($utf8.GetBytes($Password))).Replace("-","")
Write-Host "SHA1 (UTF8):    $sha1Hash"

# SHA256
$sha256 = [System.Security.Cryptography.SHA256]::Create()
$sha256Hash = [BitConverter]::ToString($sha256.ComputeHash($utf8.GetBytes($Password))).Replace("-","")
Write-Host "SHA256 (UTF8):  $sha256Hash"

Write-Host ""
Write-Host "Target hash:    EC4D24617E79F3C023A58B3555CEA7A1C5B15785"

# Try with username prefix/suffix
$sha1WithUser = [BitConverter]::ToString($sha1.ComputeHash($utf8.GetBytes("ap" + $Password))).Replace("-","")
Write-Host "SHA1 (ap+pw):   $sha1WithUser"

$sha1WithUser2 = [BitConverter]::ToString($sha1.ComputeHash($utf8.GetBytes($Password + "ap"))).Replace("-","")
Write-Host "SHA1 (pw+ap):   $sha1WithUser2"

# Unicode/UTF16
$utf16 = [System.Text.Encoding]::Unicode
$sha1Utf16 = [BitConverter]::ToString($sha1.ComputeHash($utf16.GetBytes($Password))).Replace("-","")
Write-Host "SHA1 (UTF16):   $sha1Utf16"
