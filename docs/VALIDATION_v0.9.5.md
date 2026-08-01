# AIExplorer v0.9.5 validation

## Fixed build regression

`RunBackgroundIndexingAsync` still called the removed `ResolveAllAvailableRoots()` helper after the all-roots resolver had been renamed to `ResolveAllAvailableRootsWithoutProbe()`. This caused Windows build error CS0103.

The call now uses the declared helper.

## Added guard

- Cross-platform validation extracts unqualified `Resolve*` calls in `MainWindow.xaml.cs` and verifies that each helper is declared.
- Windows PowerShell preflight rejects the removed `ResolveAllAvailableRoots()` call and requires the current helper.
- Progressive dual-pane title/AI search behavior remains unchanged.

## Environment limitation

The current validation environment does not provide the .NET 10 Windows WPF SDK, so the Windows compilation itself could not be executed here. Structural, contract, encoding, and ZIP-integrity checks were run.
