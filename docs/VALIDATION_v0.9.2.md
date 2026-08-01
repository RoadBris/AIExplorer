# AIExplorer v0.9.2 validation

This release adds live SMB/UNC result batches and context-aware lexical ranking.

Static validation covers:

- C# bracket and XAML event structure
- progressive partial result contracts
- UNC roots always using a current direct scan
- live result batches from the targeted scanner
- multi-term weak-match rejection
- document context-coherence ranking
- existing AI, NuGet, favorites, process-cleanup, and packaging contracts

The Linux packaging environment cannot execute Windows WPF, DirectML, or the full .NET smoke-test project.
