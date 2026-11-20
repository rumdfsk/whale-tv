$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    dotnet run --project tests/TVLive.Checks
    if ($LASTEXITCODE -ne 0) { throw 'Checks failed' }
    dotnet publish src/TVLive/TVLive.csproj -c Release -r win-x64 --self-contained true -o dist/WhaleTV-0.1.6-win-x64 -p:PublishReadyToRun=false
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    dotnet run --project tests/TVLive.PackageChecks -- dist/WhaleTV-0.1.6-win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Package dependency checks failed' }
    Copy-Item README.md, THIRD-PARTY-NOTICES.md dist/WhaleTV-0.1.6-win-x64
    Copy-Item licenses dist/WhaleTV-0.1.6-win-x64 -Recurse -Force
    New-Item dist/WhaleTV-0.1.6-win-x64/research -ItemType Directory -Force | Out-Null
    Copy-Item research/local-channel-adaptation.md dist/WhaleTV-0.1.6-win-x64/research
    foreach ($arch in @("win-x86", "win-arm64")) {
        $folder = "dist/WhaleTV-0.1.6-win-x64/libvlc/$arch"
        if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
    }
    Compress-Archive -Path dist/WhaleTV-0.1.6-win-x64 -DestinationPath dist/WhaleTV-0.1.6-win-x64.zip -Force
} finally { Pop-Location }
