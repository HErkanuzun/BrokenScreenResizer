# Ekran Kurtar

**Kırık veya ölü pikselli monitörünü çöpe atma.** Ekran Kurtar, monitörünün bozuk kısmını Windows'tan gizler; Windows monitörü sanki sadece sağlam kısımdan ibaretmiş gibi kullanır.

![Ekran Kurtar](docs/screenshot.png)

*English below · 中文 · Русский arayüz dilleri de mevcut.*

---

## Ne yapar?

Monitörünün sol, sağ, üst veya alt kenarındaki bozuk bölgeyi seçersin. Ekran Kurtar o bölgeyi Windows'un ekran ayarlarından çıkarır:

- **Tam ekran = sağlam alan.** Oyunlar, videolar ve büyütülen pencereler sadece sağlam kısmı kullanır.
- **Fare bozuk alana giremez.** Windows için o alan yoktur.
- **Ölü alan yok.** Birden fazla monitörde, bir monitörün bittiği yerden diğerine doğrudan geçersin.
- **Her şey geri alınabilir.** Her değişiklik 15 saniyelik onay ister; onaylamazsan kendiliğinden geri döner. Tek tuşla eski haline dönebilirsin.
- **Görsel ayar.** Bozuk alanı monitör çiziminde sürükleyerek ya da kaydırıcıyla ayarlarsın; ayar yaparken gerçek ekranda kırmızıyla gösterilir.
- **Nasıl çalışır paneli.** Her özelliği küçük animasyonlarla anlatır; bir ayarın üstüne gelince o ayarın ne yaptığını canlandırır.
- **4 dil:** Türkçe, English, 中文, Русский.

## Üç yöntem

| | Yazılım modu (önerilen) | Sürücü modu (deneysel) | Sanal monitör (deneysel) |
|---|---|---|---|
| Nasıl çalışır | Bozuk alanı görev çubuğu gibi rezerve edip kapatır; fareyi düşük seviyeli kancayla yönlendirir, taşan pencereleri geri iter | Windows ekran yapılandırmasında masaüstünü sinyalin sağlam kısmına yerleştirir; NVIDIA'da bu reddedilirse NVIDIA Warp ile dener | Sağlam alan boyutunda sanal bir monitör açar, fiziksel monitörün yerine koyar ve görüntüsünü Windows Büyüteç API'siyle sağlam alana 1:1 kopyalar |
| Uyumluluk | Her sistem | Ekran kartı sürücüsüne bağlı. NVIDIA GeForce'ta Warp yeni sürücülerde genelde kapalı | Windows 10 (1903+) / 11. Ekran Kurtar'ın kendi sanal ekran sürücüsü exe'nin içindedir, ilk kullanımda kurulur |
| Tam ekran oyunlar | Kenarlıksız pencere modunda | ✅ | Kenarlıksız pencere modunda; biraz gecikme ekler |
| Program kapalıyken | Program çalışmalı | Ayar kalır | Program çalışmalı; kapanınca ekran düzeni eski haline döner |

Yazılım modu varsayılandır. Deneysel bir yöntem başarısız olursa program hiçbir şeyi değiştirmeden yazılım moduna geçmeyi önerir.

### Sanal monitör yöntemi

Ayrı bir program indirmene gerek yok; sanal ekran sürücüsü `EkranKurtar.exe`'nin içindedir.

1. **Sanal monitör** yöntemini seç ve **Uygula**'ya bas.
2. Sürücü kurulu değilse program "Kurulsun mu?" diye sorar. **Kur ve devam et** de; Windows bir kez yönetici izni ister.
3. Program sürücüyü kurar, sağlam alan boyutunda sanal monitörü açar, yerine yerleştirir ve aynayı başlatır.

Geri al dediğinde (ya da program kapandığında) ekran düzeni önceki haline döner ve sanal monitör kapatılır. Sürücü kurulu kalır ama monitörsüz durur; hiçbir şey göstermez.

Sürücüyü tamamen kaldırmak için: `EkranKurtar.exe --uninstall-driver`

## Kullanım

1. [Releases](../../releases) sayfasından `EkranKurtar.exe` dosyasını indir ve çalıştır (kurulum gerekmez).
2. Bozuk monitörü ve bozuk kenarı seç.
3. Kaydırıcıyla kırmızı alanı bozuk kısmı tam kaplayana kadar ayarla.
4. **Uygula**'ya bas ve görüntü doğruysa **Böyle kalsın**'ı seç.

İstersen **Windows ile başlat** seçeneğini işaretle; sürücü güncellemesi gibi bir sebeple ayar sıfırlanırsa program açılışta yeniden uygular.

### Acil durum

Bir şeyler ters giderse:

- Uygulamadaki **Acil durum: tüm monitörleri sıfırla** bağlantısına tıkla, ya da
- Komut satırından çalıştır: `EkranKurtar.exe --reset`
- Ya da Windows **Ekran ayarları**'ndan çözünürlüğü "önerilen" değere getir.

## Kaynaktan derleme

Ekstra bir şey kurmaya gerek yok; Windows'ta bulunan .NET Framework 4 derleyicisi kullanılır.

```
build.bat
```

Çıktı: `bin\EkranKurtar.exe`. `driver\package` klasöründeki hazır sürücü paketi exe'nin içine gömülür.

### Sanal ekran sürücüsünü derleme (sadece sürücüyü değiştirdiysen)

Sürücü `driver\` klasöründedir: Microsoft'un IddCx (Indirect Display Driver) altyapısıyla yazılmış, kullanıcı modunda çalışan (UMDF 2) bir sürücü. Uygulama sürücüyle doğrudan konuşur ve sağlam alan boyutunda monitör ekleyip kaldırır.

Gerekenler: Visual Studio 2022 veya 2026, **C++ ile masaüstü geliştirme** bileşeni. WDK kurulu değilse betik Microsoft'un resmi NuGet paketini `driver\.wdk` içine indirir.

```
driver\build_driver.bat
build.bat
```

`build_driver.bat` sürücüyü derler, katalog dosyasını oluşturur ve imzalar. İlk çalıştırmada senin bilgisayarında kendinden imzalı bir sertifika oluşturur ("Ekran Kurtar Driver Signing", Kullanıcı\Kişisel deposu). Özel anahtar bilgisayarından çıkmaz; pakete sadece açık sertifika (`.cer`) girer. Ekran Kurtar sürücüyü kurarken bu sertifikayı Windows'un güvenilen sertifikalarına ekler, kaldırırken siler.

> Not: Kendinden imzalı sürücüler kişisel kullanım ve test için uygundur. Programı geniş çapta dağıtacaksan sürücüyü Microsoft'un imzalama hizmetinden (Partner Center, EV sertifikası gerekir) geçirmen önerilir.

Kurulum günlüğü: `C:\ProgramData\EkranKurtar\driver\install.log`

## Gereksinimler

- Windows 10 veya 11 (sanal monitör yöntemi için Windows 10 1903 veya üstü, 64 bit)
- .NET Framework 4.x (Windows'ta hazır gelir)

## Sürüm geçmişi

- **v1.4:** Sanal monitör modunda takılma ve "pencere görünüyor ama tıklanmıyor" sorunu düzeltildi. Ayna ve fare kilidi ayrı iş parçacıklarında çalışıyor. Fiziksel monitörün arkasında kalan pencereler (Ekran Kurtar dahil) otomatik olarak sanal monitöre taşınıyor. Program tekrar açılınca penceresi görünür yere geliyor. `--reset` çalışan programı düzgünce kapatıyor.
- **v1.3:** Ekran Kurtar'ın kendi sanal ekran sürücüsü (IddCx). Virtual Display Driver indirmeye gerek yok; sürücü exe'nin içinde, ilk kullanımda sorup kuruluyor. `--uninstall-driver` ile kaldırılır.
- **v1.2:** Sanal monitör yöntemi (Virtual Display Driver ile) ve NVIDIA Warp destekli sürücü modu. Yazılım modu varsayılan oldu.
- **v1.1:** Yeni arayüz, görsel ayar, "Nasıl çalışır" paneli, 4 dil (Türkçe, English, 中文, Русский).
- **v0.4:** İlk sürüm: sürücü modu ve yazılım modu.

## Lisans

MIT

---

# Ekran Kurtar (English)

**Don't throw away a monitor with a cracked or dead area.** Ekran Kurtar ("Screen Rescue") hides the broken part of your monitor from Windows, so Windows treats the monitor as if it only consisted of the working part.

## Features

- **Fullscreen = the working area.** Games, videos and maximized windows only use the working part.
- **The mouse cannot enter the broken area.**
- **No dead zones between monitors.** In multi-monitor setups, the cursor moves directly from one monitor's edge to the next.
- **Fully reversible.** Every change needs confirmation within 15 seconds or it reverts automatically. One click restores everything.
- **Visual setup.** Drag the broken area on the monitor drawing or use the slider; it is highlighted in red on the real screen while you adjust.
- **How it works panel** with small animations for every feature.
- **4 languages:** Türkçe, English, 中文, Русский.

## Three methods

- **Software mode (recommended):** reserves and covers the broken area like a taskbar, redirects the cursor with a low-level mouse hook so it never enters the broken area, and pushes overflowing windows back. Works everywhere; the app must keep running and games need borderless window mode.
- **Driver mode (experimental):** changes the Windows display configuration (`DisplayConfig` desktop image region) so the desktop is placed on the working part of the signal. NVIDIA drivers reject non-centered placement (error 31); in that case the app tries NVIDIA Warp (centered desktop + `NvAPI_GPU_SetScanoutWarping`), which is officially supported on professional GPUs and often disabled on recent GeForce drivers.
- **Virtual monitor (experimental):** creates a virtual monitor the size of the working area using Ekran Kurtar's own virtual display driver (built in, installed on first use after asking you), puts it in place of the physical monitor and mirrors it 1:1 onto the working area with the Windows Magnification API. Adds a little latency; the app must keep running, and closing it restores the display layout. Remove the driver with `EkranKurtar.exe --uninstall-driver`.

If an experimental method fails, nothing is changed and the app offers to switch to software mode.

## Usage

1. Download `EkranKurtar.exe` from [Releases](../../releases) and run it (no installation).
2. Pick the broken monitor and edge.
3. Move the slider until the red area exactly covers the broken part.
4. Press **Apply** and confirm with **Keep changes**.

Emergency reset: use the **Emergency: reset all monitors** link, run `EkranKurtar.exe --reset`, or set the recommended resolution in Windows Display Settings.

## Building

Run `build.bat` on Windows. It uses the .NET Framework 4 compiler included with Windows. Output: `bin\EkranKurtar.exe`, with the driver package from `driver\package` embedded.

The virtual display driver (`driver\`) is an IddCx / UMDF 2 indirect display driver. To rebuild it, install Visual Studio 2022/2026 with the C++ desktop workload and run `driver\build_driver.bat` (it downloads the official WDK NuGet package if the WDK is not installed, then builds, catalogs and signs the driver with a self-signed certificate created on your machine), then run `build.bat`.

## License

MIT
