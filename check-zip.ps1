param(
    [string]$PackagePath = (Join-Path $PSScriptRoot "artifacts/ConvenientText.cipx")
)

$ErrorActionPreference = "Stop"
$requiredFiles = @(
    "ConvenientText.dll",
    "System.CodeDom.dll",
    "System.Management.dll",
    "manifest.yml",
    "icon.png",
    "README.md"
)

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
try {
    $entries = @($zip.Entries | ForEach-Object FullName)
    $missing = @($requiredFiles | Where-Object { $_ -notin $entries })
    if ($missing.Count -gt 0) {
        throw "Package is missing: $($missing -join ', ')"
    }
    Write-Host "Package contains all required files."
}
finally {
    $zip.Dispose()
}
