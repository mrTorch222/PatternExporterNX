# Architecture boundaries

PatternExporterNX is a standalone WPF application with two independent Autodesk Inventor workflows. The source layout defines the ownership boundary while compatibility namespaces remain unchanged.

## Modules

- `UI` owns the WPF shell, windows, shared controls, themes, and UI adapters.
- `Features/SheetMetal` owns assembly scanning for sheet-metal parts, flat-pattern DXF export, bend analysis and annotations, cut-length calculation, and DXF post-processing.
- `Features/Frame` owns Frame Generator scanning, file naming, BOM output, and IGES, STEP, SAT, and STL export.
- `Core`, `Models`, `Services`, and `Utilities` contain code shared by both workflows, including Inventor connection management, settings, localization, update support, and common UI-independent contracts.

## Dependency rules

1. UI may depend on shared code and both feature modules.
2. Feature backend code may depend on shared code.
3. Shared code must not depend on feature implementations or UI implementations.
4. Frame and SheetMetal feature code must not depend on each other.
5. User interaction requested by backend code goes through `IUserDialogService`; WPF owns its implementation.
6. Changes needed by more than one feature are integrated into `master` before feature branches consume them.

`ArchitectureBoundaryTests` enforce the source-level dependency directions until the modules are extracted into separate projects.
