# AIExplorer v0.9.6 validation

## Regression fixed

The llama.cpp release asset selector now accepts the current Windows x64 CPU
asset name (`llama-b*-bin-win-x64.zip`) as well as earlier explicit CPU and
AVX variants. Accelerated backend packages are excluded.

## Static validation

- PowerShell 5.1 UTF-8 BOM and ASCII-safe body
- Current and legacy llama.cpp Windows CPU asset names
- Accelerated backend exclusion
- Matching runtime selection rules in build script and `AiModelManager`
- Existing search, network, favorites and WPF source contracts
