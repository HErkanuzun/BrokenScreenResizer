# Security Policy · Güvenlik Politikası

## Supported versions · Desteklenen sürümler

Only the [latest release](https://github.com/HErkanuzun/BrokenScreenResizer/releases/latest) receives fixes.
Düzeltmeler yalnızca [son sürüme](https://github.com/HErkanuzun/BrokenScreenResizer/releases/latest) gelir.

## Reporting a vulnerability · Güvenlik açığı bildirme

Please **do not** open a public issue for security problems. Report them privately on GitHub:
**Security → Report a vulnerability** ([link](https://github.com/HErkanuzun/BrokenScreenResizer/security/advisories/new)).
Include the version, your Windows version and the steps to reproduce. You will get an answer as soon as possible.

Güvenlik sorunları için lütfen herkese açık bir konu (issue) **açma**. GitHub'da gizli olarak bildir:
**Security → Report a vulnerability** ([bağlantı](https://github.com/HErkanuzun/BrokenScreenResizer/security/advisories/new)).
Sürümü, Windows sürümünü ve sorunu tekrarlama adımlarını ekle.

## What the app does on your computer · Program bilgisayarında ne yapar

| | |
|---|---|
| Network · Ağ | None. No telemetry, no update checks. · Yok. Veri toplamaz, güncelleme kontrolü yapmaz. |
| Administrator rights · Yönetici izni | Only to install or remove the virtual display driver. · Sadece sanal ekran sürücüsünü kurarken ve kaldırırken. |
| Input hooks · Giriş kancaları | A low-level **mouse** hook in software mode, to keep the pointer out of the broken area. No keyboard hook. · Yazılım modunda imleci bozuk alanın dışında tutmak için düşük seviyeli **fare** kancası. Klavye kancası yok. |
| Files and registry · Dosya ve kayıt defteri | `%AppData%\BrokenScreenResizer`, `HKCU\…\Run\BrokenScreenResizer` (if "Start with Windows" is on), `C:\ProgramData\BrokenScreenResizer\driver` (virtual monitor only) |
| Driver certificate · Sürücü sertifikası | A self-signed certificate restricted to code signing (EKU 1.3.6.1.5.5.7.3.3) is added to Trusted Root and Trusted Publishers so Windows accepts the driver. Its private key is non-exportable and never published. It is removed on uninstall. · Windows'un sürücüyü kabul etmesi için yalnızca kod imzalamaya izinli kendinden imzalı bir sertifika eklenir. Özel anahtarı dışa aktarılamaz ve yayımlanmaz. Kaldırınca silinir. |

Uninstalling (`--uninstall`, `Uninstall.bat` or Windows "Apps") reverts all of the above.
Kaldırma, yukarıdakilerin hepsini geri alır.

## Verifying releases · Sürümleri doğrulama

Release files are built from this repository by GitHub Actions. Each release includes `SHA256SUMS.txt`, and every file has a signed build provenance attestation:

```powershell
gh attestation verify .\BrokenScreenResizer-1.0-Setup.exe --repo HErkanuzun/BrokenScreenResizer
```
