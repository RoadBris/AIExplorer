# AIExplorer v0.9.7 validation

- PowerShell selector accepts null and empty asset collections.
- Empty embedded release assets fall back to the release `assets_url`.
- If the latest release has no usable Windows x64 CPU asset, the script checks up to 10 recent releases.
- GitHub API headers, CPU backend exclusion, model bundle hashes, source structure, and ZIP integrity are validated.
- The current Linux environment cannot execute Windows PowerShell 5.1 or build the .NET 10 WPF project.
