# AIExplorer v0.8.5 validation

- Global ToolTip template uses a light surface, dark text, wrapping, and a bounded width.
- Navigation labels expose their full path through the global readable ToolTip.
- Windows mapped drives are enumerated through WNetGetConnection and persistent HKCU\Network entries.
- Active and disconnected persistent mappings are represented without probing the remote server during startup.
- Automatically discovered mappings are deduplicated against manually saved network locations.
