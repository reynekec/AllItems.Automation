# Script to generate PNG icon
Add-Type -AssemblyName System.Drawing

$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)

# Blue gradient background
$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(37, 99, 235))
$g.FillEllipse($brush, 0, 0, $size, $size)

# Draw AP text
$fontSize = 90
$font = New-Object System.Drawing.Font('Arial', $fontSize, [System.Drawing.FontStyle]::Bold)
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$stringFormat = New-Object System.Drawing.StringFormat
$stringFormat.Alignment = [System.Drawing.StringAlignment]::Center
$stringFormat.LineAlignment = [System.Drawing.StringAlignment]::Center
$g.DrawString('AP', $font, $textBrush, ($size/2), ($size/2), $stringFormat)

$g.Dispose()

# Save as PNG
$outputPath = '.\AllItems.Automation.Browser.App\Assets\Icons\AppIcon.png'
$bmp.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Host "PNG icon created successfully at: $outputPath"
