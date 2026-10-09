# Security audit — 2026-10-09

## Current remediation status — 1.2.0 security baseline

**1.2.2 release preparation:** this patch tightens the main window's button, selector and status-badge spacing and replaces the native scrollbar with a themed one. Application dependencies, runtime and update-signing identity are unchanged from the 1.2.0 audit baseline. This is a UI patch, not a new full dependency/security audit.

The developer reviewed the runtime/signing tradeoffs and delegated the remaining implementation decision. The current application targets .NET 10 Windows Forms, uses SDK 10.0.401 and bundles .NETCore/WindowsDesktop 10.0.12. Application package versions remain NAudio.Wasapi 2.2.1, NAudio.Core 2.2.1 and Velopack 1.2.0. The dependency-free update signer adds no NuGet package. The original 94 dependency/version checks plus the three new 10.0.12 runtime entries total 97 distinct checks across the two snapshots. The sections below this continuation retain the original baseline findings/evidence and do not describe the current runtime or outstanding decision status.

| ID | Category | Severity | Current location | Current status |
| --- | --- | --- | --- | --- |
| DEP-01 | Dependency | High | `build.ps1:124`, vpk ImageSharp 2.1.13 | Approved controlled exception; vulnerable tool dependency retained |
| DEP-02 | Dependency | Medium | Same tool dependency | Same approved exception; ICC call-graph coverage remains 待确认 |
| CODE-01 | Code | Low | `src/services/PreferencesStorage.cs:31` | Fixed: bounded settings |
| CODE-02 | Code | Low | `src/services/PreferencesStorage.cs:59` | Fixed: exclusive random temporary file and atomic save |
| DEP-03 | Dependency/build | Low | `VBAudioMonitor.csproj:14`, `build.ps1:20` | Fixed: locked restore, explicit win-x64 target, transitive audit |
| DEP-04 | Dependency/build | Low | `global.json:1` | Fixed: serviced SDK 10.0.401, patch roll-forward |
| KEY-01 | Secrets prevention | Low | `.gitignore:14` | Fixed: private file ignores; only the new public PEM is explicitly allowed |
| CODE-03 | Code/update trust | Medium | `src/services/SignedUpdateManager.cs:9`, `src/services/UpdateProof.cs:6`, `src/services/AppUpdates.cs:77` | Fixed for 1.2.0 clients: pinned publisher signature before extraction, after cache restore and before apply |
| DEP-05 | Dependency lifecycle | Medium | `VBAudioMonitor.csproj:4` | Fixed for application: .NET 10 LTS migration; build-tool .NET 8 host lifecycle remains separate |

**Publisher authentication.** Standard platform ECDSA P-256/SHA-256 signs a domain/application identifier, version, exact filename, byte length and SHA-256. A 64-byte detached proof accompanies each full package. The source verifies metadata before downloading and bytes before returning to Velopack, whose download-finalization code can extract Update.exe. The manager never applies unsigned deltas. Cached signatures/packages are rechecked at startup and immediately before apply. Proof reads are bounded to 64 bytes and a 30-second network deadline; package verification is bounded to 1 GiB, and filenames must match the application/version. Invalid or missing proofs fail closed.

The signing tool checks the feed against package bytes and self-verifies signatures. A non-exportable per-user CNG key named `Kevinlabs26.VBAudioMonitor.Updates.v1` was intentionally created; only its public PEM is present in the repository/application. Builds fail for missing/mismatched identities. Initialization cannot silently replace an existing pinned public identity. This protects against compromise limited to GitHub release credentials; malicious software running as the signing Windows user can still request signatures. Losing the Windows key store/profile requires a separately trusted reinstall unless a reviewed key-rotation mechanism is introduced. No private-key file or publisher certificate was exported/purchased.

**Runtime and compatibility.** Microsoft's [official release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json) supplied the SDK URL/hash; its ZIP was SHA-512 verified and extracted into ignored `.sdk/`. The system SDK and running installed app were not replaced. Compilation excludes the local SDK and signing-tool sources from the main app. Runtime-license selection now reads the actual self-contained runtime configuration. The migrated application was checked against the [.NET 9](https://learn.microsoft.com/en-us/dotnet/core/compatibility/9.0) and [.NET 10](https://learn.microsoft.com/en-us/dotnet/core/compatibility/10) compatibility lists. Eight custom-control properties now declare designer serialization behavior per [WFO1000 guidance](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/compiler-messages/wfo1000), without suppressing the analyzer or changing runtime values.

The application NuGet audit reported no applicable vulnerabilities. OSV returned no advisories for the three newly resolved 10.0.12 runtime entries. Per-package web checks were cross-checked with the Microsoft-owned [NETCore](https://www.nuget.org/packages/Microsoft.NETCore.App.Runtime.win-x64/10.0.12), [WindowsDesktop](https://www.nuget.org/packages/Microsoft.WindowsDesktop.App.Runtime.win-x64/10.0.12) and [ASP.NET Core](https://www.nuget.org/packages/Microsoft.AspNetCore.App.Runtime.win-x64/10.0.12) package pages. ASP.NET Core is still restore-only, not bundled by the app.

| Newly checked runtime package | Version | Scope | OSV result |
| --- | --- | --- | --- |
| Microsoft.NETCore.App.Runtime.win-x64 | 10.0.12 | Bundled application runtime | No applicable advisory returned |
| Microsoft.WindowsDesktop.App.Runtime.win-x64 | 10.0.12 | Bundled application runtime | No applicable advisory returned |
| Microsoft.AspNetCore.App.Runtime.win-x64 | 10.0.12 | Restore-only | No applicable advisory returned |

**Continuation checks:** .NET 10 Release build passed with zero warnings/errors; hidden UI/model/settings/signature tests passed on the self-contained candidate. Signed local-feed regressions passed for valid downloads, wrong keys/metadata, altered packages, altered package plus feed hash, missing/oversized signatures, corrupted cached proofs/packages and the apply gate. Rejected downloads preserved the existing native updater; test locators explicitly prohibit launching it. The signing tool rejected a mismatched public identity without altering the valid proof; CNG metadata confirmed ECDSA_P256 and ExportPolicy=None. Full packaging, SHA-256 feed/checksum matching, signature presence, archive whitelist/private-file checks, packaged report consistency and strong secret-pattern scans passed. Existing workspace/installed settings hashes were unchanged. No live update was applied during the audit. Version 1.2.0 was subsequently published, and its public update package/feed/signature were downloaded and passed the isolated signature regression.

**Remaining limits:** the 1.2.0 security fixes are published. Old clients cannot retroactively pin the new key: a trusted first installation of 1.2.0 or later establishes future update authentication. Setup/app binaries still lack Authenticode signatures, so initial publisher trust and SmartScreen remain separate. The vpk 1.2.0 build tool still needs a serviced .NET 8 host on the build machine; it is not the application's bundled runtime. The approved ImageSharp exception remains until upstream repair/reassessment. Remote availability can still be disrupted by withholding/breaking feeds; signature verification does not guarantee update availability. Real audio quality, mixed-monitor DPI, a complete live install/update cycle and exhaustive vendor/native-code auditing remain unverified.

## Original baseline audit (historical snapshot)

## Project and scope

Baseline: commit `62c233f`, published version 1.1.0. This report covers the current working tree, including the local fixes described below. They have not been pushed or published by this audit.

The application is C# on .NET 8 Windows Forms, built with the .NET SDK/MSBuild and NuGet. PowerShell packages the self-contained Windows x64 executable with Velopack. There is no server, database, WebView, browser extension, JavaScript package manager or GitHub Actions workflow in this repository.

Application dependencies: 2 direct packages and 1 transitive package. Packaging: `vpk` 1.2.0 plus 87 package entries in its net8.0 dependency manifest. Runtime: .NETCore and WindowsDesktop 8.0.31; ASP.NET Core 8.0.31 is present as a restore download, without an ASP.NET application/framework reference. In total, 94 package/version entries were checked. Resolved versions are listed below; before this audit, there was no committed lock file.

Review covered every first-party C# file under `src`, `Program.cs`, `Checks.cs`, `build.ps1`, project/NuGet configuration, English documentation, the ignored local publishing script, current/older build folders, package archives and the one-commit reachable Git history. Third-party native code was not exhaustively audited. Archive payload manifests and token patterns were inspected; compressed single-file executables are not equivalent to a full binary reverse-engineering audit.

Threat model: ordinary per-user desktop execution, local preferences as potentially malformed input, audio devices supplied by Windows, a fixed public HTTPS GitHub update source, and developer-controlled packaging inputs. A process already able to modify the user's installed executable or repository is outside the app's isolation boundary. The program should not be run as administrator to compensate for folder permissions.

## Findings

Severity reflects the stated exploit conditions. Dependency severity is the advisory's rating; reachability in this project is assessed separately. No confirmed Critical issue or reachable High issue in first-party application code was found. This is a point-in-time assessment, not a guarantee of absence of vulnerabilities.

| ID | Category | Severity | Location | Status |
| --- | --- | --- | --- | --- |
| DEP-01 | Dependency | High | `build.ps1:118`; vpk's ImageSharp 2.1.13 | Suggested monitoring — developer approved controlled exception; package remains vulnerable |
| DEP-02 | Dependency | Medium | Same build-tool dependency, ICC/BigTIFF parsing | Suggested monitoring — same controlled exception |
| CODE-01 | Code | Low | `src/services/PreferencesStorage.cs:31` | Fixed: bound preferences input/output to 8 MiB |
| CODE-02 | Code | Low | `src/services/PreferencesStorage.cs:59` | Fixed: exclusive random temporary file, atomic replacement, failure cleanup |
| DEP-03 | Dependency/build | Low | `VBAudioMonitor.csproj:13`, `build.ps1:17`, `packages.lock.json:1` | Fixed: locked application restore and all-package NuGet audit; High/Critical advisories fail restore |
| DEP-04 | Dependency/build | Low | `global.json:1` | Fixed: selected serviced SDK 8.0.425 with latest patch roll-forward in its feature band |
| KEY-01 | Secrets prevention | Low | `.gitignore:14` | Fixed: ignore environment files, logs and common private-key/container files |
| CODE-03 | Code/update trust | Medium | `src/services/AppUpdates.cs:20`, `src/services/AppUpdates.cs:77` | Developer decision: independent publisher authentication; current releases unsigned |
| DEP-05 | Dependency lifecycle | Medium | `VBAudioMonitor.csproj:4` | Developer decision: .NET 8 support ends 2026-11-10; major runtime migration not performed |

## Dependency vulnerability results

Each package name was searched individually against primary advisory sources, including GitHub Advisory Database, official repository advisories, OSV and NVD. The exact 94 installed versions were also queried through OSV's batch API. The app was cross-checked with `dotnet list VBAudioMonitor.csproj package --vulnerable --include-transitive`; that command reported no vulnerable application packages. It does not cover the separate `vpk` tool or automatically establish that the bundled runtime is safe.

NAudio.Wasapi 2.2.1, NAudio.Core 2.2.1 and Velopack 1.2.0 had no applicable published advisory in these checks. The official [NAudio advisories](https://github.com/naudio/NAudio/security/advisories) and [Velopack advisories](https://github.com/velopack/velopack/security/advisories) pages contained no published advisories. Historical search hits against other ecosystems or older versions were not attributed to the installed NuGet versions.

Only SixLabors.ImageSharp 2.1.13 matched OSV advisories in the inventory:

| Advisory | Upstream severity | Affected range | Fixed version | Relevant behavior / project reachability |
| --- | --- | --- | --- | --- |
| [GHSA-j3p4-wp97-rph4](https://github.com/advisories/GHSA-j3p4-wp97-rph4) | High | 2.0.0–4.1.1 | 4.1.2 | Floating-point TIFF input plus HistogramEqualization can cause unsafe histogram access. Current packaging does not perform either operation. |
| [GHSA-j9gm-c75j-xc9q](https://github.com/advisories/GHSA-j9gm-c75j-xc9q) | High | 2.0.0–4.1.1 | 4.1.2 | TIFF CCITT Group 3 encoding can write beyond its output buffer. Current packaging does not encode TIFF. |
| [GHSA-jjfr-hcj7-qf5w](https://github.com/advisories/GHSA-jjfr-hcj7-qf5w) | High | 2.1.0–4.1.1 | 4.1.2 | TIFF CCITT Group 4 encoding can exceed its output buffer. Current packaging does not encode TIFF. |
| [GHSA-gwg2-r3hj-4w44](https://github.com/advisories/GHSA-gwg2-r3hj-4w44) | Medium (upstream Moderate) | 1.0.0-beta0001–4.1.1 | 4.1.2 | Malformed ICC CLUT data causes excessive allocation. Earlier releases require accessing the lazy ICC entries parser. No such access was found in the reviewed vpk PNG path; full third-party call-graph reachability remains **待确认**. |
| [GHSA-wmxv-xphr-5c9g](https://github.com/advisories/GHSA-wmxv-xphr-5c9g) | Medium (upstream Moderate) | 2.0.0–4.1.1 | 4.1.2 | Malformed BigTIFF directory data can cause non-progressing decoding. No TIFF decoder is used in the reviewed ICO/PNG path. |

The app does not reference or ship ImageSharp as an application package. It exists inside the local packaging tool. The reviewed [vpk 1.2.0 project](https://github.com/velopack/velopack/blob/1.2.0/src/vpk/Velopack.IcoLib/Velopack.IcoLib.csproj) references 2.1.13, as does the [latest stable 1.2.161 project](https://github.com/velopack/velopack/blob/1.2.161/src/vpk/Velopack.IcoLib/Velopack.IcoLib.csproj). Its [PNG decoder](https://github.com/velopack/velopack/blob/1.2.161/src/vpk/Velopack.IcoLib/Codecs/PngDecoder.cs) explicitly decodes PNG as RGBA32. Updating the application's own references would not repair this standalone tool.

**Approved exception:** the developer chose to retain the tool rather than maintain a fork with a major ImageSharp upgrade. `build.ps1:7` now checks the SHA-256 of the two reviewed, project-generated icon assets before restore, publish or packaging. This narrows the allowed input; it does not fix ImageSharp. Build only trusted source/assets, without administrator privileges, and do not feed third-party image files to this tool. Reassess the exception when upstream fixes its dependency, whenever icons change, when new image-processing paths are used, or by **2026-11-09**, whichever comes first. No monitoring automation was scheduled.

The current 8.0.31 runtime matches the serviced version listed by Microsoft's [support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) on the audit date. NAudio 2.2.1 is an older pinned release, but the [official project](https://github.com/naudio/NAudio/releases) is maintained and has later releases; old version does not by itself mean abandoned. No confirmed typosquatted package was found. Build-only Newtonsoft.Json.Bson 1.0.2 is old; no applicable advisory was found, and abandonment is **待确认** rather than asserted. Native tools bundled by vpk may contain additional statically linked dependencies beyond its managed manifest; exhaustive Rust/C/C++ SBOM coverage is **待确认**.

## Fixed issues and exploit conditions

**CODE-01 — unbounded settings read (Low).** Previously `File.ReadAllText` allocated the entire settings file before typed deserialization. A local actor able to write the preferences file, or accidental corruption, could force excessive startup allocation. No remote settings-import endpoint exists. The load now opens read-only with stable sharing and rejects files exceeding 8 MiB before reading. Saves reject oversized serialized content before touching the old file. Ordinary UTF-8/BOM preferences and the settings schema remain compatible. Over-limit reads use the existing corrupt-file fallback; back up unusually large hand-edited settings before launching.

**CODE-02 — predictable temporary path (Low).** The old fixed `settings.json.tmp` could overwrite an existing temporary file and expose saves to pre-created links or concurrent access in a writable portable folder. Exploitation needs local write access to that folder; normal per-user execution does not provide privilege escalation. Save now uses a GUID name, `FileMode.CreateNew`, `FileShare.None`, disk flush and same-directory atomic rename. Only successfully created temporary files are cleaned up. This does not secure a directory already writable by an attacker against all link/replacement races or protect an elevated app from same-user tampering.

**DEP-03 — application dependency drift / audit gap (Low).** The new project `packages.lock.json` records the three resolved packages and content hashes; include it in version control with these changes. Release restore uses `--locked-mode`. `NuGetAuditMode=all` includes indirect app packages; NU1903/NU1904 are treated as errors. No dependency major version was changed. Audit-service/network failures still need to be treated as a failed release review even where NuGet emits a warning instead of a vulnerability result. The separate tool exception is not suppressed through `NoWarn` or a false clean audit result.

**DEP-04 — SDK selection (Low).** `global.json` selects SDK 8.0.425 and allows later servicing patches in its 8.0.4xx feature band, preventing an accidental build with the older installed 8.0.3-era SDK/runtime. This is not a runtime upgrade and does not authorize moving to .NET 9/10. Self-contained downloads need rebuilding and a new public release to receive future runtime security patches.

**KEY-01 — missing ignore rules (Low, prevention only).** No active project secret was discovered. `.env`, `.env.*`, log files, and `.pfx/.p12/.key/.pem` are now ignored; `.env.example` remains eligible for version control. There are no credential configuration variables to migrate, so an artificial `.env.example` was not added. Ignore rules do not untrack existing files, erase Git history or revoke a leaked credential.

`Checks.cs` adds isolated storage tests for round trips, existing temporary-file preservation, malformed input, BOM compatibility, oversized reads, rejected oversized saves preserving the old file, and failed atomic replacement cleanup. README remains English and documents the SDK, dependency lock, icon exception and settings bounds. The packaging whitelist now includes this report so its relative README link works in downloaded archives; private audit evidence stays excluded. `build.ps1:112` also rejects an existing local vpk installation whose version differs from the reviewed 1.2.0.

## Code review coverage and negative findings

- SQL/NoSQL, command/template injection and dynamic code: no database, shell invocation, eval, assembly loading or template execution in application code. Build parameters use a restrictive version regex and literal filesystem paths. The ignored publisher script starts Git with fixed arguments; no token is embedded in it.
- Web/XSS/CSRF/CORS/authentication/SSRF: no web server, extension messaging or WebView. The app's update repository URL is fixed HTTPS, not configurable by settings. Standard TLS validation is not disabled. Redirect and archive handling delegated to Velopack was reviewed selectively, not exhaustively fuzzed.
- Deserialization: System.Text.Json targets fixed sealed settings/profile records, without polymorphic type resolution, BinaryFormatter or arbitrary object construction. Profile names, channel pairs, buffer choices and gain have bounds checks. Settings paths are derived locally; user preset/device names are not used as filesystem paths.
- Files and archives: first-party code creates archives from an explicit public-file list and does not unpack user uploads. Tests inspect ZIP entries without extracting them. Runtime update extraction is delegated to Velopack, so “no first-party Zip Slip found” is not proof that all vendor/native extraction is vulnerability-free.
- Cryptography and updates: package-integrity checks and the tamper-rejection regression passed. SHA-1 present in compatibility metadata is not a password hash or the sole verified digest of the current SHA-256 feed. There is no custom encryption, hardcoded IV/salt, TLS bypass or credential storage. There is also no independent publisher-signature pin in AppUpdates.
- Desktop/IPC: no named-pipe listener, socket service or credential-bearing IPC. The named mutex is a single-instance mechanism, not authentication. Same-session mutex squatting can interfere with startup; this is a local availability limitation, not a demonstrated cross-user privilege escalation.
- Diagnostics/logs: local diagnostic views show device names/IDs and native error messages. Test mode writes device metadata, screenshots and stack traces locally; these are excluded from Git/public packages. Reports can contain personal device details, so review them before sharing. No telemetry upload or microphone recording path was found.
- CI/CD: no Actions workflows, `pull_request_target` or third-party Action references. HTTPS NuGet source is restricted to nuget.org. GitHub account/repository protection settings, credential-helper configuration outside this project and signing-service credentials were not changed or certified by this audit.

## Credential and release-payload review

Pattern scans covered first-party source, tests, documentation, config, comments, ignored release/tool/package directories, ordinary binary byte content and reachable Git history. No `.env`, private-key container or credential file was tracked. No active hardcoded API key, GitHub token, cloud key, password, private key, connection string or private webhook was found in project-controlled files. Broad binary candidates were reviewed with values redacted; strong-pattern candidates in WiX/.NET binaries were contiguous UI resource names or culture identifiers containing `sk-`, not API credentials. Generic password/API property names were not reported as leaked secrets.

Archive payload inspection rejects preferences, environment files, private-key containers, verification output and unsafe entry paths. Public package whitelists exclude settings/presets/device IDs. This is pattern-based inspection, not an entropy-complete credential detector; compressed executable internals and secrets split or encrypted into binary data remain **待确认**. No gitleaks/trufflehog tool was installed locally. No platform key was rotated because no compromised project credential was identified. If any key is later found in a published archive or Git history, revoke/rotate it at its provider; removing a file alone is insufficient.

## Developer decisions and remaining risk

1. **ImageSharp tool exception: decided.** Keep the existing tool and reviewed icon inputs; revisit at the conditions above. Alternative: maintain a tested vpk fork using ImageSharp 4.1.2, including compatibility and license review. The exception is a mitigation, not “fixed dependencies.”
2. **Publisher authentication (CODE-03): pending decision.** AppUpdates trusts GitHub repository write access plus HTTPS and feed hashes. If a release publisher/account is compromised, an attacker could replace both package and index and distribute executable code with the user's rights. Option A: retain the mechanism, enforce strong GitHub account/release access controls and accept this trust root. Option B (recommended for broader distribution): obtain a signing identity, sign Setup/app binaries, and design independent update-manifest/package signature verification with a pinned public key and key-rotation plan. Authenticode signing alone does not automatically make this updater enforce a publisher pin. No certificate purchase, private key generation or update-policy change was made.
3. **Runtime lifecycle (DEP-05): pending decision.** .NET 8 is supported until **2026-11-10**. Option A: keep .NET 8 temporarily, apply every patch, and schedule migration before that date. Option B (recommended): validate a move to a supported .NET 10 LTS SDK/runtime, including WinForms DPI, COM/WASAPI, installation and update regression. This is a major upgrade and was not performed without approval. The obsolete .NET Core 3.1 installed on this workstation is outside the app's bundled runtime and was not removed.
4. **Delivery:** local fixes do not change users' already downloaded 1.1.0 binaries. Publish a new version after reviewing this audit; do not replace the existing public assets under the same version. Do not advertise all dependencies as vulnerability-free while the accepted build-tool exception remains.

## Validation

- Locked NuGet restore and Release build: passed, zero warnings/errors.
- Application NuGet vulnerability scan: no applicable package vulnerabilities reported.
- Existing hidden UI/model/update regression checks plus new storage checks: passed. `AudioTest=false`, `PreferencesSecurityTest=true`, `UpdateFeedTest=true`. Corrupted update package was rejected.
- Existing workspace and installed preferences hashes: unchanged; the running installed app was left running.
- Full self-contained publish, portable ZIP and Velopack packaging: passed for local candidate 1.1.2. The packaged executable also passed the hidden UI/model/storage/update checks. No new public release was made.
- Changed-icon negative test: rejected before restore/publish/packaging. Public archives contained no unsafe paths, settings, environment files, private-key containers or verification output. Older 1.1.0/1.1.1 packages were also inspected.
- Release checksums, update-feed package size/SHA-256, public ZIP whitelist, packaged report consistency and strong credential-pattern scans of archive text entries: passed. Raw binary/compressed-code limitations remain as stated above.

Local raw evidence is in ignored `dist/security-audit`: installed dependency inventory, OSV responses and five full advisory records, all per-package web searches, redacted binary candidate review, check output/screenshots and packaging results. It is not included as private evidence in public application packages.

Not checked: live audio playback quality, destructive exploit execution, cross-user/elevated link races, hardware reconnection, mixed-monitor DPI, exhaustive native/dependency source review, all binary embedded data, and GitHub account controls. None is represented as a passing security test.

## Resolved dependency inventory

The following table lists every queried package/version entry. Build-tool versions are taken from vpk's installed `vpk.deps.json`; these are not added to the application's lock file.

| Package | Actual version | Scope | OSV result at audit time |
| --- | --- | --- | --- |
| AsmResolver | 6.0.0 | vpk build tool | No applicable advisory returned |
| AsmResolver.DotNet | 6.0.0 | vpk build tool | No applicable advisory returned |
| AsmResolver.PE | 6.0.0 | vpk build tool | No applicable advisory returned |
| AsmResolver.PE.File | 6.0.0 | vpk build tool | No applicable advisory returned |
| AsmResolver.PE.Win32Resources | 6.0.0 | vpk build tool | No applicable advisory returned |
| AWSSDK.Core | 4.0.7.3 | vpk build tool | No applicable advisory returned |
| AWSSDK.S3 | 4.0.23.4 | vpk build tool | No applicable advisory returned |
| Azure.Core | 1.51.1 | vpk build tool | No applicable advisory returned |
| Azure.Storage.Blobs | 12.28.0 | vpk build tool | No applicable advisory returned |
| Azure.Storage.Common | 12.27.0 | vpk build tool | No applicable advisory returned |
| ELFSharp | 2.17.3 | vpk build tool | No applicable advisory returned |
| Gitea.Net.API | 25.8.18 | vpk build tool | No applicable advisory returned |
| Handlebars.Net | 2.1.6 | vpk build tool | No applicable advisory returned |
| Humanizer.Core | 3.0.10 | vpk build tool | No applicable advisory returned |
| Markdig | 1.2.0 | vpk build tool | No applicable advisory returned |
| Microsoft.AspNet.WebApi.Client | 6.0.0 | vpk build tool | No applicable advisory returned |
| Microsoft.Bcl.AsyncInterfaces | 10.0.2 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Configuration | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Configuration.Binder | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Configuration.CommandLine | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Configuration.FileExtensions | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Configuration.Json | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Configuration.UserSecrets | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.DependencyInjection | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Diagnostics | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Diagnostics.Abstractions | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.FileProviders.Abstractions | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.FileProviders.Physical | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.FileSystemGlobbing | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Hosting | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Http | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Logging | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Logging.Abstractions | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Logging.Configuration | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Logging.Console | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Logging.Debug | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Logging.EventLog | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Logging.EventSource | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Options | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Extensions.Primitives | 10.0.8 | vpk build tool | No applicable advisory returned |
| Microsoft.Identity.Client | 4.84.1 | vpk build tool | No applicable advisory returned |
| Microsoft.Identity.Client.Broker | 4.84.1 | vpk build tool | No applicable advisory returned |
| Microsoft.Identity.Client.Extensions.Msal | 4.84.1 | vpk build tool | No applicable advisory returned |
| Microsoft.Identity.Client.NativeInterop | 0.20.4 | vpk build tool | No applicable advisory returned |
| Microsoft.IdentityModel.Abstractions | 8.14.0 | vpk build tool | No applicable advisory returned |
| Microsoft.Security.Extensions | 1.4.0 | vpk build tool | No applicable advisory returned |
| Newtonsoft.Json | 13.0.4 | vpk build tool | No applicable advisory returned |
| Newtonsoft.Json.Bson | 1.0.2 | vpk build tool | No applicable advisory returned |
| NuGet.Common | 7.6.0 | vpk build tool | No applicable advisory returned |
| NuGet.Configuration | 7.6.0 | vpk build tool | No applicable advisory returned |
| NuGet.Frameworks | 7.6.0 | vpk build tool | No applicable advisory returned |
| NuGet.Packaging | 7.6.0 | vpk build tool | No applicable advisory returned |
| NuGet.Protocol | 7.6.0 | vpk build tool | No applicable advisory returned |
| NuGet.Versioning | 7.6.0 | vpk build tool | No applicable advisory returned |
| Octokit | 14.0.0 | vpk build tool | No applicable advisory returned |
| Polly | 8.6.2 | vpk build tool | No applicable advisory returned |
| Polly.Core | 8.6.2 | vpk build tool | No applicable advisory returned |
| RestSharp | 114.0.0 | vpk build tool | No applicable advisory returned |
| Riok.Mapperly | 4.3.1 | vpk build tool | No applicable advisory returned |
| Serilog | 4.3.0 | vpk build tool | No applicable advisory returned |
| Serilog.Expressions | 5.0.0 | vpk build tool | No applicable advisory returned |
| Serilog.Extensions.Hosting | 10.0.0 | vpk build tool | No applicable advisory returned |
| Serilog.Extensions.Logging | 10.0.0 | vpk build tool | No applicable advisory returned |
| Serilog.Sinks.Console | 6.1.1 | vpk build tool | No applicable advisory returned |
| SixLabors.ImageSharp | 2.1.13 | vpk build tool | 5 applicable advisories; accepted build exception |
| Spectre.Console | 0.55.2 | vpk build tool | No applicable advisory returned |
| Spectre.Console.Ansi | 0.55.2 | vpk build tool | No applicable advisory returned |
| System.ClientModel | 1.9.0 | vpk build tool | No applicable advisory returned |
| System.CommandLine | 2.0.8 | vpk build tool | No applicable advisory returned |
| System.Diagnostics.DiagnosticSource | 10.0.8 | vpk build tool | No applicable advisory returned |
| System.Diagnostics.EventLog | 10.0.8 | vpk build tool | No applicable advisory returned |
| System.Formats.Asn1 | 10.0.8 | vpk build tool | No applicable advisory returned |
| System.Interactive.Async | 7.0.1 | vpk build tool | No applicable advisory returned |
| System.IO.Hashing | 10.0.8 | vpk build tool | No applicable advisory returned |
| System.IO.Pipelines | 10.0.8 | vpk build tool | No applicable advisory returned |
| System.Linq.Async | 7.0.1 | vpk build tool | No applicable advisory returned |
| System.Linq.AsyncEnumerable | 10.0.6 | vpk build tool | No applicable advisory returned |
| System.Memory.Data | 10.0.1 | vpk build tool | No applicable advisory returned |
| System.Security.Cryptography.Pkcs | 8.0.1 | vpk build tool | No applicable advisory returned |
| System.Security.Cryptography.ProtectedData | 8.0.0 | vpk build tool | No applicable advisory returned |
| System.Text.Encodings.Web | 10.0.8 | vpk build tool | No applicable advisory returned |
| System.Text.Json | 10.0.8 | vpk build tool | No applicable advisory returned |
| NAudio.Core | 2.2.1 | app indirect | No applicable advisory returned |
| NAudio.Wasapi | 2.2.1 | app direct | No applicable advisory returned |
| Velopack | 1.2.0 | app direct | No applicable advisory returned |
| vpk | 1.2.0 | build direct | No applicable advisory returned |
| Microsoft.NETCore.App.Runtime.win-x64 | 8.0.31 | runtime | No applicable advisory returned |
| Microsoft.WindowsDesktop.App.Runtime.win-x64 | 8.0.31 | runtime | No applicable advisory returned |
| Microsoft.AspNetCore.App.Runtime.win-x64 | 8.0.31 | restore only | No applicable advisory returned |
