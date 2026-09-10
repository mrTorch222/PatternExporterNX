using System.Buffers.Binary;
using System.Text;
using FlatPatternExporter.Enums;
using FlatPatternExporter.Features.Frame.Services;

namespace PatternExporterNX.Tests;

public sealed class FrameExportValidatorTests
{
    [Theory]
    [InlineData(FrameExportFormat.Iges)]
    [InlineData(FrameExportFormat.Step)]
    [InlineData(FrameExportFormat.Sat)]
    [InlineData(FrameExportFormat.Stl)]
    public void RejectsEmptyOutput(FrameExportFormat format)
    {
        WithTemporaryFile([], path => Assert.Throws<InvalidDataException>(() => FrameExportValidator.Validate(path, format)));
    }

    [Fact]
    public void AcceptsIgesStartRecord()
    {
        var record = Enumerable.Repeat((byte)' ', 80).ToArray();
        record[72] = (byte)'S';
        WithTemporaryFile(record, path => FrameExportValidator.Validate(path, FrameExportFormat.Iges));
    }

    [Fact]
    public void AcceptsStepHeader() => WithTemporaryFile(
        Encoding.ASCII.GetBytes("ISO-10303-21;\r\nHEADER;"),
        path => FrameExportValidator.Validate(path, FrameExportFormat.Step));

    [Fact]
    public void RejectsTruncatedBinaryStl()
    {
        var bytes = new byte[84];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(80), 2);
        WithTemporaryFile(bytes, path => Assert.Throws<InvalidDataException>(() => FrameExportValidator.Validate(path, FrameExportFormat.Stl)));
    }

    private static void WithTemporaryFile(byte[] contents, Action<string> assertion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"frame-export-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(path, contents);
            assertion(path);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
