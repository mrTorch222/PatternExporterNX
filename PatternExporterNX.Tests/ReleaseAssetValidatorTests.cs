using System.Security.Cryptography;
using System.Text;
using FlatPatternExporter.Models;
using FlatPatternExporter.Services;

namespace PatternExporterNX.Tests;

public sealed class ReleaseAssetValidatorTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"PatternExporterNX_Asset_{Guid.NewGuid():N}.zip");

    [Fact]
    public void Validate_AcceptsMatchingSizeAndSha256()
    {
        var payload = Encoding.UTF8.GetBytes("release payload");
        File.WriteAllBytes(_path, payload);
        var asset = new ReleaseAsset
        {
            Name = "release.zip",
            Size = payload.Length,
            Digest = $"sha256:{Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant()}"
        };

        ReleaseAssetValidator.Validate(_path, asset);
    }

    [Fact]
    public void Validate_RejectsChecksumMismatch()
    {
        File.WriteAllText(_path, "release payload");
        var asset = new ReleaseAsset
        {
            Name = "release.zip",
            Size = new FileInfo(_path).Length,
            Digest = $"sha256:{new string('0', 64)}"
        };

        Assert.Throws<InvalidDataException>(() => ReleaseAssetValidator.Validate(_path, asset));
    }

    [Fact]
    public void Validate_RejectsSizeMismatch()
    {
        File.WriteAllText(_path, "release payload");
        var asset = new ReleaseAsset { Name = "release.zip", Size = 1 };

        Assert.Throws<InvalidDataException>(() => ReleaseAssetValidator.Validate(_path, asset));
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
