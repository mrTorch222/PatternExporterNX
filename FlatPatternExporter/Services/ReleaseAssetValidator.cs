using System.IO;
using System.Security.Cryptography;
using FlatPatternExporter.Models;

namespace FlatPatternExporter.Services;

public static class ReleaseAssetValidator
{
    public static void Validate(string path, ReleaseAsset asset)
    {
        var actualSize = new FileInfo(path).Length;
        if (actualSize == 0 || asset.Size > 0 && actualSize != asset.Size)
        {
            throw new InvalidDataException(
                $"Downloaded archive size mismatch for {asset.Name}: expected {asset.Size}, received {actualSize}.");
        }

        if (string.IsNullOrWhiteSpace(asset.Digest))
        {
            return;
        }

        var digestParts = asset.Digest.Split(':', 2);
        if (digestParts.Length != 2 || !digestParts[0].Equals("sha256", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Unsupported release digest for {asset.Name}: {asset.Digest}");
        }

        using var stream = File.OpenRead(path);
        var actualDigest = Convert.ToHexString(SHA256.HashData(stream));
        if (!actualDigest.Equals(digestParts[1], StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Downloaded archive checksum mismatch for {asset.Name}.");
        }
    }
}
