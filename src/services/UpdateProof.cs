using System.Security.Cryptography;
using System.Text;

namespace VBAudioMonitor;

internal static class UpdateProof
{
    internal static byte[] Payload(string version, string fileName, long size, string sha256) =>
        Encoding.UTF8.GetBytes($"VBAudioMonitor-update-v1\n{version}\n{fileName}\n{size.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n{sha256.ToUpperInvariant()}");

    internal static string PublicKey
    {
        get
        {
            using var stream = typeof(UpdateProof).Assembly.GetManifestResourceStream("VBAudioMonitor.assets.update-public-key.pem")!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    internal static bool Verify(string version, string fileName, long size, string sha256, byte[] signature, string publicKey)
    {
        if (signature.Length != 64) return false;
        using var key = ECDsa.Create();
        key.ImportFromPem(publicKey);
        return key.VerifyData(Payload(version, fileName, size, sha256), signature, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    internal static bool VerifyFile(string version, string fileName, string path, byte[] signature)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length <= 0 || stream.Length > 1024L * 1024 * 1024) return false;
        string hash = Convert.ToHexString(SHA256.HashData(stream));
        return Verify(version, fileName, stream.Length, hash, signature, PublicKey);
    }
}
