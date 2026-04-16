# Script to generate ICO file from the design
Add-Type -AssemblyName System.Drawing

function Create-IconFile {
    param(
        [string]$OutputPath,
        [int[]]$Sizes = @(16, 32, 48, 256)
    )
    
    $allImages = @()
    $offsetData = 6 + ($Sizes.Count * 16)  # Header + directory entries
    $currentOffset = $offsetData
    
    # Create each size
    foreach ($size in $Sizes) {
        $bmp = New-Object System.Drawing.Bitmap($size, $size)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        
        # Blue gradient background
        $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(37, 99, 235))
        $g.FillEllipse($brush, 0, 0, $size, $size)
        
        # Draw AP text if size > 16
        if ($size -gt 16) {
            $fontSize = [math]::Max(4, [math]::Round($size * 0.35))
            $font = New-Object System.Drawing.Font('Arial', $fontSize, [System.Drawing.FontStyle]::Bold)
            $textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
            $textWidth = $g.MeasureString('AP', $font).Width
            $x = ($size - $textWidth) / 2
            $y = ($size - $font.Height) / 2
            $g.DrawString('AP', $font, $textBrush, $x, $y)
            $font.Dispose()
        }
        
        $g.Dispose()
        
        # Convert to 32-bit bitmap
        $bmp32 = New-Object System.Drawing.Bitmap($bmp)
        
        # Get bitmap data
        $ms = New-Object System.IO.MemoryStream
        $bmp32.Save($ms, [System.Drawing.Imaging.ImageFormat]::Bmp)
        $bitmapData = $ms.ToArray()
        $ms.Dispose()
        
        # Store for later
        $allImages += @{
            Size = $size
            Data = $bitmapData
            Offset = $currentOffset
        }
        
        $currentOffset += $bitmapData.Length
        
        $bmp32.Dispose()
        $bmp.Dispose()
    }
    
    # Create ICO file
    $fs = New-Object System.IO.FileStream($OutputPath, [System.IO.FileMode]::Create)
    $bw = New-Object System.IO.BinaryWriter($fs)
    
    # Write ICO header
    $bw.Write([uint16]0)           # Reserved
    $bw.Write([uint16]1)           # Type (1 = ICO)
    $bw.Write([uint16]$Sizes.Count) # Number of images
    
    # Write directory entries
    foreach ($img in $allImages) {
        $size = if ($img.Size -eq 256) { 0 } else { $img.Size }
        $bw.Write([byte]$size)               # Width (0 = 256)
        $bw.Write([byte]$size)               # Height (0 = 256)
        $bw.Write([byte]0)                   # Color palette (0 = none)
        $bw.Write([byte]0)                   # Reserved
        $bw.Write([uint16]1)                 # Color planes
        $bw.Write([uint16]32)                # Bits per pixel
        $bw.Write([uint32]$img.Data.Length)  # Image size
        $bw.Write([uint32]$img.Offset)       # Offset to image data
    }
    
    # Write image data
    foreach ($img in $allImages) {
        # Skip BMP header (first 40 bytes of DIB header)
        $bw.Write($img.Data, 40, $img.Data.Length - 40)
    }
    
    $bw.Close()
    $fs.Close()
}

# Generate the icon
$iconPath = Join-Path $PSScriptRoot "..\AllItems.Automation.Browser.App\Assets\Icons\AppIcon.ico"
Create-IconFile -OutputPath $iconPath -Sizes @(16, 32, 48, 256)
Write-Host "Icon file created at: $iconPath"
