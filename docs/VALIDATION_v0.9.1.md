# AIExplorer v0.9.1 validation

This maintenance release fixes the CS0136 local-variable scope collision in `NavigationTree_Drop`.

Cross-platform validation checks:

- XAML XML and event handlers
- C# bracket and literal structure
- Favorite drag/drop scope regression (`reorderSourcePath` vs. file-drop `sourcePath`)
- Progressive search contracts
- NuGet, AI bundle, network, favorites, and process cleanup contracts

A Windows .NET 10 WPF build remains the final platform-specific verification step.
