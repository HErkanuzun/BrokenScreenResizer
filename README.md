<div align="center">

<img src="docs/images/logo.png" width="88" alt="Broken Screen Resizer logosu">

# Broken Screen Resizer

**Monitörünün bozuk kısmını Windows'tan gizle; geri kalanını tam ekran gibi kullan.**

[![Son sürüm](https://img.shields.io/github/v/release/HErkanuzun/BrokenScreenResizer?label=s%C3%BCr%C3%BCm&color=2ec4b6)](https://github.com/HErkanuzun/BrokenScreenResizer/releases/latest)
[![İndirme](https://img.shields.io/github/downloads/HErkanuzun/BrokenScreenResizer/total?label=indirme)](https://github.com/HErkanuzun/BrokenScreenResizer/releases)
[![Derleme](https://img.shields.io/github/actions/workflow/status/HErkanuzun/BrokenScreenResizer/build.yml?branch=main&label=derleme)](https://github.com/HErkanuzun/BrokenScreenResizer/actions/workflows/build.yml)
[![Lisans: MIT](https://img.shields.io/github/license/HErkanuzun/BrokenScreenResizer?label=lisans)](LICENSE)
![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6)

🇹🇷 **Türkçe** · 🇬🇧 [English](docs/README.en.md) · 🇷🇺 [Русский](docs/README.ru.md) · 🇨🇳 [简体中文](docs/README.zh-CN.md)

<img src="docs/images/screenshot-tr.png" width="760" alt="Broken Screen Resizer ekran görüntüsü">

</div>

---

## İçindekiler

- [Ne işe yarar?](#ne-işe-yarar)
- [Özellikler](#özellikler)
- [Kurulum](#kurulum)
- [Kullanım](#kullanım)
- [Yöntemler](#yöntemler)
- [Ekran Çoğaltıcı](#ekran-çoğaltıcı)
- [Windows düzenlemeleri](#windows-düzenlemeleri)
- [Ekran düzeni](#ekran-düzeni)
- [Güvenlik ve gizlilik](#güvenlik-ve-gizlilik)
- [Güncelleme ve kaldırma](#güncelleme-ve-kaldırma)
- [Sorun giderme](#sorun-giderme)
- [Komut satırı](#komut-satırı)
- [Kaynaktan derleme](#kaynaktan-derleme)
- [Sürüm geçmişi](#sürüm-geçmişi)
- [Broken ailesi](#broken-ailesi)
- [Lisans](#lisans)

## Ne işe yarar?

Monitörünün bir kenarında çizgiler, ölü pikseller ya da kırık bir bölge mi var? Broken Screen Resizer o bölgeyi Windows'tan saklar. Windows monitörü sanki sadece **sağlam kısımdan** ibaretmiş gibi kullanır: pencereler, görev çubuğu, tam ekran videolar ve fare bozuk alana girmez.

Monitörü atmak ya da her gün pencereleri elle kaydırmak yerine bir kez ayarlarsın, gerisini program halleder.

## Özellikler

- **Birden fazla bozuk kenar.** Sol, sağ, üst, alt ya da birkaçı birden; sağlam alan ekranın herhangi bir dikdörtgeni olabilir.
- **Görsel ayar.** Monitör çiziminde kenarları sürüklersin; köşeden tutunca iki kenar birlikte, ortadan tutunca tüm alan taşınır. Piksel hassasiyetinde ince ayar için kaydırıcı ve −/+ düğmeleri var.
- **Canlı önizleme.** Ayar yaparken gizlenecek alan gerçek ekranda birkaç saniyeliğine kırmızıyla gösterilir. Program açılışta ekranı asla kendiliğinden boyamaz.
- **Tam ekran = sağlam alan.** Büyütülen pencereler ve videolar sadece sağlam kısmı kullanır.
- **Fare ve pencereler dışarıda.** İmleç bozuk alana giremez; oraya taşan pencereler geri itilir.
- **Her şey geri alınabilir.** Her değişiklik 15 saniyelik onay ister; onaylamazsan kendiliğinden geri döner. Tek tıkla acil sıfırlama da var.
- **Sürüm bilinçli kurulum.** Kurulum paketi program varsa günceller, yoksa kurar; çalışan eski sürüm önce düzgünce kapatılır.
- **Tam kaldırma.** Kaldırınca programın bilgisayarda yaptığı her değişiklik geri alınır, kalıntı bırakmaz.
- **Küçük ekranlara uyumlu.** Pencere boyutlandırılabilir; dar ekranda tek sütuna geçer ve kaydırılır.
- **4 dil:** Türkçe, English, 中文, Русский.

## Kurulum

[**Son sürümü indir →**](https://github.com/HErkanuzun/BrokenScreenResizer/releases/latest)

| Dosya | Ne zaman |
|---|---|
| `BrokenScreenResizer-<sürüm>-Setup.exe` | **Önerilen.** Kurar; zaten kuruluysa günceller. Başlat menüsüne eklenir, Windows "Uygulamalar" listesinde sürümüyle görünür. Yönetici izni istemez. |
| `BrokenScreenResizer-<sürüm>-Portable.zip` | Kurulum istemeyenler için. Çıkart, `BrokenScreenResizer.exe`'yi çalıştır. Kaldırmak için `Uninstall.bat`. |
| `SHA256SUMS.txt` | İndirdiğin dosyanın değişmediğini doğrulamak için özetler. |

> [!NOTE]
> **"Windows kişisel bilgisayarınızı korudu" uyarısı:** Program ücretsiz ve açık kaynaklı olduğu için ücretli bir kod imzalama sertifikası yok. SmartScreen, az indirilmiş her imzasız program gibi uyarı gösterebilir. **Ek bilgi → Yine de çalıştır** ile devam edebilirsin. Dosyanın gerçekten bu depodan çıktığını aşağıdaki gibi doğrulayabilirsin.

### İndirilen dosyayı doğrulama

Sürümdeki her dosya GitHub Actions üzerinde, bu depodaki kaynak koddan derlenir. GitHub da dosyanın hangi koddan ve hangi derlemeden çıktığını imzalı bir kayıtla (*build provenance*) onaylar.

```powershell
# 1) SHA-256 özetini SHA256SUMS.txt ile karşılaştır
Get-FileHash .\BrokenScreenResizer-1.7-Setup.exe -Algorithm SHA256

# 2) Dosyanın bu depodaki koddan GitHub'da derlendiğini doğrula (GitHub CLI gerekir)
gh attestation verify .\BrokenScreenResizer-1.7-Setup.exe --repo HErkanuzun/BrokenScreenResizer
```

**Gereksinimler:** Windows 10 veya 11 (64 bit), .NET Framework 4.x (Windows'ta hazır gelir). Sanal monitör yöntemi için Windows 10 1903 veya üstü.

## Kullanım

1. Programı aç ve bozuk monitörü seç.
2. Monitör çiziminde bozuk olan her kenarı içeri sürükle. Ayar yaparken bozuk alan gerçek ekranda kırmızıyla görünür; kırmızı alan bozuk kısmı tam kaplayınca dur. İnce ayar için **Bozuk kenarlar** bölümünden kenarı seçip kaydırıcıyı ya da −/+ düğmelerini kullan.
3. Yöntemi seç (**Yazılım modu** önerilir) ve **Uygula**'ya bas.
4. Görüntü doğruysa **Böyle kalsın**'ı seç. Seçmezsen 15 saniye sonra her şey eski haline döner.

**Windows ile başlat** açıksa ayar her açılışta kendiliğinden uygulanır. Program sağ alttaki simgede çalışmaya devam eder.

## Yöntemler

| | Yazılım modu (önerilen) | Sanal monitör (deneysel) |
|---|---|---|
| Nasıl çalışır | Bozuk kenarları görev çubuğu gibi Windows'a "ayrılmış alan" olarak bildirir, fareyi yönlendirir ve taşan pencereleri geri iter. | Sağlam alan boyutunda sanal bir monitör açar, fiziksel monitörün yerine koyar ve görüntüsünü sağlam alana birebir kopyalar. |
| Uyumluluk | Her sistem | Windows 10 1903+ / 11, 64 bit. Programın kendi sanal ekran sürücüsü exe'nin içindedir; ilk kullanımda sorulup kurulur (bir kez yönetici izni). |
| Tam ekran oyunlar | Kenarlıksız pencere modunda | Kenarlıksız pencere modunda; biraz gecikme ekler |
| Program kapalıyken | Program çalışmalı | Program çalışmalı; kapanınca ekran düzeni eski haline döner |

Sanal monitör yöntemi başarısız olursa program hiçbir şeyi değiştirmeden yazılım moduna geçmeyi önerir.

## Ekran Çoğaltıcı

Büyük bir monitörü (örneğin 4K bir TV) birkaç ayrı monitöre böler. Her parça Windows'ta gerçek bir monitör olur: büyütülen pencere sadece kendi parçasını kaplar, Windows + ok tuşlarıyla parçalar arasında taşınır.

- **Yerleşim.** Üstteki **Ekran Çoğaltıcı** sekmesinde ekranları monitör çiziminde sürükleyip boyutlandırırsın. Kenarlar komşu ekranlara ve 8 piksellik ızgaraya yapışır. **Ekran ekle** boş alana ya da en büyük ekranı ikiye bölerek yeni ekran ekler. **Ekranı kaldır** seçili ekranı siler. En fazla 8 ekran açılabilir.
- **Çarpışma.** Bir ekranı komşusuna doğru sürüklersen önce durur ve titrer. Aynı yöne bir daha sürüklersen komşuyu en küçük boyutuna (640×360) kadar küçültür.
- **Şablonlar.** 2 yan yana, 2 üst üste, 3 sütun, 1 büyük + 2, 2 × 2, 1 büyük + 3, 3 + 2, 3 × 2, 4 × 2. Monitöre sığmayan şablonlar soluk görünür.
- **Öneriler.** Program monitörün boyutunu monitörün kendi bilgisinden (EDID) okur ve piksel yoğunluğunu (PPI) hesaplar. Buna göre üç öneri verir: kalite kaybı olmadan, önerilen ve en fazla. Örneğin 55″ 4K bir TV için 2 / 4 / 6 ekran. Boyut okunamazsa inç değerini elle girebilirsin.
- **Dağılım.** Her ekranın çözünürlüğü, monitörün yüzde kaçını kapladığı, kaç inç olduğu ve kalitesi listelenir.

| Kalite | En az |
|---|---|
| Keskin | 3,5 MP ve 20″ |
| İyi | 1,9 MP ve 14″ |
| Kullanılabilir | 1,2 MP ve 10″ |

**Nasıl çalışır:** her parça için bir sanal monitör açılır, fiziksel monitör bunların arkasına alınır ve her sanal monitörün görüntüsü fiziksel monitördeki yerine kopyalanır (sanal monitör yöntemiyle aynı teknik). Bu yüzden program çalışmaya devam etmelidir; kapanınca ekran düzeni eski haline döner. v1.5, sanal ekran sürücüsünün yeni sürümünü (2.0) kullanır. İlk kullanımda sürücüyü güncellemek için bir kez yönetici izni istenir. Bozuk ekran modu yeni sürücüyle de çalışır.

Bozuk ekran ayarı ile Ekran Çoğaltıcı aynı anda kullanılamaz; birinden diğerine geçmek için önce **Geri al**'a bas.

## Windows düzenlemeleri

Görev çubuğunu her monitörde ayrı bir kenara yaslar: örneğin ana monitörde altta, sağdaki monitörde solda, üstteki monitörde yukarıda. İstersen bir monitörü tamamen çubuksuz bırakabilirsin.

- **Seçim.** Üstteki **Windows düzenlemeleri** sekmesinde monitörlerin çizimi görünür. Bir monitörün kenarına tıklayınca çubuk oraya geçer; ya da monitörü seçip **Alt / Üst / Sol / Sağ / Çubuk yok** düğmelerini kullanırsın. **Uygula** ile etkinleşir, **Geri al** her şeyi eski haline döndürür.
- **Ana monitör** her zaman Windows'un kendi görev çubuğunu kullanır (Başlat, sistem tepsisi, bildirimler). Kenarı Windows 11'in "Görev çubuğu konumu" ayarıyla değiştirilir.
- **Diğer monitörler** ana monitörle aynı kenardaysa hepsinde Windows'un çubuğu kalır. Farklı kenar seçilen monitörlere program kendi görev çubuğunu çizer: Başlat düğmesi, o monitördeki açık pencereler (tıkla: öne getir / küçült, sağ tık: menü), yanıp sönen pencere uyarısı ve saat. Bu çubuk da Windows'unki gibi ekranda yer ayırır; büyütülen pencereler üstüne binmez. Tam ekran oyun ve videolarda kendini gizler.
- **Simge hizası** (ortada / solda) ve programın çubuğunda pencere adlarının gösterilmesi ayarlanabilir.

**Neden iki çeşit çubuk?** Windows 11'in görev çubuğu tek bir konum alır ve bu konum tüm monitörlere uygulanır; monitör başına ayrı kenar seçilemez. Bu yüzden farklı kenar istenen monitörlerde Windows'un ikincil çubukları kapatılır ve yerine programın çubuğu konur. Program kapanınca Windows'un çubukları geri gelir, açılınca ayar yeniden uygulanır.

**Not:** Görev çubuğunu alt dışındaki bir kenara taşıma özelliği Windows 11'e 2026'da Windows Update ile kademeli olarak geldi. Windows'un henüz bunu desteklemiyorsa ana monitördeki çubuk altta kalır ve program bunu söyler; diğer monitörlerin ayarı yine uygulanır. Program yalnızca kullanıcıya ait görev çubuğu ayarlarını değiştirir; ilk değerleri saklar ve **Geri al**, **acil sıfırlama** ya da kaldırma sırasında geri yazar.

## Ekran düzeni

Monitörlerin masaüstündeki yerini Windows Ekran ayarlarına gitmeden, programın **Ekran düzeni** sekmesinden ayarlarsın.

- Ekranı sürükle: bıraktığın yerde en yakın ekranın kenarına yapışır; üst, alt ya da ortası yakınsa hizalanır. Ekranlar üst üste binmez ve hep birbirine değer.
- Bir ekrana tıklayıp **Ana ekran yap** ile Windows'un ana ekranını değiştir.
- **Uygula**'dan sonra 15 saniye içinde onaylamazsan eski düzen geri gelir.
- Sanal monitör ya da Ekran Çoğaltıcı açıkken bozuk monitörün görünmeyen kopyası listede yoktur; program onu hep diğer ekranların altına alır. Yeni düzen kaydedilir, program yeniden açılınca da korunur.

## Güvenlik ve gizlilik

Broken Screen Resizer açık kaynaklıdır; aşağıdakilerin hepsi koddan doğrulanabilir.

- **İnternete bağlanmaz.** Hiçbir veri toplamaz ya da göndermez; güncelleme kontrolü bile yapmaz.
- **Yönetici izni istemez.** Tek istisna sanal monitör sürücüsünün kurulumu ve kaldırılmasıdır; Windows o anda izin sorar.
- **Klavyeyi dinlemez.** Yazılım modu sadece fare imlecini bozuk alanın dışında tutmak için Windows'un fare kancasını kullanır.
- **Neyi değiştirir?**
  - Ayarlar ve tanı günlüğü: `%AppData%\BrokenScreenResizer\settings.ini`, `log.txt`
  - "Windows ile başlat" açıksa: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\BrokenScreenResizer`
  - Sanal monitör yöntemi kullanıldıysa: sanal ekran sürücüsü, onu imzalayan sertifika (Güvenilen Kök ve Güvenilen Yayımcılar depolarında) ve `C:\ProgramData\BrokenScreenResizer\driver`
- **Sürücü sertifikası:** Sürücü kendinden imzalı bir sertifikayla imzalanmıştır. Sertifika sadece *kod imzalama* için geçerlidir; web sitesi ya da e-posta trafiğine karışamaz. Özel anahtarı hiçbir zaman depoya ya da sürüme girmez. Kaldırma sırasında sertifika da silinir.
- **Kaldırma her şeyi geri alır.** Yukarıdakilerin hepsi, eski "Ekran Kurtar" adıyla kalanlar dahil, kaldırma sırasında temizlenir.

Bir güvenlik sorunu bulursan lütfen [SECURITY.md](SECURITY.md)'deki adımları izle.

Kod imzalama: **[Code signing policy](SECURITY.md#code-signing-policy)** (SignPath Foundation, ücretsiz).

## Güncelleme ve kaldırma

**Güncelleme:** Yeni sürümün kurulum paketini çalıştırman yeterli. Paket mevcut kurulumu bulur, çalışan eski sürümü (taşınabilir kopyalar dahil) düzgünce kapatır, üzerine kurar ve ayarlarını korur. Taşınabilir sürümde yeni exe'yi açman yeterli; eski sürüm çalışıyorsa kapatılır ve yerini yenisi alır.

**Kaldırma:** Aşağıdakilerden biri yeterli:

- Windows **Ayarlar → Uygulamalar → Broken Screen Resizer → Kaldır** (kurulum paketiyle kurduysan)
- Programdaki **Programı kaldır…** bağlantısı ya da sağ alttaki simgenin menüsü
- Taşınabilir sürümde `Uninstall.bat`

Kaldırma ekranları eski haline getirir; sanal sürücüyü ve sertifikasını, Windows'un sanal monitör kayıtlarını, başlangıç kaydını ve ayarları siler. Sürücü kuruluysa Windows bir kez yönetici izni ister.

## Sorun giderme

<details>
<summary><b>Sanal monitör modunda ekran bembeyaz kaldı</b></summary>

v1.7'den itibaren ayna bunu kendisi fark edip birkaç saniye içinde onarır. Geçmezse tepsi simgesine sağ tıklayıp **Ekranı onar**'ı seç; programı kapatıp açmaya gerek yok.
</details>

<details>
<summary><b>Windows'ta fazladan (3.) bir ekran görünüyor</b></summary>

Sanal monitör modunda bozuk monitörün kendisi Windows'ta ayrı bir ekran olarak kalır; görüntü oraya kopyalandığı için Windows'un onu açık tutması gerekir. Fare oraya giremez, oraya düşen pencereler anında geri alınır ve programın kendi ekran listelerinde gösterilmez. Yönetici olarak çalışan bir pencere orada kalırsa onu seçip **Win+Shift+←/→** ile taşıyabilirsin.
</details>

<details>
<summary><b>Ekran garip kaldı ya da yanlış monitör kırpıldı</b></summary>

Programdaki **Acil durum: tüm monitörleri sıfırla** bağlantısına tıkla ya da komut satırında `BrokenScreenResizer.exe --reset` çalıştır. Son çare olarak Windows **Ekran ayarları**'ndan çözünürlüğü "önerilen" değere getir.
</details>

<details>
<summary><b>Sanal monitör açılmıyor</b></summary>

- Windows 10 1903 veya üstü ve 64 bit olmalı.
- Sürücü kurulumunda yönetici iznini onayladığından emin ol.
- Kurulum günlüğüne bak: `C:\ProgramData\BrokenScreenResizer\driver\install.log`
- Bilgisayarı yeniden başlatıp tekrar dene. Olmazsa **Yazılım modu**nu kullan; her sistemde çalışır.
</details>

<details>
<summary><b>Tam ekran oyun bozuk alana taşıyor</b></summary>

Oyunu **kenarlıksız pencere** (borderless / windowed fullscreen) modunda çalıştır. Özel tam ekran modu Windows'un masaüstü ayarlarını atlar.
</details>

<details>
<summary><b>Yeni sürümü açınca eski sürüm açılıyor</b></summary>

v1.0'dan itibaren yeni sürüm, çalışan eski sürümü kendiliğinden kapatıp yerini alır. Kurulum paketi de kurmadan önce çalışan her kopyayı kapatır.
</details>

## Komut satırı

| Komut | Ne yapar |
|---|---|
| `BrokenScreenResizer.exe --tray` | Pencereyi açmadan simge olarak başlar (Windows ile başlat bunu kullanır) |
| `BrokenScreenResizer.exe --reset` | Acil durum: tüm monitörleri sıfırlar, çalışan kopyayı düzgünce kapatır |
| `BrokenScreenResizer.exe --uninstall` | Tam kaldırma (onay ister) |
| `BrokenScreenResizer.exe --uninstall-driver` | Sadece sanal ekran sürücüsünü kaldırır |

## Kaynaktan derleme

Ekstra bir araç gerekmez; Windows'taki .NET Framework 4 derleyicisi kullanılır.

```bat
build.bat
```

Çıktı `bin\BrokenScreenResizer.exe`. `driver\package` klasöründeki hazır, imzalı sürücü paketi exe'nin içine gömülür. Sürüm paketleri (kurulum + taşınabilir ZIP + özetler) için [Inno Setup 6](https://jrsoftware.org/isinfo.php) gerekir:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\package.ps1 -Version 1.7
```

<details>
<summary><b>Sanal ekran sürücüsünü derleme (sadece sürücüyü değiştirdiysen)</b></summary>

Sürücü `driver\` klasöründedir: Microsoft'un IddCx (Indirect Display Driver) altyapısıyla yazılmış, kullanıcı modunda çalışan (UMDF 2) bir sürücü. Gerekenler: Visual Studio 2022 veya 2026 ve **C++ ile masaüstü geliştirme** bileşeni. WDK kurulu değilse betik Microsoft'un resmi NuGet paketini `driver\.wdk` içine indirir.

```bat
driver\build_driver.bat
build.bat
```

`build_driver.bat` sürücüyü derler, katalog dosyasını oluşturur ve geliştiricinin bilgisayarında bir kez oluşturulan kendinden imzalı sertifikayla imzalar. Özel anahtar dışa aktarılamaz ve bilgisayardan çıkmaz; pakete sadece açık sertifika (`.cer`) girer.
</details>

### Proje yapısı

```
src/          Uygulama (C#, WinForms, .NET Framework 4)
driver/       Sanal ekran sürücüsü (C++, IddCx / UMDF 2) ve imzalı paket
installer/    Kurulum paketi betiği (Inno Setup)
scripts/      Sürüm paketleme
packaging/    Taşınabilir ZIP'e giren kısa kullanım notu
docs/         Diğer dillerde README ve görseller
```

## Sürüm geçmişi

Ayrıntılar: [CHANGELOG.md](CHANGELOG.md) · Tüm sürümler: [Releases](https://github.com/HErkanuzun/BrokenScreenResizer/releases)

- **v1.9** — Sanal monitör modunda ağır bir oyun açıkken aynanın donması ya da saniyede 1-2 kareye düşmesi düzeltildi. "Windows ile başlat" açıkken ayarın oturum açılışında bazen uygulanmaması düzeltildi. Tanı günlüğü eklendi (`%AppData%\BrokenScreenResizer\log.txt`).
- **v1.8** — Broken ailesinin ortak cam tasarımı; logo, ad ve düğmeler her sayfada aynı yerde.
- **v1.7** — Ekran düzeni: monitörlerin yerini program içinden sürükleyerek ayarla. Sanal monitör modunda beyaz kalan ayna kendini onarır; görünmeyen 3. ekrana düşen pencereler anında geri gelir. Ayna monitörün yenilenmesine (VSync) göre çizilir; oyunlarda daha akıcı. Ekran Çoğaltıcı ve Windows düzenlemeleri artık kararlı.
- **v1.6 (önizleme)** — Windows düzenlemeleri: görev çubuğunu her monitörde istediğin kenara yasla (alt, üst, sol, sağ ya da çubuksuz). Ana monitörde Windows'un çubuğu, diğerlerinde gerekirse programın kendi çubuğu. Kararlı sürüm değildir.
- **v1.5 (önizleme)** — Ekran Çoğaltıcı: büyük bir monitörü 8'e kadar ayrı ekrana böl. Sürükle ve it düzenleyici, şablonlar, piksel yoğunluğuna göre üç öneri. Sanal ekran sürücüsü 2.0. Kararlı sürüm değildir.
- **v1.01** — Geniş pencerede ve tam ekranda içerik ortalanır. Ayar uygulanınca değiştirilemeyen kısımlar sisli görünür, kilit etiketi çıkar ve Geri al öne çıkar.
- **v1.0** — İlk kararlı sürüm: kurulum paketi (varsa günceller, yoksa kurar), sürüm bilinçli açılış, açılışta kırmızı ekran ve varsayılan kesim yok, antivirüs dostu kaldırma, doğrulanabilir sürüm dosyaları.
- **v0.7** — Sürücü modu kaldırıldı; tam kaldırma; küçük ekranlara uyumlu arayüz.
- **v0.6** — Yeni ad (Broken Screen Resizer), birden fazla bozuk kenar.
- **v0.5** — Sanal monitör modunda takılma düzeltmesi.
- **v0.4** — Programın kendi sanal ekran sürücüsü.
- **v0.3** — Sanal monitör yöntemi.
- **v0.2** — Yeni arayüz, 4 dil.
- **v0.1** — İlk sürüm (adı Ekran Kurtar).

## Broken ailesi

Windows'ta bozuk ya da sorunlu bir şeyi bulup onunla yaşamanı sağlayan küçük araçlar. Hepsi aynı tasarım dilinde, ücretsiz ve açık kaynak.

| | |
|---|---|
| **Broken Screen Resizer** | Monitörün bozuk kısmını Windows'un dışında bırakır. |
| [Broken Disk Resizer](https://github.com/HErkanuzun/BrokenDiskResizer) | Hard diskin bozuk sektörlerini bulur, sadece sağlam kısımları bölümler. |
| [Broken Space Finder](https://github.com/HErkanuzun/BrokenSpaceFinder) | Diski neyin doldurduğunu bulur ve güvenle temizler. |
| [Broken Microphone](https://github.com/HErkanuzun/BrokenMicrophone) | Telefonu ya da tableti bilgisayarın mikrofonu yapar. |
| [Broken Hub](https://github.com/HErkanuzun/BrokenHub) | Bütün Broken araçlarını tek yerden kurar, günceller ve açar. |

## Lisans

[MIT](LICENSE) © 2026 HErkanuzun. Özgürce kullanabilir, değiştirebilir ve dağıtabilirsin.
