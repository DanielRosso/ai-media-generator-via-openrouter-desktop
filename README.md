<div align="center">
  <h1>🖥️ AI Media Orchestrator (Desktop Native)</h1>
  <p>Ein nativer Cross-Platform-Client für die Generierung von KI-Medien, entwickelt mit C# .NET 10 und Avalonia UI.</p>

  <p>
    <img src="https://img.shields.io/badge/.NET-10.0-5C2D91.svg?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 10" />
    <img src="https://img.shields.io/badge/C%23-239120.svg?style=for-the-badge&logo=csharp&logoColor=white" alt="C#" />
    <img src="https://img.shields.io/badge/Avalonia-UI-blue.svg?style=for-the-badge" alt="Avalonia UI" />
    <img src="https://github.com/DanielRosso/ai-media-generator-via-openrouter-desktop/actions/workflows/build.yml/badge.svg" alt="Build Status" />
  </p>
</div>

---

## 📌 Projektübersicht

Während Weboberflächen großartig sind, profitieren rechenintensive Medien-Workflows oft von einer nativen Desktop-Integration: direkte Dateisystemzugriffe, lokale Speicherung von Zugangsdaten und keine browserbasierten Timeout-Beschränkungen. 

Diese C#-Anwendung nutzt **Avalonia UI**, um eine nahtlose, performante und native Erfahrung für die Interaktion mit der OpenRouter API zu bieten. Sie demonstriert fortgeschrittene asynchrone Programmierung in .NET und robustes State Management für lang laufende KI-Aufgaben.

## 🏗️ Architektur & Technische Highlights

Dieses Projekt wurde mit einem starken Fokus auf Enterprise-Muster und Clean-Code-Prinzipien entwickelt:

- **🔄 Asynchrone Polling-Engine:** Die KI-Videogenerierung durch Modelle (wie Kling oder Seedance) braucht Zeit. Die App implementiert einen robusten, nicht-blockierenden `async/await` Polling-Mechanismus, um den Status der Jobs abzufragen, ohne den UI-Thread einzufrieren.
- **🏛️ MVVM-Architektur:** Streng nach dem Model-View-ViewModel (MVVM) Prinzip aufgebaut. Dies sorgt für eine klare Trennung zwischen Geschäftslogik, API-Aufrufen und dem Avalonia XAML-Frontend, wodurch die Codebasis hochgradig testbar und wartbar wird.
- **🔐 Sichere lokale Zugangsdatenverwaltung:** API-Keys sind *niemals* fest im Code hinterlegt oder werden über ungesicherte `.env`-Dateien gefordert. Sie werden sicher im lokalen `AppData`-Verzeichnis des Benutzers verwaltet, was das Risiko versehentlicher Git-Commits vollständig eliminiert.
- **⚙️ CI/CD & Automatisierung:** Integriert mit **GitHub Actions**. Jeder Push in den `main`-Branch löst eine automatisierte Build-Pipeline aus, die die Anwendung kompiliert und automatisch die fertige Windows `.exe`-Datei erstellt.
- **🗂️ Direkter Dateisystemzugriff (Disk I/O):** Umgeht Browser-Download-Dialoge, indem generierte Medien direkt und nahtlos auf der lokalen Festplatte gespeichert werden.

## 📥 Download (Vorkompiliert)

**Du musst den Code nicht selbst kompilieren!**
Dank der CI/CD Pipeline steht eine vollautomatisch gebaute `.exe`-Datei (Windows x64) bereit.

👉 **[Download der neuesten Release Version hier](https://github.com/DanielRosso/ai-media-generator-via-openrouter-desktop/releases)**

## 🚀 Für Entwickler (Build from Source)

Falls du den Code selbst ausführen oder anpassen möchtest:

### Voraussetzungen
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Ausführen
```bash
git clone https://github.com/DanielRosso/ai-media-generator-via-openrouter-desktop.git
cd ai-media-generator-via-openrouter-desktop

# Projekt bauen und starten
dotnet run
```

## 📄 Lizenz
Dieses Projekt steht unter der [MIT-Lizenz](./LICENSE).