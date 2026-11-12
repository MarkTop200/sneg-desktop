$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Source') -Filter '*.cs' | ForEach-Object FullName
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /debug:pdbonly /utf8output ("/out:" + (Join-Path $PSScriptRoot 'Sneg.exe')) ("/win32manifest:" + (Join-Path $PSScriptRoot 'app.manifest')) ("/win32icon:" + (Join-Path $PSScriptRoot 'Assets\Sneg_icon.ico')) ("/resource:" + (Join-Path $PSScriptRoot 'Assets\Sneg_icon.ico') + ",Sneg.AppIcon") /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.dll /r:System.Core.dll $sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Сборка не удалась' }
Write-Output 'Готово: Sneg.exe'
