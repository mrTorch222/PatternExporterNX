using System.Buffers.Binary;
using System.IO;
using System.Text;
using FlatPatternExporter.Enums;

namespace FlatPatternExporter.Features.Frame.Services;

public static class FrameExportValidator
{
    public static void Validate(string filePath, FrameExportFormat format)
    {
        var file = new FileInfo(filePath);
        if (!file.Exists || file.Length == 0)
            throw new InvalidDataException("The translator did not create an output file.");

        using var stream = File.OpenRead(filePath);
        switch (format)
        {
            case FrameExportFormat.Iges: ValidateIges(stream); break;
            case FrameExportFormat.Step: ValidateStep(stream); break;
            case FrameExportFormat.Sat: ValidateSat(stream); break;
            case FrameExportFormat.Stl: ValidateStl(stream); break;
            default: throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    private static void ValidateIges(Stream stream)
    {
        var firstRecord = ReadPrefix(stream, 80);
        if (firstRecord.Length < 80 || firstRecord[72] != (byte)'S')
            throw new InvalidDataException("The output is not a valid IGES start section.");
    }

    private static void ValidateStep(Stream stream)
    {
        var prefix = Encoding.ASCII.GetString(ReadPrefix(stream, 256)).TrimStart('\uFEFF', ' ', '\r', '\n', '\t');
        if (!prefix.StartsWith("ISO-10303-21;", StringComparison.Ordinal))
            throw new InvalidDataException("The output does not contain a STEP header.");
    }

    private static void ValidateSat(Stream stream)
    {
        var prefix = Encoding.ASCII.GetString(ReadPrefix(stream, 512));
        var firstLine = prefix.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        if (firstLine.Length < 5 || !char.IsDigit(firstLine[0]))
            throw new InvalidDataException("The output does not contain an ACIS SAT header.");
    }

    private static void ValidateStl(Stream stream)
    {
        var prefix = ReadPrefix(stream, 512);
        var ascii = Encoding.ASCII.GetString(prefix).TrimStart();
        if (ascii.StartsWith("solid", StringComparison.OrdinalIgnoreCase))
        {
            if (!ascii.Contains("facet", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The ASCII STL output contains no facets.");
            return;
        }

        if (stream.Length < 84)
            throw new InvalidDataException("The binary STL output is shorter than its header.");
        Span<byte> triangleCountBytes = stackalloc byte[4];
        stream.Position = 80;
        if (stream.Read(triangleCountBytes) != triangleCountBytes.Length)
            throw new InvalidDataException("The binary STL triangle count is missing.");
        var triangleCount = BinaryPrimitives.ReadUInt32LittleEndian(triangleCountBytes);
        var expectedLength = 84L + (50L * triangleCount);
        if (stream.Length < expectedLength)
            throw new InvalidDataException("The binary STL output is truncated.");
    }

    private static byte[] ReadPrefix(Stream stream, int length)
    {
        stream.Position = 0;
        var buffer = new byte[Math.Min(length, checked((int)Math.Min(stream.Length, int.MaxValue)))];
        _ = stream.Read(buffer, 0, buffer.Length);
        return buffer;
    }
}
