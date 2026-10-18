# Ekran Kurtar sanal ekran sürücüsü - katalog oluşturma ve imzalama.
# build_driver.bat tarafından çağrılır.
#
# Windows, sürücü paketlerinin güvenilir bir sertifikayla imzalanmasını ister. Burada geliştiricinin
# bilgisayarında kendinden imzalı bir sertifika oluşturulur (bir kez). Ekran Kurtar sürücüyü kurarken
# bu sertifikanın açık kısmını (.cer) "Güvenilen Kök" ve "Güvenilen Yayımcılar" depolarına ekler.
# Özel anahtar hiçbir zaman pakete girmez.
param(
    [Parameter(Mandatory = $true)][string]$Package,
    [Parameter(Mandatory = $true)][string]$WdkRoot,
    [Parameter(Mandatory = $true)][string]$WdkVer
)
$ErrorActionPreference = 'Stop'

$Subject = 'CN=Ekran Kurtar Driver Signing'
$dll = Join-Path $Package 'EkranKurtarVDD.dll'
$inf = Join-Path $Package 'EkranKurtarVDD.inf'
$cat = Join-Path $Package 'EkranKurtarVDD.cat'
$cer = Join-Path $Package 'EkranKurtarVDD.cer'

function Find-Tool([string]$name) {
    $c = Get-Command $name -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }
    $sdk = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $hit = Get-ChildItem $sdk -Recurse -Filter $name -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
    if ($hit) { return $hit.FullName }
    throw "$name bulunamadı (Windows SDK kurulu mu?)"
}

$signtool = Find-Tool 'signtool.exe'

# ------------------------------------------------------------------ Sertifika
$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $Subject -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
    Sort-Object NotAfter -Descending | Select-Object -First 1
if (-not $cert) {
    Write-Host 'İmzalama sertifikası oluşturuluyor (bir kez)...'
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $Subject `
        -CertStoreLocation Cert:\CurrentUser\My -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 `
        -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(10)
}
Export-Certificate -Cert $cert -FilePath $cer -Type CERT | Out-Null
Write-Host "Sertifika: $($cert.Thumbprint)"

function Sign([string]$file) {
    # Önce zaman damgalı dene (internet gerekir); olmazsa damgasız imzala
    & $signtool sign /q /fd sha256 /sha1 $cert.Thumbprint /s My /tr http://timestamp.digicert.com /td sha256 $file 2>$null
    if ($LASTEXITCODE -ne 0) {
        & $signtool sign /q /fd sha256 /sha1 $cert.Thumbprint /s My $file
        if ($LASTEXITCODE -ne 0) { throw "İmzalanamadı: $file" }
    }
}

Sign $dll

# ------------------------------------------------------------------ Katalog (.cat)
$inf2cat = Join-Path $WdkRoot "bin\$WdkVer\x86\Inf2Cat.exe"
$made = $false
if (Test-Path $inf2cat) {
    & $inf2cat /driver:$Package /os:10_X64,10_19H1_X64,10_VB_X64,10_CO_X64,10_NI_X64,10_GE_X64 /uselocaltime | Out-Host
    $made = ($LASTEXITCODE -eq 0) -and (Test-Path $cat)
}
if (-not $made) {
    # Yedek yol: Windows SDK'daki makecat
    Write-Host 'Inf2Cat kullanılamadı, makecat ile katalog oluşturuluyor...'
    $makecat = Find-Tool 'makecat.exe'
    $cdf = Join-Path $Package 'EkranKurtarVDD.cdf'
    @"
[CatalogHeader]
Name=EkranKurtarVDD.cat
ResultDir=$Package
PublicVersion=0x0000001
EncodingType=0x00010001
CATATTR1=0x10010001:OSAttr:2:10.0

[CatalogFiles]
<hash>EkranKurtarVDD.inf=$inf
<hash>EkranKurtarVDD.dll=$dll
"@ | Set-Content -Encoding ASCII $cdf
    & $makecat -v $cdf | Out-Host
    Remove-Item $cdf -ErrorAction SilentlyContinue
    if (-not (Test-Path $cat)) { throw 'Katalog oluşturulamadı.' }
}

Sign $cat
Write-Host 'İmzalama tamam.'
exit 0
