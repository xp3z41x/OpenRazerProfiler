# Gera dist\OpenRazerProfiler.exe: um único executável portátil (.NET Framework 4.8, já incluso no Windows 11).
# Requisito para compilar: .NET SDK 6 ou mais novo (winget install Microsoft.DotNet.SDK.8).
# Uso: powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'OpenRazerProfiler.csproj'
$output = Join-Path $root 'bin\Release'
$dist = Join-Path $root 'dist'

dotnet build $project -c Release -nologo
if ($LASTEXITCODE -ne 0) { throw "A compilação falhou (código $LASTEXITCODE)." }

New-Item -ItemType Directory -Force $dist | Out-Null
Copy-Item (Join-Path $output 'OpenRazerProfiler.exe') (Join-Path $dist 'OpenRazerProfiler.exe') -Force
$exe = Get-Item (Join-Path $dist 'OpenRazerProfiler.exe')
Write-Output ("Pronto: {0} ({1:N0} KB)" -f $exe.FullName, ($exe.Length / 1KB))
