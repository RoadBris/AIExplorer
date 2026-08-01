# AIExplorer v0.8.8 validation

- Navigation tree drop contract: folder, `.lnk`, and `.url` sources are accepted as favorites.
- Favorites persist through `AppSettings.Favorites`.
- Favorites render directly below Quick Access.
- The inactive network plus button is absent from `MainWindow.xaml`.
- Favorite rename and removal commands are wired in XAML and code-behind.
- Existing search, AI, network share, and 500-result contracts remain covered by smoke tests and preflight.
