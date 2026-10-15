using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DxfRenderer;
using FlatPatternExporter.Converters;
using FlatPatternExporter.Services;
using Inventor;
using Microsoft.WindowsAPICodePack.Shell;
using Svg.Skia;

namespace FlatPatternExporter.Core;

public class ThumbnailGenerator
{
    private static readonly Guid InventorSummaryInformationId = new("3D38DE39-0588-4C14-BB37-18F4D5DD31C7");
    private const int PidThumbnail = 0x11;
    private const ushort VtClipboardData = 71;
    private const int ThumbnailDecodeWidth = 256;

    private readonly ApprenticeServerComponent? _apprentice = CreateApprenticeServer();

    private static ApprenticeServerComponent? CreateApprenticeServer()
    {
        try
        {
            return new ApprenticeServerComponent();
        }
        catch
        {
            return null;
        }
    }
    /// <summary>
    /// Converts SVG string to BitmapImage
    /// </summary>
    public static BitmapImage? ConvertSvgToBitmapImage(string svgContent)
    {
        if (string.IsNullOrEmpty(svgContent))
            return null;

        try
        {
            var svg = new SKSvg();
            var svgDocument = svg.FromSvg(svgContent);

            if (svgDocument == null)
                return null;

            var bounds = svgDocument.CullRect;

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return null;

            using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo((int)bounds.Width, (int)bounds.Height));
            if (surface?.Canvas == null)
                return null;

            surface.Canvas.Clear(SkiaSharp.SKColors.White);
            surface.Canvas.DrawPicture(svgDocument);
            surface.Canvas.Flush();

            using var image = surface.Snapshot();
            using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = new MemoryStream(data.ToArray());
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public BitmapImage? GenerateDxfThumbnails(string dxfFilePath, Dispatcher dispatcher)
    {
        if (!System.IO.File.Exists(dxfFilePath)) return null;

        try
        {
            var generator = new DxfThumbnailGenerator();
            var svg = generator.GenerateSvg(dxfFilePath);

            BitmapImage? bitmapImage = null;

            // Convert SVG to BitmapImage
            dispatcher.Invoke(() =>
            {
                bitmapImage = ConvertSvgToBitmapImage(svg);
            });

            return bitmapImage;
        }
        catch (Exception ex)
        {
            dispatcher.Invoke(() =>
            {
                UserDialogService.Show(LocalizationManager.Instance.GetString("Error_ThumbnailGeneration", ex.Message), LocalizationManager.Instance.GetString("Error_Title"), UserDialogButtons.Ok,
                    UserDialogIcon.Error);
            });
            return null;
        }
    }

    public async Task<BitmapImage?> GetThumbnailAsync(PartDocument document, Dispatcher dispatcher)
    {
        if (ReadEmbeddedPng(document.FullFileName) is { } embeddedPng)
            return CreateFrozenBitmap(embeddedPng, ThumbnailDecodeWidth);

        try
        {
            var inventorThumbnail = await GetThumbnailViaInventorAsync(document, dispatcher);
            if (inventorThumbnail is not null) return inventorThumbnail;
        }
        catch
        {
            // Fall back to the thumbnail embedded in the saved file.
        }

        if (_apprentice is not null)
        {
            try
            {
                var apprenticeThumbnail = await GetThumbnailViaApprenticeAsync(document, dispatcher);
                if (apprenticeThumbnail is not null) return apprenticeThumbnail;
            }
            catch
            {
                // Fallback to Shell API
            }
        }

        try
        {
            return await GetThumbnailViaShellAsync(document, dispatcher);
        }
        catch (Exception ex)
        {
            dispatcher.Invoke(() =>
            {
                UserDialogService.Show(LocalizationManager.Instance.GetString("Error_ThumbnailObtaining", ex.Message), LocalizationManager.Instance.GetString("Error_Title"), UserDialogButtons.Ok,
                    UserDialogIcon.Error);
            });
            return null;
        }
    }

    private static BitmapImage CreateFrozenBitmap(byte[] imageData, int decodePixelWidth = 0)
    {
        using var stream = new MemoryStream(imageData);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = stream;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = decodePixelWidth;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[]? ReadEmbeddedPng(string filePath)
    {
        NativeMethods.IPropertySetStorage? storage = null;
        NativeMethods.IPropertyStorage? propertyStorage = null;
        var values = new NativeMethods.PropVariant[1];

        try
        {
            NativeMethods.StgOpenStorage(
                filePath,
                IntPtr.Zero,
                NativeMethods.StgmRead | NativeMethods.StgmShareDenyWrite,
                IntPtr.Zero,
                0,
                out storage);
            var formatId = InventorSummaryInformationId;
            storage.Open(ref formatId, NativeMethods.StgmRead | NativeMethods.StgmShareExclusive, out propertyStorage);
            propertyStorage.ReadMultiple(
                1,
                [new NativeMethods.PropSpec { Kind = NativeMethods.PropertySpecificationId, PropertyId = PidThumbnail }],
                values);

            if (values[0].VariantType != VtClipboardData || values[0].Pointer == IntPtr.Zero) return null;

            const int clipFormatSize = 4;
            const int headerSize = 12;
            var clipData = Marshal.PtrToStructure<NativeMethods.ClipData>(values[0].Pointer);
            var imageSize = (int)clipData.Size - clipFormatSize - headerSize;
            if (imageSize <= NativeMethods.PngSignature.Length || clipData.Data == IntPtr.Zero) return null;

            var image = new byte[imageSize];
            Marshal.Copy(clipData.Data + headerSize, image, 0, imageSize);
            return image.AsSpan().StartsWith(NativeMethods.PngSignature) ? image : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            NativeMethods.PropVariantClear(ref values[0]);
            if (propertyStorage is not null) Marshal.ReleaseComObject(propertyStorage);
            if (storage is not null) Marshal.ReleaseComObject(storage);
        }
    }

    private static async Task<BitmapImage?> GetThumbnailViaInventorAsync(PartDocument document, Dispatcher dispatcher)
    {
        BitmapImage? bitmap = null;
        await dispatcher.InvokeAsync(() => bitmap = ConvertPictureToBitmapImage(document.Thumbnail));
        return bitmap;
    }

    private Task<BitmapImage?> GetThumbnailViaApprenticeAsync(PartDocument document, Dispatcher dispatcher)
    {
        var tcs = new TaskCompletionSource<BitmapImage?>();

        dispatcher.InvokeAsync(() =>
        {
            ApprenticeServerDocument? apprenticeDoc = null;
            try
            {
                apprenticeDoc = _apprentice!.Open(document.FullDocumentName);
                tcs.SetResult(ConvertPictureToBitmapImage(apprenticeDoc.Thumbnail));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
            finally
            {
                apprenticeDoc?.Close();
            }
        });

        return tcs.Task;
    }

    private static BitmapImage? ConvertPictureToBitmapImage(stdole.IPictureDisp thumbnail)
    {
        using var image = IPictureDispConverter.PictureDispToImage(thumbnail);
        if (image is null) return null;

        using var memoryStream = new MemoryStream();
        image.Save(memoryStream, ImageFormat.Png);
        memoryStream.Position = 0;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = memoryStream;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private async Task<BitmapImage?> GetThumbnailViaShellAsync(PartDocument document, Dispatcher dispatcher)
    {
        BitmapImage? bitmap = null;
        await dispatcher.InvokeAsync(() =>
        {
            var filePath = document.FullFileName;

            if (!System.IO.File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            using var shellFile = ShellFile.FromFilePath(filePath);
            using var thumbnail = shellFile.Thumbnail.ExtraLargeBitmap;

            if (thumbnail != null)
                using (var memoryStream = new MemoryStream())
                {
                    thumbnail.Save(memoryStream, ImageFormat.Png);
                    memoryStream.Position = 0;

                    bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = memoryStream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }
        });

        return bitmap;
    }

    private static class NativeMethods
    {
        public const uint StgmRead = 0x00000000;
        public const uint StgmShareExclusive = 0x00000010;
        public const uint StgmShareDenyWrite = 0x00000020;
        public const uint PropertySpecificationId = 1;
        public static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        [DllImport("ole32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void StgOpenStorage(
            string name,
            IntPtr priority,
            uint mode,
            IntPtr exclude,
            uint reserved,
            [MarshalAs(UnmanagedType.Interface)] out IPropertySetStorage storage);

        [DllImport("ole32.dll")]
        public static extern int PropVariantClear(ref PropVariant value);

        [ComImport, Guid("0000013A-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IPropertySetStorage
        {
            void Create(ref Guid formatId, IntPtr classId, uint flags, uint mode, out IntPtr propertyStorage);
            void Open(ref Guid formatId, uint mode, out IPropertyStorage propertyStorage);
        }

        [ComImport, Guid("00000138-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IPropertyStorage
        {
            void ReadMultiple(
                uint count,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] PropSpec[] specifications,
                [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] PropVariant[] values);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PropSpec
        {
            public uint Kind;
            public IntPtr PropertyId;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        public struct PropVariant
        {
            [FieldOffset(0)] public ushort VariantType;
            [FieldOffset(8)] public IntPtr Pointer;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ClipData
        {
            public uint Size;
            public int Format;
            public IntPtr Data;
        }
    }
}
