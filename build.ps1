param([switch]$Test)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$src = Join-Path $PSScriptRoot 'src'
$tests = Join-Path $PSScriptRoot 'tests'
$assets = Join-Path $PSScriptRoot 'assets'
$output = Join-Path $PSScriptRoot 'Parcel.exe'
$icon = Join-Path $assets 'Parcel.ico'
if (-not (Test-Path -LiteralPath $icon)) { throw "Application icon not found: $icon" }
$references = @('/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Web.Extensions.dll', '/r:System.IO.Compression.dll')
$engine = Join-Path $src 'PackageEngine.cs'
$program = Join-Path $src 'Program.cs'
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu "/win32icon:$icon" "/out:$output" @references $engine $program
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
Write-Output "Built $output"
if ($Test) {
    # Test binaries and fixtures live in a throwaway folder that is removed afterwards.
    $work = Join-Path $PSScriptRoot 'build'
    New-Item -ItemType Directory -Force -Path $work | Out-Null
    try {
        $testExe = Join-Path $work 'PackageTests.exe'
        & $compiler /nologo /target:exe "/out:$testExe" @references $engine (Join-Path $tests 'PackageTests.cs')
        if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
        & $testExe
        if ($LASTEXITCODE -ne 0) { throw 'Packaging tests failed.' }
        $uiTestExe = Join-Path $work 'UiSmoke.exe'
        & $compiler /nologo /target:exe /main:UiSmoke "/out:$uiTestExe" @references $engine $program (Join-Path $tests 'UiSmoke.cs')
        if ($LASTEXITCODE -ne 0) { throw 'UI test build failed.' }
        & $uiTestExe (Join-Path $assets 'ui-preview.png')
        if ($LASTEXITCODE -ne 0) { throw 'UI smoke test failed.' }
    } finally {
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}
