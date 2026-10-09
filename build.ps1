param([string]$Version = '')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
if (!$Version) { $Version = ([xml](Get-Content -LiteralPath VBAudioMonitor.csproj -Raw)).Project.PropertyGroup.Version }
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw 'Use a three-part semantic version such as 1.1.0.' }
$releaseDirectory = Join-Path $PSScriptRoot 'release-installable'
dotnet restore VBAudioMonitor.csproj --configfile NuGet.Config --runtime win-x64
if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
dotnet publish VBAudioMonitor.csproj --configuration Release --runtime win-x64 --self-contained true --no-restore --output $releaseDirectory -p:Version=$Version -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $releaseDirectory 'README.md')
$releaseAssets = Join-Path $releaseDirectory 'assets'
New-Item -ItemType Directory -Path $releaseAssets -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets\app.ico'), (Join-Path $PSScriptRoot 'assets\app.png') -Destination $releaseAssets
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets\VELOPACK-LICENSE.txt') -Destination (Join-Path $releaseDirectory 'VELOPACK-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.txt') -Destination (Join-Path $releaseDirectory 'THIRD-PARTY-NOTICES.txt')
$runtimePackage = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '.packages\microsoft.netcore.app.runtime.win-x64') -Directory | Sort-Object Name -Descending | Select-Object -First 1
Copy-Item -LiteralPath (Join-Path $runtimePackage.FullName 'LICENSE.TXT') -Destination (Join-Path $releaseDirectory 'DOTNET-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $runtimePackage.FullName 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $releaseDirectory 'DOTNET-THIRD-PARTY-NOTICES.txt')
$desktopPackage = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '.packages\microsoft.windowsdesktop.app.runtime.win-x64') -Directory | Sort-Object Name -Descending | Select-Object -First 1
Copy-Item -LiteralPath (Join-Path $desktopPackage.FullName 'LICENSE') -Destination (Join-Path $releaseDirectory 'WINDOWS-DESKTOP-LICENSE.txt')
$savedSettings = Join-Path $PSScriptRoot 'release-verified\settings.json'
if (!(Test-Path -LiteralPath $savedSettings)) {
    $savedSettings = Join-Path $PSScriptRoot 'release-multilingual\settings.json'
}
if (!(Test-Path -LiteralPath $savedSettings)) {
    $savedSettings = Join-Path $PSScriptRoot 'release-readable\settings.json'
}
if (!(Test-Path -LiteralPath $savedSettings)) {
    $savedSettings = Join-Path $PSScriptRoot 'release-compact\settings.json'
}
if (!(Test-Path -LiteralPath $savedSettings)) {
    $savedSettings = Join-Path $PSScriptRoot 'release-scenes-ui\settings.json'
}
if (!(Test-Path -LiteralPath $savedSettings)) {
    $savedSettings = Join-Path $PSScriptRoot 'release-scenes\settings.json'
}
if (!(Test-Path -LiteralPath $savedSettings)) {
    $savedSettings = Join-Path $PSScriptRoot 'release-pro\settings.json'
}
if (!(Test-Path -LiteralPath $savedSettings)) {
    $savedSettings = Join-Path $PSScriptRoot 'release-improved\settings.json'
}
if (!(Test-Path -LiteralPath $savedSettings)) {
    $savedSettings = Join-Path $PSScriptRoot 'release-large-ui\settings.json'
}
$activeApp = Get-Process -Name VBAudioMonitor -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -and $_.Path.StartsWith($PSScriptRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)
} | Select-Object -First 1
if ($activeApp) {
    $activeSettings = Join-Path (Split-Path -Parent $activeApp.Path) 'settings.json'
    if (Test-Path -LiteralPath $activeSettings) { $savedSettings = $activeSettings }
}
if ((Test-Path -LiteralPath $savedSettings) -and !(Test-Path -LiteralPath (Join-Path $releaseDirectory 'settings.json'))) {
    Copy-Item -LiteralPath $savedSettings -Destination (Join-Path $releaseDirectory 'settings.json')
}

# Explicit files keep local settings and verification output out of public downloads.
$distributionFiles = @('VBAudioMonitor.exe', 'README.md', 'assets/app.ico', 'assets/app.png',
    'THIRD-PARTY-NOTICES.txt', 'DOTNET-LICENSE.txt', 'DOTNET-THIRD-PARTY-NOTICES.txt', 'WINDOWS-DESKTOP-LICENSE.txt', 'VELOPACK-LICENSE.txt')
$distributionDirectory = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $distributionDirectory -Force | Out-Null
$archivePath = Join-Path $distributionDirectory 'VBAudioMonitor-win-x64-portable.zip'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$archive = [System.IO.Compression.ZipArchive]::new([System.IO.File]::Create($archivePath), [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($relativePath in $distributionFiles) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,
            (Join-Path $releaseDirectory $relativePath), $relativePath) | Out-Null
    }
} finally { $archive.Dispose() }
$archiveCheck = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    if ($archiveCheck.Entries.Count -ne $distributionFiles.Count -or
        @($archiveCheck.Entries | Where-Object { $_.FullName -notin $distributionFiles -or $_.Length -le 0 }).Count -ne 0) {
        throw 'Public archive contains missing, empty or unexpected files.'
    }
} finally { $archiveCheck.Dispose() }
$checksum = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath ($archivePath + '.sha256') -Value ($checksum + '  ' + [System.IO.Path]::GetFileName($archivePath)) -Encoding ASCII
Write-Output ('Public download: ' + $archivePath)

$packInput = Join-Path $distributionDirectory ('installer-input\' + $Version)
New-Item -ItemType Directory -Path $packInput -Force | Out-Null
foreach ($relativePath in $distributionFiles) {
    $targetPath = Join-Path $packInput $relativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $targetPath) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $releaseDirectory $relativePath) -Destination $targetPath
}
$inputFiles = @(Get-ChildItem -LiteralPath $packInput -Recurse -File)
if ($inputFiles.Count -ne $distributionFiles.Count -or
    @($inputFiles | Where-Object { $_.FullName.Substring($packInput.Length + 1).Replace('\', '/') -notin $distributionFiles }).Count -ne 0) {
    throw 'Installer input includes unexpected files. No public installer was generated.'
}
$packTool = Join-Path $PSScriptRoot '.tools\vpk.exe'
if (!(Test-Path -LiteralPath $packTool)) {
    dotnet tool install vpk --tool-path .tools --version 1.2.0 --configfile NuGet.Config
    if ($LASTEXITCODE -ne 0) { throw 'Installer packaging tool restore failed.' }
}
$installerOutput = Join-Path $distributionDirectory ('github-release\' + $Version)
$packOutput = Join-Path $distributionDirectory ('pack-output\' + [Guid]::NewGuid().ToString('N'))
& $packTool pack --packId VBAudioMonitor --packVersion $Version --packDir $packInput --mainExe VBAudioMonitor.exe --packTitle 'VB Audio Monitor' --packAuthors Kevinlabs26 --runtime win-x64 --icon (Join-Path $PSScriptRoot 'assets\app.ico') --noPortable --outputDir $packOutput --skip-updates
if ($LASTEXITCODE -ne 0) { throw 'Installer packaging failed.' }
New-Item -ItemType Directory -Path $installerOutput -Force | Out-Null
Get-ChildItem -LiteralPath $packOutput -File | Copy-Item -Destination $installerOutput -Force
if (![IO.Path]::GetFullPath($packOutput).StartsWith([IO.Path]::GetFullPath($distributionDirectory).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Temporary packaging path escaped the distribution directory.'
}
Remove-Item -LiteralPath $packOutput -Recurse -Force
if (!(Test-Path -LiteralPath (Join-Path $installerOutput 'VBAudioMonitor-win-Setup.exe')) -or
    !(Test-Path -LiteralPath (Join-Path $installerOutput 'releases.win.json'))) { throw 'Installer or update index is missing.' }
$checksums = Get-ChildItem -LiteralPath $installerOutput -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name
}
Set-Content -LiteralPath (Join-Path $installerOutput 'SHA256SUMS.txt') -Value $checksums -Encoding ASCII
Write-Output ('Upload every file from: ' + $installerOutput)
