using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.Win32;

namespace FlatPatternExporter.Utilities;

internal static class InventorAssemblyResolver
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        AssemblyLoadContext.Default.Resolving += Resolve;
    }

    private static Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
    {
        if (name.Name != "Autodesk.Inventor.Interop") return null;

        var installDir = Environment.GetEnvironmentVariable("InventorInstallDir");
        if (string.IsNullOrWhiteSpace(installDir))
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Autodesk\Inventor\RegistryVersion31.0");
            installDir = key?.GetValue("InstallLocation") as string;
        }

        if (string.IsNullOrWhiteSpace(installDir))
        {
            installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk", "Inventor 2027");
        }

        var interopPath = Path.GetFullPath(Path.Combine(installDir, "Bin", "Public Assemblies", "Autodesk.Inventor.Interop.dll"));
        if (!File.Exists(interopPath))
        {
            throw new FileNotFoundException($"Inventor 2027 interop was not found at '{interopPath}'. Install Inventor 2027 or set the InventorInstallDir environment variable.", interopPath);
        }

        return context.LoadFromAssemblyPath(interopPath);
    }
}
