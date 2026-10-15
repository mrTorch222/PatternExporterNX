using System.IO;
using System.Windows.Media.Imaging;
using FlatPatternExporter.Core;
using FlatPatternExporter.Features.Acceptance.Models;
using Inventor;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace FlatPatternExporter.Features.Acceptance.Services;

public sealed class AcceptancePreviewService(InventorManager inventorManager)
{
    public Task<BitmapImage> RenderAsync(AcceptanceSheetItem item, int width = 900, int height = 650,
        CancellationToken cancellationToken = default) =>
        StaTaskRunner.RunAsync(() => Render(item, width, height), cancellationToken);

    private BitmapImage Render(AcceptanceSheetItem item, int width, int height)
    {
        if (!inventorManager.EnsureInventorConnection())
            throw new InvalidOperationException("Autodesk Inventor is not available.");

        var app = inventorManager.Application!;
        var documentWasOpen = HasOpenDocument(app, item.FullFileName);
        var document = FindOpenDocument(app, item.FullFileName, item.ModelState);
        var closeAfterRender = !documentWasOpen;
        var documentPath = string.IsNullOrWhiteSpace(item.ModelState) ||
                           item.ModelState.Equals("Primary", StringComparison.OrdinalIgnoreCase)
            ? item.FullFileName
            : $"{item.FullFileName}<{item.ModelState}>";
        document ??= app.Documents.Open(documentPath, false);

        var temporaryFile = IOPath.Combine(IOPath.GetTempPath(), $"PatternExporterNX_{Guid.NewGuid():N}.png");
        try
        {
            var scene = document switch
            {
                PartDocument part => (object)part.ComponentDefinition,
                AssemblyDocument assembly => assembly.ComponentDefinition,
                _ => throw new InvalidOperationException("Only part and assembly documents can be rendered.")
            };

            var camera = app.TransientObjects.CreateCamera();
            camera.SceneObject = scene;
            camera.ViewOrientationType = ViewOrientationTypeEnum.kIsoTopRightViewOrientation;
            camera.Perspective = item.Perspective;
            camera.PerspectiveAngle = item.PerspectiveAngle * Math.PI / 180.0;
            camera.Fit();
            Rotate(camera, app.TransientGeometry, item.Yaw, item.Pitch);
            camera.Fit();

            var options = app.TransientObjects.CreateNameValueMap();
            options.Add("TransparentBackground", true);
            camera.SaveAsBitmapWithOptions(temporaryFile, width, height, options);
            return LoadFrozenBitmap(temporaryFile);
        }
        finally
        {
            if (closeAfterRender) document.Close(true);
            try { IOFile.Delete(temporaryFile); } catch { }
        }
    }

    private static Document? FindOpenDocument(Inventor.Application app, string fileName, string modelState)
    {
        foreach (Document document in app.Documents)
        {
            if (!string.Equals(document.FullFileName, fileName, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.IsNullOrWhiteSpace(modelState) ||
                string.Equals(document.ModelStateName, modelState, StringComparison.OrdinalIgnoreCase)) return document;
        }
        return null;
    }

    private static bool HasOpenDocument(Inventor.Application app, string fileName)
    {
        foreach (Document document in app.Documents)
            if (string.Equals(document.FullFileName, fileName, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void Rotate(Camera camera, TransientGeometry geometry, double yawDegrees, double pitchDegrees)
    {
        if (Math.Abs(yawDegrees) < 0.01 && Math.Abs(pitchDegrees) < 0.01) return;
        var target = camera.Target;
        var eye = camera.Eye;
        var vector = geometry.CreateVector(eye.X - target.X, eye.Y - target.Y, eye.Z - target.Z);
        var matrix = geometry.CreateMatrix();
        matrix.SetToRotation(yawDegrees * Math.PI / 180.0, geometry.CreateVector(0, 0, 1), target);
        eye.TransformBy(matrix);
        var right = geometry.CreateVector(camera.UpVector.Y * vector.Z - camera.UpVector.Z * vector.Y,
            camera.UpVector.Z * vector.X - camera.UpVector.X * vector.Z,
            camera.UpVector.X * vector.Y - camera.UpVector.Y * vector.X);
        if (right.Length > 0.000001)
        {
            right.Normalize();
            matrix.SetToRotation(pitchDegrees * Math.PI / 180.0, right, target);
            eye.TransformBy(matrix);
        }
        camera.Eye = eye;
    }

    private static BitmapImage LoadFrozenBitmap(string path)
    {
        var bytes = IOFile.ReadAllBytes(path);
        using var stream = new MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
