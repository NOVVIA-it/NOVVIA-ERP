$path = "C:\NovviaERP\src\NovviaERP\NovviaERP.WPF"
$files = Get-ChildItem -Path $path -Filter "*.xaml" -Recurse | Where-Object { $_.Name -ne "App.xaml" }

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8

    # Replace hardcoded #E86B5C with DynamicResource PrimaryBrush
    $newContent = $content -replace 'Background="#E86B5C"', 'Background="{DynamicResource PrimaryBrush}"'
    $newContent = $newContent -replace 'Foreground="#E86B5C"', 'Foreground="{DynamicResource PrimaryBrush}"'
    $newContent = $newContent -replace 'BorderBrush="#E86B5C"', 'BorderBrush="{DynamicResource PrimaryBrush}"'
    $newContent = $newContent -replace 'Fill="#E86B5C"', 'Fill="{DynamicResource PrimaryBrush}"'
    $newContent = $newContent -replace 'Stroke="#E86B5C"', 'Stroke="{DynamicResource PrimaryBrush}"'

    if ($content -ne $newContent) {
        Set-Content -Path $file.FullName -Value $newContent -NoNewline -Encoding UTF8
        Write-Host "Updated: $($file.Name)"
    }
}

Write-Host "Done."
