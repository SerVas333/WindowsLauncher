param(
    [Parameter(Mandatory=$true)]
    [string]$Password
)

Add-Type -AssemblyName System.Security

# Используем ту же соль что и в коде (32 байта нулей)
$saltBytes = New-Object byte[] 32
$salt = [Convert]::ToBase64String($saltBytes)

# Генерируем хэш по тому же алгоритму что в коде
$pbkdf2 = New-Object System.Security.Cryptography.Rfc2898DeriveBytes($Password, $saltBytes, 100000, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
$hashBytes = $pbkdf2.GetBytes(32)
$passwordHash = [Convert]::ToBase64String($hashBytes)

Write-Host "Password: $Password"
Write-Host "Salt: $salt"
Write-Host "Hash: $passwordHash"
Write-Host ""
Write-Host "Update your appsettings.json or auth-config.json with:"
Write-Host "`"passwordHash`": `"$passwordHash`","
Write-Host "`"salt`": `"$salt`","

$pbkdf2.Dispose()