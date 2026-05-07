$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root "UndertaleSaveStudioPro.cs"
$out = Join-Path $root "UndertaleSaveStudioPro.exe"
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (!(Test-Path -LiteralPath $csc)) {
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (!(Test-Path -LiteralPath $csc)) {
    throw "Could not find the .NET Framework C# compiler. Install/enable .NET Framework 4.x developer tools."
}

& $csc /nologo /target:winexe /platform:anycpu `
    /r:System.dll `
    /r:System.Core.dll `
    /r:System.Drawing.dll `
    /r:System.Windows.Forms.dll `
    /r:System.IO.Compression.dll `
    /r:System.IO.Compression.FileSystem.dll `
    /out:$out `
    $source

Write-Host "Built $out"
