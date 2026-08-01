# AIExplorer v0.8.6 validation

## Added regression contracts

- `내 PC` navigation node is selectable and opens a drive/server view.
- A bare IPv4 or host name is normalized to a UNC server root.
- UNC server roots and share roots are distinguished.
- Connected and remembered WNet resources, Explorer network shortcuts, and MountPoints2 UNC records are discovered.
- Expanding or opening a server root enumerates normal disk shares through `NetShareEnum`.
- Search roots configured as a server are expanded into concrete share paths before indexing.

## Checks available in this source package

- XAML parsing and event-handler binding
- C# literal/comment-aware bracket validation
- Network discovery and dialog contract checks
- Windows smoke-test cases for UNC normalization and navigation-node behavior

Actual SMB enumeration, Windows credential prompts, WPF rendering, DirectML inference, and the complete smoke-test executable must be run on Windows with `verify_source.cmd`.
