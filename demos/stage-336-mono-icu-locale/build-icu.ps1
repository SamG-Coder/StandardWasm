param(
    [Parameter(Mandatory)] [string] $SdkTools,
    [Parameter(Mandatory)] [string] $EmscriptenCache,
    [Parameter(Mandatory)] [string] $MonoNative,
    [Parameter(Mandatory)] [string] $OutputPath
)
$ErrorActionPreference = 'Stop'
$kit = (Resolve-Path -LiteralPath $SdkTools).Path
$cache = (Resolve-Path -LiteralPath $EmscriptenCache).Path
$runtime = (Resolve-Path -LiteralPath $MonoNative).Path
$env:EM_CONFIG = Join-Path $kit 'emscripten/.emscripten'
$env:DOTNET_EMSCRIPTEN_LLVM_ROOT = Join-Path $kit 'bin'
$env:DOTNET_EMSCRIPTEN_BINARYEN_ROOT = $kit
$env:DOTNET_EMSCRIPTEN_NODE_JS = (Get-Command node).Source
$env:EM_CACHE = $cache

python (Join-Path $kit 'emscripten/emcc.py') -O2 (Join-Path $PSScriptRoot 'icu.c') `
    (Join-Path $runtime 'libSystem.Globalization.Native.a') `
    (Join-Path $runtime 'libicuuc.a') (Join-Path $runtime 'libicudata.a') --no-entry `
    '-sEXPORTED_FUNCTIONS=[_installIcuData,_GlobalizationNative_IsPredefinedLocale,_malloc]' `
    -sFILESYSTEM=0 -sALLOW_MEMORY_GROWTH=1 -o $OutputPath
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
