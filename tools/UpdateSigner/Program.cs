using System.Security.Cryptography;
using System.Text.Json;
using VBAudioMonitor;

const string keyName = "Kevinlabs26.VBAudioMonitor.Updates.v1";
if (args.Length == 2 && args[0] == "init")
{
    if (!CngKey.Exists(keyName) && File.Exists(args[1]))
        throw new InvalidOperationException("A pinned public key already exists; this Windows account must use the original private identity, not generate a replacement.");
    using var cng = CngKey.Exists(keyName) ? CngKey.Open(keyName) :
        CngKey.Create(CngAlgorithm.ECDsaP256, keyName, new CngKeyCreationParameters { ExportPolicy = CngExportPolicies.None });
    using var key = new ECDsaCng(cng);
    string publicKey = key.ExportSubjectPublicKeyInfoPem();
    if (File.Exists(args[1]) && File.ReadAllText(args[1]).Trim() != publicKey.Trim())
        throw new InvalidOperationException("Existing public key differs; key rotation requires a separately reviewed release.");
    File.WriteAllText(args[1], publicKey);
    Console.WriteLine("Public key written; private key remains non-exportable in the current Windows user's CNG store.");
    return;
}
if (args.Length != 3 || args[0] != "sign")
    throw new ArgumentException("Use: init <public-key.pem> OR sign <release-directory> <public-key.pem>");
if (!CngKey.Exists(keyName)) throw new InvalidOperationException("Signing key not found on this Windows account; do not replace the pinned public key.");
using var storedKey = CngKey.Open(keyName);
using var signer = new ECDsaCng(storedKey);
if (File.ReadAllText(args[2]).Trim() != signer.ExportSubjectPublicKeyInfoPem().Trim())
    throw new InvalidOperationException("Signing identity does not match the application's pinned key.");
using var feed = JsonDocument.Parse(File.ReadAllText(Path.Combine(args[1], "releases.win.json")));
foreach (var asset in feed.RootElement.GetProperty("Assets").EnumerateArray())
{
    string version = asset.GetProperty("Version").GetString()!;
    string name = asset.GetProperty("FileName").GetString()!;
    if (asset.GetProperty("PackageId").GetString() != "VBAudioMonitor"
        || asset.GetProperty("Type").GetString() != "Full" || name != $"VBAudioMonitor-{version}-full.nupkg"
        || name.IndexOfAny(['/', '\\', '\r', '\n']) >= 0)
        throw new InvalidDataException("Unexpected release identity.");
    using var package = File.OpenRead(Path.Combine(args[1], name));
    string hash = Convert.ToHexString(SHA256.HashData(package));
    long size = package.Length;
    if (size != asset.GetProperty("Size").GetInt64() || hash != asset.GetProperty("SHA256").GetString()!.ToUpperInvariant())
        throw new InvalidDataException("Release feed differs from package.");
    byte[] signature = signer.SignData(UpdateProof.Payload(version, name, size, hash), HashAlgorithmName.SHA256,
        DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    if (!UpdateProof.Verify(version, name, size, hash, signature, File.ReadAllText(args[2])))
        throw new CryptographicException("Signature self-check failed.");
    File.WriteAllBytes(Path.Combine(args[1], name + ".sig"), signature);
    Console.WriteLine("Signed and verified: " + name);
}
