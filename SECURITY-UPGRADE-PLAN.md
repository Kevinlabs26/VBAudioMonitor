# Remaining security fixes

Historical implementation decision record: after reviewing this concrete plan and its key-loss tradeoff, the developer delegated the remaining implementation decision. The runtime migration and independent update authentication were implemented and published in 1.2.0. The details below retain that selected plan; the 1.2.1 UI patch uses the same runtime and signing identity.

## Selected runtime migration

- Change the application from net8.0-windows to net10.0-windows and select SDK 10.0.401.
- Package the self-contained Windows x64 application with runtime 10.0.12, as reported by the [official release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json) retrieved for this continuation.
- Use local candidate version 1.2.0; do not replace the public 1.1.0 release or publish without a release request.
- Retain existing NAudio/Velopack package versions and the approved ImageSharp exception. Refresh the lock file for the new target.
- Update build/license selection and the English README. Test preferences compatibility, all languages/themes/window sizes, COM/audio-provider models, update download/tamper rejection, installation-package structure and private-file exclusion.

The official SDK archive was downloaded, SHA-512 checked against Microsoft's metadata and extracted inside ignored .sdk/. The system SDK and running installed app were not changed. First-party compilation also excludes this local SDK directory. No real audio stream or installer has been launched during this continuation.

Review both [.NET 9 compatibility changes](https://learn.microsoft.com/en-us/dotnet/core/compatibility/9.0) and [.NET 10 compatibility changes](https://learn.microsoft.com/en-us/dotnet/core/compatibility/10). Real audio quality, mixed-monitor DPI movement and a full live installation/update cycle require separate verification; model/UI checks cannot prove them.

## Selected independent update authentication

Use standard ECDSA P-256 with SHA-256 and a public key embedded in the application. No new cryptography package, custom cipher, certificate purchase or external signing service is needed.

The signing payload binds a format identifier, application identity, version, exact full-package filename, byte length and SHA-256. Each full update package receives a detached 64-byte signature. The signing tool checks that feed metadata matches the package and self-verifies its output before packaging is considered successful. Upload the signature along with the package and existing feed files.

Enforcement must cover all three entry points:

1. Validate metadata and signature before accepting downloaded package bytes, then validate those bytes before returning control to Velopack.
2. Revalidate a cached package and its signature before marking it ready after startup.
3. Revalidate immediately before asking the native updater to apply it.

This placement matters: reviewed Velopack 1.2.0 DownloadUpdatesAsync extracts a new Update.exe in its download-finalization block. Checking only after this call returns is too late to establish the updater's identity. The update source must verify package bytes before Velopack's extraction. Full packages must be used; unsigned deltas must not reach native patch processing.

Missing, malformed, wrong-key or changed signatures must fail closed. Modified metadata, filenames, versions, packages and cached proofs must be rejected. Signature downloads must be bounded in size/time; feed filenames must be validated before local file operations. Keep Velopack's existing SHA-256 checks as an additional integrity check.

## Signing identity and key-loss tradeoff

Selected identity: a new, non-exportable, per-user ECDSA key in this Windows user's CNG key store, named Kevinlabs26.VBAudioMonitor.Updates.v1. Only its public key enters Git/public downloads. Building a release fails if the private identity is absent or differs from the pinned public key; builds never silently generate a replacement.

This protects the update trust root from a compromise limited to GitHub publishing credentials. It does not protect against malware or malicious build scripts executing as the signing Windows user; those could ask CNG to sign even without exporting the key.

**Loss of this Windows profile/key store means loss of the signing identity.** A non-exportable key cannot be backed up as a private-key file. If it is lost, existing clients cannot authenticate an ordinary release signed by a replacement key; a separately trusted installer/manual reinstall is required unless a reviewed rotation protocol is introduced. This is the significant tradeoff requiring explicit approval. Alternative: use a separately managed exportable key with encrypted offline backup, hardware signer or signing service; those need an agreed storage/recovery policy.

This update-proof mechanism does not add an Authenticode signature to Setup/app binaries or remove SmartScreen warnings. That requires a separately chosen publisher certificate/service.

## Initial trust and delivery

The public 1.1.0 application has no pinned publisher key. New code cannot retroactively secure its first upgrade. A trusted first installation of 1.2.0 establishes the new key; future updates can then be authenticated independently of GitHub. Existing cached unsigned updates must not be accepted by the new implementation.

The controlled ImageSharp exception remains a mitigation, not a repaired tool dependency. No automatic release or key-rotation monitoring is introduced by this plan.
