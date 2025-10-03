# SphereServerScriptAnalyser

---

## 🇬🇧 English

SphereServerScriptAnalyser is a **Windows Forms** app (.NET net9.0-windows) that scans SphereServer **.scp** script files and flags common syntax/structure issues.  
It integrates **AutoUpdater.NET** to check for newer versions.

### Key Features
- Select a folder and scan for **.scp** files
- Balance checks for IF / ELSEIF(ELIF) / ELSE / ENDIF blocks
- Validations for end-of-file markers (e.g., `[eof]`)
- Basic pattern/regex validations
- Language switch & persisted settings
- **AutoUpdater.NET** integration
- Uses `AssemblyVersion`, `FileVersion`, `ProductVersion` where appropriate

### Requirements
- Windows 10/11
- .NET Desktop Runtime **9.0** (or SDK for development)

### Installation
1. Download the **setup / zip** release.
2. Extract or run the installer.
3. Launch the application.

> If AutoUpdater.NET is enabled, the app will check `Updater.xml` on startup.

### Usage
1. Choose a folder containing `.scp` files.  
2. Start the scan (e.g., “Scan Again”).  
3. Review issues in the list; double-click to open the file.  
4. You can change UI language; restarting may be required.

### Configuration
- App settings live in `App.config`.
- Version info in `SphereServerScriptAnalyser.csproj`:
  ```xml
  <Version>1.0.0.0</Version>
  <FileVersion>1.0.0.0</FileVersion>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  ```

### Development (Build)
```bash
dotnet restore
dotnet build -c Release
dotnet run --project SphereServerScriptAnalyser.csproj
# or publish
dotnet publish -c Release -r win-x64 -o out
```

### Auto Update (Optional)
- Uses `AutoUpdaterDotNET`.
- `AutoUpdater.Start("<Updater.xml URL>")`
- Prefer **FileVersion** for `InstalledVersion`.

---

## Project Structure
```
SphereServerScriptAnalyser/
├── SphereServerScriptAnalyser.sln
├── SphereServerScriptAnalyser.csproj
├── Program.cs
├── Form1.cs
├── Form1.Designer.cs
├── Properties/
└── App.config
```

---

## 🇹🇷 Türkçe

SphereServerScriptAnalyser, SphereServer için **.scp** script dosyalarını tarayıp temel sözdizimi hatalarını ve yapısal tutarsızlıkları tespit etmeye yardımcı olan bir **Windows Forms** (.NET net9.0-windows) uygulamasıdır.  
AutoUpdater.NET entegrasyonu ile yeni sürümleri otomatik kontrol edebilir.

### Başlıca Özellikler
- Klasör seçip içindeki **.scp** dosyalarını tarama
- IF / ELSEIF(ELIF) / ELSE / ENDIF blok **dengesi** kontrolü
- Dosya sonu işaretleri (örn. `[eof]`) için doğrulamalar
- Temel desen/Regex kontrolleri
- Dil seçimi ve kalıcı ayar kaydı (Settings)
- **AutoUpdater.NET** ile güncelleme kontrolü
- .NET sürüm bilgileri: `AssemblyVersion`, `FileVersion`, `ProductVersion` kullanımı

### Gereksinimler
- Windows 10/11
- .NET Desktop Runtime **9.0** (veya SDK geliştirme için)

### Kurulum
1. Yayınlanan **setup / zip** paketini indirin.
2. Arşivi açın veya kurulumu çalıştırın.
3. Uygulamayı başlatın.

> AutoUpdater.NET kullanıyorsanız uygulama açılışında `Updater.xml` üzerinden sürüm kontrolü yapılır.

### Kullanım
1. **Browse/Select Folder** ile `.scp` içeren klasörü seçin.  
2. **Scan** (veya “Yeniden Tara”) butonu ile taramayı başlatın.  
3. Bulunan sorunlar listelenir; bir öğeye çift tıklayarak ilgili dosyayı açabilirsiniz.  
4. Menüden dil değiştirebilirsiniz; değişiklik uygulamayı yeniden başlatmayı gerektirebilir.

### Yapılandırma
- `App.config` içinde uygulama ayarları bulunur.
- Sürüm bilgileri `SphereServerScriptAnalyser.csproj` içinde:
  ```xml
  <Version>1.0.0.0</Version>
  <FileVersion>1.0.0.0</FileVersion>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  ```

### Geliştirme (Build)
```bash
dotnet restore
dotnet build -c Release
dotnet run --project SphereServerScriptAnalyser.csproj
# veya yayınlama
dotnet publish -c Release -r win-x64 -o out
```

### Otomatik Güncelleme (Opsiyonel)
- Uygulama, `AutoUpdaterDotNET` kullanır.
- `AutoUpdater.Start("<Updater.xml URL>")`
- `InstalledVersion` için **FileVersion** kullanmanız önerilir.
