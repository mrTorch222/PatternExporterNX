# PatternExporterNX — исходное состояние и проверка оформления форка

Проверка выполнена 7 сентября 2026 года на Windows x64.

## Происхождение

- Upstream: https://github.com/isinicyn/FlatPatternExporter, ветка `master`.
- Исходный коммит: `98d5837fe9446c8705270ba5448730592e76ad73` от 12 марта 2026 года.
- История Git сохранена. До копирования проекта в рабочей папке находились только пользовательские `AGENTS.md` и `IMPLEMENTATION_PLAN.md`.
- Их требования сохранены; добавлены сведения о форке и актуальное имя решения.
- `LICENSE.txt` не изменен, исходный copyright Sinicyn Ivan Victorovich сохранен.

## Базовая сборка до изменения кода

Команда: `dotnet build .\PatternExporterNX.sln -c Release -p:Platform=x64`.

1. Установлен .NET SDK `8.0.204`. Источники NuGet в локальной конфигурации отсутствовали (`dotnet nuget list source`: «Источники не найдены»).
2. Первая сборка завершилась `NU1100`: не разрешены ClosedXML 0.105.0, Microsoft-WindowsAPICodePack-Shell 1.1.5, netDxf.netstandard 3.0.1, Svg.Skia 3.2.1 и stdole 17.14.40260.
3. `dotnet restore .\PatternExporterNX.sln --source https://api.nuget.org/v3/index.json` успешно восстановил зависимости без изменения глобальной конфигурации NuGet.
4. Повторная сборка с `--no-restore` завершилась `NETSDK1140`: `10.0.26100.0` не является допустимой `TargetPlatformVersion` для установленного SDK. Это исходная проблема окружения, возникшая до переименования.
5. В исходном проекте interop указывал на Inventor 2026. На машине установлен Inventor 2027.1 с interop `31.10.26700.0`.

После фиксации исходных ошибок установлен официальный .NET SDK `8.0.424` x64. SHA-512 и цифровая подпись Microsoft проверены до установки.

## Проверки оформления форка

- `dotnet build .\PatternExporterNX.sln -c Release -p:Platform=x64 --no-restore`: та же исходная ошибка `NETSDK1140`.
- `dotnet build .\FlatPatternExporter.Updater\FlatPatternExporter.Updater.csproj -c Release --no-restore`: успешно, 0 предупреждений и 0 ошибок; получен `PatternExporterNX.Updater.exe`.
- В выходной папке обновлятора проверены побайтные копии `LICENSE.txt` и `NOTICE.md`.
- Проверены синтаксис измененных XML/XAML/JSON, соответствие новых ключей ресурсов на русском и английском, кодировки и CRLF.
- Проверены новое имя сборок, сохранение пространства имен ресурсов и согласованность имен архивов с обновлятором.
- Исходный текст MIT License и copyright сопоставлены с исходным коммитом; `git diff -- LICENSE.txt` пуст.
- `git diff --check`: без ошибок.
- DXF-экспортер, оптимизатор и сериализация настроек не изменены.

## Эталоны Inventor 2027

Inventor 2027.1 запущен через COM. Собранное окно PatternExporterNX открылось и подключилось к Inventor. Через существующий `DxfExporter` созданы три изолированных эталона в `TestData/Baseline/Inventor2027`:

- `rectangle-hole-mm.dxf`: `$INSUNITS=4`, `$MEASUREMENT=1`, координаты X 0–100 мм, Y 0–50 мм, четыре `LINE` и один `CIRCLE`; расчетная длина 362,831853 мм.
- `rectangle-hole-m.dxf`: `$INSUNITS=6`, `$MEASUREMENT=1`, координаты X 0–0,1 м, Y 0–0,05 м, та же геометрия; расчетная длина после явного перевода 362,831853 мм.
- `spline-control-mm.dxf`: `$INSUNITS=4`, `$MEASUREMENT=1`, три `LINE`, один `CIRCLE`, один control-point `SPLINE`: flags 8, degree 3, 4 control points, 0 fit points; расчетная длина 368,752488 мм.

Во всех трех случаях экспорт завершился со статусом Success, а `UnitsOfMeasure.LengthUnits` до и после экспорта совпал. Полный машинно-читаемый отчет находится в `inspection.json` рядом с эталонами.

## Выполненный этап 1

- SDK закреплен в `global.json`, а nuget.org — в репозиторном `NuGet.Config`.
- Путь сборки задается свойством `InventorInstallDir`, по умолчанию Inventor 2027. При отсутствующем interop MSBuild выдает одну понятную ошибку до компиляции.
- `Private=false`; Autodesk DLL отсутствует в output. Runtime resolver успешно загрузил interop из установленного Inventor и загрузил все 227 типов приложения.
- Release x64: успешно, 0 предупреждений и 0 ошибок.

## Непроверенное и следующий этап

Визуальный preview и импорт в реальную программу резки пока не проверены.

На момент снятия этого baseline преобразование сплайнов, максимальная ошибка преобразования и расчет длины реза еще не были реализованы и измерены. Результаты после реализации приведены в `IMPLEMENTATION_PLAN.md` и отчете соответствующего коммита. Геометрия DXF автоматически не масштабируется.

Выпуск всех архивов и установка обновлений требуют последующей проверки. До первого выпуска форка GitHub Releases API может возвращать 404 при проверке обновлений; релизы исходного проекта не используются.

Дальнейшая работа: выполнить этапы 2–6 из `IMPLEMENTATION_PLAN.md`. Компиляция, COM-подключение и базовый экспорт подтверждены, но приемка целевой программой резки еще не выполнена.
