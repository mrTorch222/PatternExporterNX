# PatternExporterNX

> Export sheet-metal flat patterns from Autodesk Inventor to DXF with customizable workflows.

## Происхождение и статус форка / Fork attribution

**PatternExporterNX** — независимый форк [FlatPatternExporter](https://github.com/isinicyn/FlatPatternExporter), который сопровождает [mrTorch222](https://github.com/mrTorch222). Автор исходного проекта — **Sinicyn Ivan Victorovich (isinicyn)**. История Git и исходный copyright сохранены; форк распространяется по **MIT License**. Это самостоятельная ветка развития, без заявления об одобрении со стороны исходного автора или Autodesk.

**PatternExporterNX is an independent fork of isinicyn/FlatPatternExporter, maintained by mrTorch222.** Original author: Sinicyn Ivan Victorovich. The original Git history, copyright notice and MIT License are preserved. See [LICENSE.txt](LICENSE.txt) and [NOTICE.md](NOTICE.md); include both in source and binary distributions.

- Репозиторий форка / Fork: [mrTorch222/PatternExporterNX](https://github.com/mrTorch222/PatternExporterNX).
- Ошибки и предложения по форку / Fork issues: [Issues](https://github.com/mrTorch222/PatternExporterNX/issues).
- Подготовлены новое имя приложения и выпусков, сведения об авторстве и канал обновлений форка.
- Сборка переведена на Inventor 2027. Реализованы FitPoints, диагностическая колонка единиц документа и длина реза в мм/м; импорт в конкретную программу резки еще требует внешней приемки, см. [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md).
- Ограничения исходной сборки и ручной проверки: [FORK_BASELINE.md](FORK_BASELINE.md).

## Overview
PatternExporterNX is a standalone WPF utility that connects to a running Autodesk Inventor session. It exports sheet-metal flat patterns to DXF and Frame Generator members to IGES. The tool scans assemblies or parts, resolves conflicts, and produces files with predictable naming. Settings, tokens, and UI preferences persist between sessions so teams can standardize their export pipeline.

## Highlights
- Connects to Autodesk Inventor through the COM API and validates the active document before processing.
- Scans assemblies using BOM or occurrence traversal, with filters for reference, phantom, purchased, and library components.
- Tracks part metadata in a document cache and resolves duplicate item numbers via the conflict analyzer.
- Exports DXF files with customizable layer mapping, AutoCAD version targeting, polylines merging, spline replacement, geometry rebasing, and optional DXF optimization.
- Builds file names from tokenized templates (including custom text and user-defined iProperties) and organizes output by material, thickness, or custom subfolders.
- Generates thumbnails for parts and exported DXF previews to aid validation. Uses a dual-method approach: ApprenticeServer API (primary, faster) with automatic fallback to Windows Shell API if ApprenticeServer is unavailable.
- Reads `Part Img` from saved-file thumbnail data without opening a new Inventor view; DXF export creates a missing sheet-metal Flat Pattern in memory before calling `FlatPattern.DataIO.WriteDataToFile`.
- Persists UI layout, column order, presets, themes, and localization preferences in `%AppData%\FlatPatternExporter\settings.json`.
- Provides a separate Frame Generator tab that finds unique frame-member IPT documents recursively, counts their occurrences, reads `G_L` in millimeters, exports them with Inventor's IGES translator, and writes the resulting BOM to Excel or CSV.
- Ships with English and Russian UI resources plus a light/dark theme switcher.

## Development
The upstream project documents development with assistance from [Claude Code](https://claude.ai/code). The initial fork setup and branding were prepared with AI coding assistance. Original authorship remains attributed to the upstream author.

## System Requirements
- Windows 10/11 x64
- .NET 8.0 Desktop Runtime; development uses .NET SDK 8.0.424 x64 (pinned in `global.json`) and Visual Studio Code or Visual Studio 2022
- Autodesk Inventor 2027 installed locally; build, external interop loading and DXF export are verified. Cutting-software acceptance remains pending
- Git in `PATH` if you want build numbers populated by the MSBuild `SetVersionInfo` target
- Standalone ApprenticeServer (optional) – recommended for reliable saved-file thumbnails; Inventor 2026 and later require the separately installed, Windows-registered Apprentice Server for external applications. Windows Shell API is used automatically if it is unavailable.

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
3. Install Inventor 2027 in its default location or pass `-p:InventorInstallDir="C:\path\to\Inventor 2027"` to MSBuild.
4. Set the solution platform to `x64` (runtime identifier `win-x64`) and build.
5. Start Autodesk Inventor, open the target assembly or part, then run the application from Visual Studio (`F5`).

### Build with dotnet CLI
```bash
dotnet restore PatternExporterNX.sln --source https://api.nuget.org/v3/index.json
dotnet build PatternExporterNX.sln -c Release -p:Platform=x64 --no-restore
```
The solution uses .NET SDK 8.0.424 (`global.json`), C# 12 and `net8.0-windows10.0.26100.0`. `NuGet.Config` provides nuget.org without changing global NuGet settings. `InventorInstallDir` defaults to `%ProgramW6432%\Autodesk\Inventor 2027`; MSBuild reports a clear error if interop is absent. At runtime, interop is loaded from the `InventorInstallDir` environment variable, the Inventor 2027 installation registry entry, or the default install directory. A command-line MSBuild property applies only to the build; use the environment variable for a custom runtime location. Autodesk interop remains `Private=false` and is not redistributed.

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

### Frame Generator members

1. Open the main Frame Generator assembly and select the **Frame Generator** tab.
2. Click **Scan frames**. Suppressed occurrences are ignored; repeated references to the same IPT are grouped and counted.
3. Choose the IGES folder and configure the file-name template. Available tokens are `{PartNumber}`, `{StockNumber}`, `{Material}`, `{Description}`, `{Length}`, `{Qty}`, and `{FileName}`.
4. Select the IGES geometry, face, and surface options, then click **Export IGES**. Files with duplicate resolved names receive `_2`, `_3`, and later suffixes.
5. Click **Export BOM** to save the displayed grouped list as `.xlsx` or UTF-8 `.csv`.

Frame detection follows Inventor's Frame Generator document interest identifier, and length is read from the `G_L` model parameter using Inventor's database units. IGES export requires a real Frame Generator assembly and the IGES Translator Add-In available in Inventor 2027.

### Export Options at a Glance
- **Component filters**: exclude reference, purchased, phantom, and library parts from the export queue.
- **Organization**: create material/thickness subfolders or route files to a custom project/workspace directory.
- **DXF formatting**: merge profiles into polylines, rebase geometry to origin, trim centerlines, and atomically post-process DXFs with `Utilities/DxfPostProcessor`.
- **Spline handling**: replace splines with lines or arcs, or preserve SPLINE entities while converting control points to fit points with a checked tolerance.
- **Diagnostics and metrics**: select document-unit and cut-length columns; cut length uses enabled outer/interior profile layers and is shown in millimeters or meters without changing DXF scale or headers.
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
- **Missing Autodesk interop**: verify that Inventor 2027 is installed and `InventorInstallDir` points to its installation root. The runtime resolves the local interop instead of requiring a redistributed DLL.
- **Duplicate part numbers**: review the conflict analyzer panel after scanning. Resolve naming conflicts in Inventor or adjust token templates before exporting.
- **Incorrect DXF output**: experiment with spline replacement, geometry rebasing, and layer presets. Use the DXF preview column to confirm results quickly.
- **Thumbnail generation**: the application reads the document's saved thumbnail, then tries standalone ApprenticeServer and Windows Shell. It never creates a new Inventor view during scanning. If the optional standalone ApprenticeServer is unavailable and Windows has no cached IPT thumbnail, `Part Img` remains empty.

## License

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

This project is licensed under the **MIT License** - see the [LICENSE.txt](LICENSE.txt) file for full details.

Copyright © 2025 Sinicyn Ivan Victorovich

---

## 🇷🇺 Кратко
**PatternExporterNX**, независимый форк проекта Синицына Ивана Викторовича, подключается к Autodesk Inventor и автоматизирует экспорт разверток листового металла в DXF. Приложение сканирует сборки (BOM или обходом), фильтрует детали, выявляет конфликтующие обозначения, предлагает тонкие настройки DXF (слои, версии AutoCAD, полилинии, оптимизацию) и формирует имена файлов по токенам, включая пользовательские iProperties. Все настройки, пресеты и параметры интерфейса сохраняются в `%AppData%\FlatPatternExporter\settings.json`. Для сборки установите .NET 8, Visual Studio 2022 и убедитесь, что `Autodesk.Inventor.Interop.dll` ссылается на установленную версию Inventor. Подробные инструкции и структура проекта описаны в разделах выше.
