# AIExplorer v0.9.3 validation

This release separates immediate literal title matching from slower document and AI analysis.

Static validation covers:

- XAML structure and all dual-pane event handlers
- an independent long-running title-search task
- no synchronous network existence probe before title search starts
- separate file/directory enumeration without one extra SMB attribute request per file
- first/exact title matches reported before the complete traversal finishes
- title result and scan limits
- AI-pane evidence filtering
- progressive result preservation, favorites, network access, process cleanup, and packaging contracts
- local smoke-test coverage for an obvious Korean filename keyword

The Linux packaging environment cannot execute Windows WPF, DirectML, or the full .NET 10 smoke-test project. Run `verify_source.cmd` on Windows before building the release package.
