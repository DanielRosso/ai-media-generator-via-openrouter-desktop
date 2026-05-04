# 🎬 Media Generator

> Eine native Cross-Platform Desktop-App, gebaut mit **C#** und **Avalonia UI**, um KI-Bilder und KI-Videos über die [OpenRouter API](https://openrouter.ai) zu generieren.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)
![Avalonia](https://img.shields.io/badge/Avalonia-12.0-blue.svg)
![Build](https://github.com/DanielRosso/ai-media-generator-via-openrouter-desktop/actions/workflows/build.yml/badge.svg)

---

## ✨ Features

- 🖼️ **KI-Bildgenerierung** – Nutze Modelle wie `flux 2 Pro` oder `Seedream 4.5` direkt aus der App
- 🎥 **KI-Videogenerierung** – Unterstützt Video-Modelle wie `Seedance 2.0` und `kling 3.0 Std` mit integriertem Polling-Mechanismus für lange Generierungs-Jobs
- 🔄 **Dynamische Modellauswahl** – Wechsle zwischen Bild- und Video-Modus; die Modell-Liste aktualisiert sich automatisch
- ⬇️ **Direkter Download** – Bilder und Videos werden direkt auf deine Festplatte gespeichert (kein Browser-Umweg)
- 🔑 **Sichere API-Key-Verwaltung** – Der OpenRouter API-Key wird ausschließlich lokal in deinem `AppData`-Verzeichnis gespeichert – keine `.env`-Dateien, kein Risiko versehentlicher Git-Commits
- 🤖 **Vollautomatischer Build** – GitHub Actions kompiliert bei jedem Push auf `main` automatisch eine fertige `.exe`
- 🐛 **Integriertes Debug-Protokoll** – Bei API-Fehlern wird ein detailliertes Protokoll (Statuscode, gesendeter Payload, Rohantwort) direkt im UI angezeigt

---

## 📥 Installation & Download

**Du musst den Code nicht selbst kompilieren!**

Die fertige `.exe`-Datei für Windows (x64) steht direkt auf GitHub zum Download bereit:

👉 **[Zur Releases-Seite](https://github.com/DanielRosso/ai-media-generator-via-openrouter-desktop/releases)**

1. Lade die neueste `MediaGenerator-win-x64.exe` herunter
2. Starte die Datei – keine Installation nötig (self-contained)

> Alternativ kannst du unter **Actions → letzter erfolgreicher Build → Artifacts** die neueste Build-Version herunterladen.

---

## 🚀 Nutzung

### Erster Start

Beim allerersten Start der App erscheint ein **Setup-Bildschirm**:

1. Gehe zu [openrouter.ai/keys](https://openrouter.ai/keys) und erstelle einen API-Key
2. Füge den Key in das Eingabefeld ein und klicke **„✅ Key speichern"**
3. Der Key wird sicher in `%APPDATA%\MediaGenerator\config.json` gespeichert
4. Die App startet direkt ins Haupt-UI – beim nächsten Start entfällt dieser Schritt

### Bild generieren

1. Wähle **„Bild"** im Medientyp-Dropdown
2. Wähle ein Modell (z. B. `bblack-forest-labs/flux.2-pro`)
3. Gib deinen Prompt ein und klicke **„✨ Generieren"**
4. Das Bild erscheint direkt in der App – mit **„💾 Bild speichern"** auf die Festplatte speichern

### Video generieren

1. Wähle **„Video"** im Medientyp-Dropdown
2. Wähle ein Modell (z. B. `bytedance/seedance-2.0`)
3. Gib deinen Prompt ein und klicke **„✨ Generieren"**
4. Die App startet den Job und pollt automatisch alle 5 Sekunden den Status
5. Sobald das Video fertig ist, klicke **„⬇ Video herunterladen und speichern"** – der Media Player öffnet sich automatisch

---

## 🛠️ Selbst kompilieren (optional)

```bash
# Voraussetzungen: .NET 10 SDK
git clone https://github.com/DanielRosso/ai-media-generator-via-openrouter-desktop.git
cd ai-media-generator-via-openrouter-desktop
dotnet run
```

Für eine selbst-enthaltene `.exe`:

```bash
dotnet publish vid-img-frontend-net-core.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish_output
```

---

## 🧩 Technologie-Stack

| Komponente    | Technologie                             |
| ------------- | --------------------------------------- |
| Sprache       | C# / .NET 10                            |
| UI-Framework  | Avalonia UI 12                          |
| MVVM          | CommunityToolkit.Mvvm                   |
| HTTP          | System.Net.Http (HttpClient)            |
| JSON          | System.Text.Json                        |
| Konfiguration | Microsoft.Extensions.Configuration.Json |
| CI/CD         | GitHub Actions                          |

---

## 📄 Lizenz

Dieses Projekt steht unter der [MIT License](LICENSE).  
Copyright © 2026 Daniel Rosso
