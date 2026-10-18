# Broken Screen Resizer - sürüm paketleri
# build.bat'ten sonra çalışır; dist\ klasörüne şunları üretir:
#   BrokenScreenResizer-<sürüm>-Setup.exe     kurulum paketi (önerilen; varsa günceller, yoksa kurar)
#   BrokenScreenResizer-<sürüm>-Portable.zip  kurulumsuz kullanım: program, kaldırma betiği, sürücü paketi, lisans
#   SHA256SUMS.txt                            dosyaların SHA-256 özetleri (indirilen dosyayı doğrulamak için)
# Kullanım: powershell -ExecutionPolicy Bypass -File scripts\package.ps1 -Version 1.01 [-Iscc <ISCC.exe yolu>]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Iscc = ''
)
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$bin = Join-Path $root 'bin'
$dist = Join-Path $root 'dist'
$portable = "BrokenScreenResizer-$Version-Portable"
$stage = Join-Path $dist 'BrokenScreenResizer'

foreach ($f in 'BrokenScreenResizer.exe', 'Uninstall.bat') {
    if (-not (Test-Path (Join-Path $bin $f))) { throw "bin\$f yok; önce build.bat çalıştır." }
}

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $stage 'driver') | Out-Null

# ------------------------------------------------------------------ taşınabilir ZIP
Copy-Item (Join-Path $bin 'BrokenScreenResizer.exe') $stage
Copy-Item (Join-Path $bin 'Uninstall.bat') $stage
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $stage 'LICENSE.txt')
Copy-Item (Join-Path $root 'packaging\README.txt') $stage

# Sürücü paketi exe'nin içinde de var; klasör incelemek ve elle kurmak isteyenler için
$pkg = Join-Path $root 'driver\package'
foreach ($f in 'BrokenScreenResizerVDD.inf', 'BrokenScreenResizerVDD.dll', 'BrokenScreenResizerVDD.cat', 'BrokenScreenResizerVDD.cer') {
    $src = Join-Path $pkg $f
    if (-not (Test-Path $src)) { throw "Sürücü dosyası yok: driver\package\$f" }
    Copy-Item $src (Join-Path $stage "driver\$f")
}
Compress-Archive -Path $stage -DestinationPath (Join-Path $dist "$portable.zip") -CompressionLevel Optimal
Remove-Item $stage -Recurse -Force

# ------------------------------------------------------------------ kurulum paketi
if ($Iscc -eq '') {
    $c = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($c) { $Iscc = $c.Source }
    else {
        foreach ($p in "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") {
            if (Test-Path $p) { $Iscc = $p; break }
        }
    }
}
if ($Iscc -eq '' -or -not (Test-Path $Iscc)) { throw 'Inno Setup 6 (ISCC.exe) bulunamadı; -Iscc ile yolunu ver.' }
& $Iscc /Q "/DAppVersion=$Version" (Join-Path $root 'installer\BrokenScreenResizer.iss')
if ($LASTEXITCODE -ne 0) { throw "Kurulum paketi derlenemedi ($LASTEXITCODE)." }

# ------------------------------------------------------------------ özetler
$sums = foreach ($f in Get-ChildItem $dist -File | Sort-Object Name) {
    "$((Get-FileHash $f.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $($f.Name)"
}
[IO.File]::WriteAllLines((Join-Path $dist 'SHA256SUMS.txt'), [string[]]$sums)

Write-Host "Paketler hazır (dist\):"
$sums | ForEach-Object { Write-Host "  $_" }
