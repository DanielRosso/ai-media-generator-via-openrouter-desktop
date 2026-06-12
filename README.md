<div align="center">
  <h1>🖥️ AI Media Orchestrator (Desktop Native)</h1>
  <p>A cross-platform native client for AI media generation, built with C# .NET 10 and Avalonia UI.</p>

  <p>
    <img src="https://img.shields.io/badge/.NET-10.0-5C2D91.svg?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 10" />
    <img src="https://img.shields.io/badge/C%23-239120.svg?style=for-the-badge&logo=csharp&logoColor=white" alt="C#" />
    <img src="https://img.shields.io/badge/Avalonia-UI-blue.svg?style=for-the-badge" alt="Avalonia UI" />
    <img src="https://github.com/DanielRosso/ai-media-generator-via-openrouter-desktop/actions/workflows/build.yml/badge.svg" alt="Build Status" />
  </p>
</div>

---

## 📌 Executive Summary

While web interfaces are great, heavy media generation workflows often benefit from native desktop integration: direct disk I/O, local credential storage, and no browser-based timeout limitations. 

This C# application leverages **Avalonia UI** to provide a seamless, performant, native experience for interacting with the OpenRouter API. It demonstrates advanced asynchronous programming in .NET and robust state management for long-running AI tasks.

## 🏗️ Architecture & Technical Highlights

This project was built with a strong focus on enterprise-grade patterns and clean code principles:

- **🔄 Asynchronous Polling Engine:** AI Video generation via LLMs (like Kling or Seedance) takes time. The app implements a robust, non-blocking `async/await` polling mechanism to check job statuses without freezing the UI thread.
- **🏛️ MVVM Architecture:** Built strictly on Model-View-ViewModel (MVVM) principles. This ensures a clear separation of concerns between business logic, API calls, and the Avalonia XAML frontend, making the codebase highly testable and maintainable.
- **🔐 Secure Local Credential Management:** API keys are *never* hardcoded or required via `.env` files. They are managed securely within the user's local `AppData` directory, completely mitigating the risk of accidental Git commits.
- **⚙️ CI/CD & Automation:** Integrated with **GitHub Actions**. Every push to the `main` branch triggers an automated build pipeline that compiles the application and generates the final Windows `.exe` artifact automatically.
- **🗂️ Direct Disk I/O:** Bypasses browser download dialogs by streaming generated media directly to the local filesystem.

## 📥 Download (Pre-Compiled)

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
