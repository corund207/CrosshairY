# Builds CrosshairY.exe — a native Windows app targeting .NET Framework 4.8 (built into Windows 10/11).
# No Visual Studio or .NET SDK required: the script fetches the Roslyn C# compiler from NuGet on first run.
param([switch]$Run, [switch]$Debug, [string]$Out = "", [switch]$Sign)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$tools = Join-Path $root '.tools'
$csc = Join-Path $tools 'roslyn\tasks\net472\csc.exe'

if (-not (Test-Path $csc)) {
    Write-Host 'Downloading Roslyn compiler (Microsoft.Net.Compilers.Toolset)...'
    New-Item -ItemType Directory -Force $tools | Out-Null
    $zip = Join-Path $tools 'roslyn.zip'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest 'https://www.nuget.org/api/v2/package/Microsoft.Net.Compilers.Toolset/4.14.0' -OutFile $zip
    Expand-Archive $zip (Join-Path $tools 'roslyn') -Force
    Remove-Item $zip
}

$buildDir = Join-Path $root 'build'
$bin = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $buildDir, $bin | Out-Null

# 1) app icon
$icon = Join-Path $buildDir 'app.ico'
if (-not (Test-Path $icon) -or (Get-Item (Join-Path $root 'tools\IconGen.cs')).LastWriteTime -gt (Get-Item $icon).LastWriteTime) {
    $gen = Join-Path $buildDir 'IconGen.exe'
    & $csc -nologo -langversion:latest -out:$gen -r:System.Drawing.dll (Join-Path $root 'tools\IconGen.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Icon generator failed to compile' }
    & $gen $icon
}

# 2) the app
$out = if ($Out) { $Out } else { Join-Path $bin "CrosshairY.exe" }
$sources = Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
$refs = 'System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Numerics.dll','Microsoft.CSharp.dll' | ForEach-Object { "-r:$_" }
$resources = Get-ChildItem (Join-Path $root 'src\Assets\Fonts') -Filter *.ttf | ForEach-Object { "-resource:$($_.FullName),CrosshairY.Fonts.$($_.Name)" }
$opt = if ($Debug) { @('-debug+', '-optimize-', '-define:DEBUG') } else { @('-optimize+', '-debug-') }
& $csc -nologo -langversion:latest -target:winexe -platform:anycpu -nowarn:CS0649,CS0169,CS0414 `
    "-out:$out" "-win32manifest:$(Join-Path $root 'src\app.manifest')" "-win32icon:$icon" @refs @resources @opt @sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Host "Built $out" -ForegroundColor Green

# 3) optional code signing (see docs/SIGNING.md)
#    certificate in the store:  $env:CROSSHAIRY_CERT_THUMBPRINT
#    or a PFX file:             $env:CROSSHAIRY_PFX + $env:CROSSHAIRY_PFX_PASSWORD
#    or Azure Trusted Signing:  $env:CROSSHAIRY_TRUSTED_SIGNING_JSON (metadata.json) + $env:CROSSHAIRY_TRUSTED_SIGNING_DLIB
if ($Sign) {
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $signtool) { throw 'signtool.exe not found: install the Windows SDK (Signing Tools for Desktop Apps).' }
    $ts = if ($env:CROSSHAIRY_TIMESTAMP_URL) { $env:CROSSHAIRY_TIMESTAMP_URL } else { 'http://timestamp.digicert.com' }
    $common = @('sign', '/fd', 'SHA256', '/tr', $ts, '/td', 'SHA256', '/d', 'CrosshairY', '/du', 'https://github.com/corund207/CrosshairY')
    if ($env:CROSSHAIRY_TRUSTED_SIGNING_JSON) {
        & $signtool @common /dlib $env:CROSSHAIRY_TRUSTED_SIGNING_DLIB /dmdf $env:CROSSHAIRY_TRUSTED_SIGNING_JSON $out
    } elseif ($env:CROSSHAIRY_CERT_THUMBPRINT) {
        & $signtool @common /sha1 $env:CROSSHAIRY_CERT_THUMBPRINT $out
    } elseif ($env:CROSSHAIRY_PFX) {
        & $signtool @common /f $env:CROSSHAIRY_PFX /p $env:CROSSHAIRY_PFX_PASSWORD $out
    } else { throw 'No signing certificate configured. See docs/SIGNING.md.' }
    if ($LASTEXITCODE -ne 0) { throw 'Signing failed' }
    & $signtool verify /pa $out | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Signature verification failed' }
    Write-Host "Signed $out" -ForegroundColor Green
}
if ($Run) { Start-Process $out }
