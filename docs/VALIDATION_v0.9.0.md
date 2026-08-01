# AIExplorer v0.9.0 validation

## Progressive search contracts

- The first search pass uses only currently available metadata/content indexes and does not invoke E5 or SigLIP.
- A targeted filename/path pass can add immediate results without waiting for full indexing.
- Metadata, content/OCR, semantic and visual indexes are warmed in bounded stages.
- Each completed stage searches the newly available partial indexes and reconciles results without clearing the whole list.
- Existing selection paths and already loaded preview images are preserved when an item is upgraded.
- Cancellation retains all results discovered before cancellation.
- Partial current metadata/content snapshots are searchable even when their configured limit is below the final search target.

## Environment limitation

The source validator checks XAML, event handlers, bracket balance, package contracts and progressive-search source contracts. A Windows .NET 10 WPF environment is still required for the final `verify_source.cmd` smoke run and DirectML execution test.
