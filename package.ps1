param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "ConvenientText.csproj"
$output = Join-Path $PSScriptRoot "bin/Release/net8.0"
$artifactDirectory = Join-Path $PSScriptRoot "artifacts"
$package = Join-Path $artifactDirectory "ConvenientText.cipx"

if (-not $NoBuild) {
    dotnet build $project --configuration Release
}

$files = @(
    "ConvenientText.dll",
    "System.CodeDom.dll",
    "System.Management.dll",
    "manifest.yml",
    "icon.png",
    "README.md"
)

New-Item -ItemType Directory -Force $artifactDirectory | Out-Null
Remove-Item $package -ErrorAction SilentlyContinue
$stagingDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "ConvenientText-$([guid]::NewGuid().ToString('N'))"
try {
    New-Item -ItemType Directory $stagingDirectory | Out-Null
    $files | ForEach-Object { Copy-Item (Join-Path $output $_) $stagingDirectory }
    [System.IO.Compression.ZipFile]::CreateFromDirectory($stagingDirectory, $package)
}
finally {
    Remove-Item $stagingDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "Created $package"
