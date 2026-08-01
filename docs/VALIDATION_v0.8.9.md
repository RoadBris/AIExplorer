# AIExplorer v0.8.9 validation

- The left navigation footer explains that dropping a folder or shortcut adds it to Favorites.
- Favorites can be reordered by drag and the new order persists in `AppSettings.Favorites`.
- Folder context menus in both the navigation tree and current-folder list expose `즐겨찾기에 추가`.
- Favorite rename and removal remain available only for favorite nodes.
- The standalone Network navigation root is absent; network access remains available through My PC, address entry, and favorites.
- Smoke tests cover direct folder creation, favorite reordering, and the non-selectable Favorites section.
- Existing search, AI, connected-share, tooltip, and 500-result contracts remain validated.
