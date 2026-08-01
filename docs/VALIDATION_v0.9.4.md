# AIExplorer v0.9.4 validation

This maintenance release fixes a stale Windows preflight contract introduced after the search-root pipeline changed.

Static validation covers:

- `ResolveSearchRoots` retains syntactically valid UNC candidates without synchronous availability filtering
- `EnsureSearchRootsAccessibleAsync` performs the asynchronous network access and reconnect check
- `_networkPathService.EnsureAccessibleAsync` remains in that accessibility stage
- the removed legacy `IsConfiguredSearchRoot` filter is not referenced by source or preflight checks
- PowerShell preflight and Python validation require the same current contract
- existing dual title/AI search, progressive SMB search, favorites, XAML, and packaging contracts

The Linux packaging environment cannot execute Windows PowerShell, WPF, DirectML, or the full .NET 10 smoke-test project. Run `verify_source.cmd` on Windows before building the release package.
