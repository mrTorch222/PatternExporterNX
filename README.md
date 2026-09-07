# PatternExporterNX

> Export sheet-metal flat patterns from Autodesk Inventor to DXF with customizable workflows.

## Происхождение и статус форка / Fork attribution

**PatternExporterNX** — независимый форк [FlatPatternExporter](https://github.com/isinicyn/FlatPatternExporter), который сопровождает [mrTorch222](https://github.com/mrTorch222). Автор исходного проекта — **Sinicyn Ivan Victorovich (isinicyn)**. История Git и исходный copyright сохранены; форк распространяется по **MIT License**. Это самостоятельная ветка развития, без заявления об одобрении со стороны исходного автора или Autodesk.

**PatternExporterNX is an independent fork of isinicyn/FlatPatternExporter, maintained by mrTorch222.** Original author: Sinicyn Ivan Victorovich. The original Git history, copyright notice and MIT License are preserved. See [LICENSE.txt](LICENSE.txt) and [NOTICE.md](NOTICE.md); include both in source and binary distributions.

- Репозиторий форка / Fork: [mrTorch222/PatternExporterNX](https://github.com/mrTorch222/PatternExporterNX).
- Ошибки и предложения по форку / Fork issues: [Issues](https://github.com/mrTorch222/PatternExporterNX/issues).
- Подготовлены новое имя приложения и выпусков, сведения об авторстве и канал обновлений форка.
- Inventor 2027, FitPoints, длина реза и единицы документа — **планируемые доработки**, см. [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md). Их реализация и приемка не заявляются завершенными.
- Ограничения исходной сборки и ручной проверки: [FORK_BASELINE.md](FORK_BASELINE.md).

## Overview
PatternExporterNX is a standalone WPF utility that connects to a running Autodesk Inventor session and automates flat-pattern exports for sheet-metal parts. The tool scans assemblies or parts, resolves conflicts, and produces DXF files with predictable naming, layer configuration, and optional previews. Settings, tokens, and UI preferences persist between sessions so teams can standardize their export pipeline.

## Highlights
- Connects to Autodesk Inventor through the COM API and validates the active document before processing.
- Scans assemblies using BOM or occurrence traversal, with filters for reference, phantom, purchased, and library components.
- Tracks part metadata in a document cache and resolves duplicate item numbers via the conflict analyzer.
- Exports DXF files with customizable layer mapping, AutoCAD version targeting, polylines merging, spline replacement, geometry rebasing, and optional DXF optimization.
- Builds file names from tokenized templates (including custom text and user-defined iProperties) and organizes output by material, thickness, or custom subfolders.
- Generates thumbnails for parts and exported DXF previews to aid validation. Uses a dual-method approach: ApprenticeServer API (primary, faster) with automatic fallback to Windows Shell API if ApprenticeServer is unavailable.
- Persists UI layout, column order, presets, themes, and localization preferences in `%AppData%\FlatPatternExporter\settings.json`.
- Ships with English and Russian UI resources plus a light/dark theme switcher.

## Development
The upstream project documents development with assistance from [Claude Code](https://claude.ai/code). The initial fork setup and branding were prepared with AI coding assistance. Original authorship remains attributed to the upstream author.

## System Requirements
- Windows 10/11 x64
- .NET 8.0 Desktop Runtime (or Visual Studio 2022 with .NET workload for development)
- Autodesk Inventor installed locally. The fork targets Inventor 2027 as its development goal; the inherited interop reference still points to Inventor 2026 pending the planned migration. No Inventor compatibility testing of this fork has been completed
- Git in `PATH` if you want build numbers populated by the MSBuild `SetVersionInfo` target
- ApprenticeServer (optional) – recommended for faster thumbnail generation; Windows Shell API is used automatically if ApprenticeServer is unavailable

## Getting Started
Clone the fork and choose the workflow that fits your environment.
```powershell
git clone https://github.com/mrTorch222/PatternExporterNX.git
cd PatternExporterNX
git remote add upstream https://github.com/isinicyn/FlatPatternExporter.git
```
The solution and output executable use the new product name. Source directories and namespaces retain `FlatPatternExporter` to keep the fork easy to compare with upstream. The existing settings path is also retained; upstream and fork currently share that settings file when run under the same Windows account.

### Build with Visual Studio
1. Install Visual Studio 2022 with the `.NET desktop development` workload.
2. Open `PatternExporterNX.sln` and restore NuGet packages (`netDxf.netstandard`, `Svg.Skia`, `Microsoft-WindowsAPICodePack-Shell`, `ClosedXML`, `stdole`).
3. Ensure the reference to `Autodesk.Inventor.Interop.dll` in `FlatPatternExporter/FlatPatternExporter.csproj` points to your Inventor installation (update the `HintPath` if necessary).
4. Set the solution platform to `x64` (runtime identifier `win-x64`) and build.
5. Start Autodesk Inventor, open the target assembly or part, then run the application from Visual Studio (`F5`).

### Build with dotnet CLI
```bash
dotnet restore PatternExporterNX.sln --source https://api.nuget.org/v3/index.json
dotnet build PatternExporterNX.sln -c Release -p:Platform=x64 --no-restore
```
Use a .NET 8 SDK that supports the inherited `net8.0-windows10.0.26100.0` target. SDK 8.0.204 fails with NETSDK1140 on this machine. The inherited Inventor interop `HintPath` must match a local installation; the configurable Inventor 2027 reference is planned. Autodesk interop is marked `Private=false` and must not be copied into the repository or distributed with the application.

### Portable build / publish
Use the included publish profiles under `FlatPatternExporter/Properties/PublishProfiles` or run:
```bash
dotnet publish FlatPatternExporter/FlatPatternExporter.csproj -c Release -r win-x64 --self-contained false
```
Publish output includes `LICENSE.txt` and `NOTICE.md`; keep both with the executable, including portable and updater distributions. Complete build and manual acceptance before distributing binaries. No release of this fork has been verified yet.

### Creating GitHub Releases
For automated release creation, use the included scripts:

1. **Build all release archives:**
   ```bash
   publish.bat
   # Select option 5 (All) to create all build types
   ```
   This creates archives in `Release/` directory:
   - `PatternExporterNX-v{VERSION}-x64-Deploy.zip`
   - `PatternExporterNX-v{VERSION}-x64-Portable.zip`
   - `PatternExporterNX-v{VERSION}-x64-FrameworkDependent.zip`
   - `PatternExporterNX.Updater-v{VERSION}-x64.zip`

2. **Create draft release on GitHub:**
   ```bash
   create-release-draft.bat
   ```
   This script automatically:
   - Detects version from Git commit count
   - Creates and pushes a new tag
   - Uploads all archives to GitHub
   - Creates a draft release with placeholder notes

3. **Finalize release:**
   - Open the provided edit URL in your browser
   - Update release notes with detailed description
   - Click "Publish release" when ready

See [PUBLISH.md](PUBLISH.md) for detailed publishing documentation.

## Usage
1. Launch Autodesk Inventor and open the assembly or part you want to process.
2. Run PatternExporterNX (`PatternExporterNX.exe`). The app connects to the active Inventor session on startup.
3. Click **Scan** to analyze the document. Choose between **BOM** and **Traverse** scanning modes and adjust component filters as needed.
4. Review detected sheet-metal parts, quantities, properties, and conflict warnings in the data grid.
5. Configure export options: output folder strategy, layer presets, AutoCAD version, spline replacement, DXF optimization, file-name tokens, and thumbnail generation.
6. Click **Export** to generate DXF files (and optional previews). Progress bars report the operation status and any skipped items.
7. Use **Clear** to reset the session or adjust settings and re-export as needed.

### Export Options at a Glance
- **Component filters**: exclude reference, purchased, phantom, and library parts from the export queue.
- **Organization**: create material/thickness subfolders or route files to a custom project/workspace directory.
- **DXF formatting**: merge profiles into polylines, rebase geometry to origin, trim centerlines, and post-process DXFs with `Utilities/DxfOptimizer`.
- **Spline handling**: replace splines with lines or arcs and control tolerance.
- **Layer presets**: toggle individual layers, assign custom names, colors, and line types; save presets for later reuse.
- **File naming**: compose file names with tokens such as `{PartNumber}`, `{Material}`, `{Thickness}`, model states, user-defined properties, or `{CUSTOM:text}` segments.

## Configuration & Data Persistence
- Application settings are serialized to `%AppData%\FlatPatternExporter\settings.json`. Delete this file to reset the UI to defaults.
- Template presets, token configurations, layer overrides, and column layouts are saved per user.
- User-defined properties picked during a session are recorded in `PropertyMetadataRegistry.UserDefinedProperties` and become available as tokens automatically.
- The conflict analyzer stores part occurrences with model states to highlight duplicate identifiers before export.

## Project Layout
- `FlatPatternExporter/`
  - `Core/` – Inventor integration, document scanning, caching, DXF export, thumbnail generation.
  - `Services/` – property metadata registry, token engine, settings persistence, version info, localization.
  - `UI/` – WPF windows, controls, helpers, and view models for the main user experience.
  - `Models/` – export options, scan results, conflict data, layer settings, and progress models.
  - `Libraries/` – standalone helpers (DXF renderer, COM marshal core, tooltip notifications).
  - `Utilities/` – DXF post-processing utilities.
  - `Converters/`, `Extensions/`, `Styles/`, `Resources/` – XAML infrastructure, themes, and localized strings (`Strings.resx`, `Strings.ru.resx`).

## Localization & Themes
`LocalizationManager` exposes runtime language switching between English (`en-US`) and Russian (`ru-RU`). Use the toggle in the custom title bar to switch themes. All visual styles are defined in `Styles/` to keep XAML declarative and maintainable.

## Troubleshooting
- **Cannot connect to Inventor**: ensure Inventor is running under the same user and that COM registration is intact. The app displays localized error messages when the connection fails.
- **Missing Autodesk interop**: verify the path to `Autodesk.Inventor.Interop.dll` matches your Inventor version. Different installations (e.g., 2024/2025/2026) store the assembly in version-specific folders.
- **Duplicate part numbers**: review the conflict analyzer panel after scanning. Resolve naming conflicts in Inventor or adjust token templates before exporting.
- **Incorrect DXF output**: experiment with spline replacement, geometry rebasing, and layer presets. Use the DXF preview column to confirm results quickly.
- **Thumbnail generation**: the application automatically handles thumbnail retrieval using a dual-method approach. If ApprenticeServer (Inventor's lightweight document reader) is unavailable, the app seamlessly falls back to Windows Shell API. No manual configuration is required—thumbnails will be generated using the best available method.

## License

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

This project is licensed under the **MIT License** - see the [LICENSE.txt](LICENSE.txt) file for full details.

Copyright © 2025 Sinicyn Ivan Victorovich

---

## 🇷🇺 Кратко
**PatternExporterNX**, независимый форк проекта Синицына Ивана Викторовича, подключается к Autodesk Inventor и автоматизирует экспорт разверток листового металла в DXF. Приложение сканирует сборки (BOM или обходом), фильтрует детали, выявляет конфликтующие обозначения, предлагает тонкие настройки DXF (слои, версии AutoCAD, полилинии, оптимизацию) и формирует имена файлов по токенам, включая пользовательские iProperties. Все настройки, пресеты и параметры интерфейса сохраняются в `%AppData%\FlatPatternExporter\settings.json`. Для сборки установите .NET 8, Visual Studio 2022 и убедитесь, что `Autodesk.Inventor.Interop.dll` ссылается на установленную версию Inventor. Подробные инструкции и структура проекта описаны в разделах выше.
