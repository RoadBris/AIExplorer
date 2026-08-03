# AI Explorer

This is a Windows-exclusive file manager that combines the familiar usage of Windows Explorer with completely local search. It searches not only file names and document contents, but also text in images and PDFs, as well as screen contents. The **v0.82.4 deployment package** includes the Qwen3 natural language interpretation model, the Multilingual E5 Base document search model, the SigLIP 2 Base visual search model, and the WD ViT character tagger. The visual model prioritizes the DirectML integrated graphics and automatically switches to the CPU on incompatible PCs, allowing you to use offline AI search from the very first launch.

## Search Method

The navigation tree, file list, unified search, and quick name/path search are laid out horizontally, and the two search results are displayed simultaneously from the beginning. `Quick Name/Path` uses the character postings of the completed memory index to update immediately starting from a single character input, and the natural language name search only displays items that match actual file or folder names. `Unified Search` separately combines evidence from document text, Excel sheets/cells, OCR, E5, and SigLIP.

The search results calculate the following evidence together, but in accordance with the typical flow of a user finding a file, direct clues from the file name and parent folder are sorted before the text/AI similarity.

1. File format, extension, name, parent folder, and modification time
2. Direct word matches in TXT, source code, and Office document texts, as well as internal cells of XLS, XLSX, XLSM, and XLSB
3. Text read by Windows local OCR from images and sample PDF pages
4. Multilingual E5 semantic candidates converted from file names, relative paths, and extracted documents
5. SigLIP 2 image/PDF visual candidates where the full screen and central detailed screen are saved separately
6. WD character, general, and rating tags extracted and saved once from anime/illustration images

Special files whose text cannot be read are also included in the E5 semantic index by composing the file name, extension meaning, and nearby parent folders as metadata descriptions. Candidates found separately by keywords, text, and E5 are merged through rank combination, and direct name matches are protected as the final safety priority.

You can also include result priority instructions within the search sentence. For example, `Find the AWS SSH key, and put more recently created files higher up` separates and interprets the search target and the `sort by latest creation date strongly` instruction. Creation/modification dates, file name, path, format, text, semantic similarity, and file size can be specified as `a little`, `strongly`, `top priority`, or as a percentage. To prevent irrelevant items from rising to the top just because they are the newest files, weights are applied within the direct name/combined evidence protection stage, and the interpreted criteria and reasons for application are displayed on the screen.

After the search is finished, you can enter natural language conditions in `Find again in results` to narrow down only the current candidates in the two result tabs. `Find only files containing Korean` checks whether actual Korean characters exist in the file name and the saved text/OCR/Excel cells, instead of searching for the word `Korean`. You can also specify the inspection scope, such as `Files with Korean in the content` or `Files with Korean in the file name`, and `Korean file` is distinguished as a request for HWP/HWPX formats. `Files completely in Korean` is interpreted as files containing only Korean in the file name, excluding the extension. Spaces, parentheses, and underscores are allowed, but names mixed with numbers or English are excluded. The conditions understood by the program are displayed below the input box, and clicking `To all results` restores the previous results and their exact order.

The sort menu at the top of the search results applies to both the unified results and the quick name/path results simultaneously. In addition to the default `By relevance`, you can sort by path (grouping from drives and top-level folders), alphabetical order, and latest modification date. Even if you use a different sort, the original relevance order is preserved, so you can immediately revert without running the search again.

Contents unrelated to the file name, such as `file containing mullvad login code`, are also found in TXT or supported document texts. By installing AI models, semantic similarities are reflected in the search rankings even if the expressions and languages differ, such as `privacy network access credentials` and `WireGuard tunnel account token`. Photos without clues in the file name, like `sunset picture` or `image with a dog`, are also found as pixel-based visual AI candidates.

When an image is requested along with a character name, it is compared against multiple character phrases distinct from general photos, and screens closer to software UIs, dashboards, or dialog boxes are penalized. Character identity is not concluded solely by pixel similarity, and only images whose identity is verified by the file name, folder name, or WD character tags are included in the results. Because of this, irrelevant banners are not displayed at the top of character searches just because they have high visual scores.
English transliterations of Korean proper nouns, such as `라피`→`rapi` or `아스나`→`asuna`, are also included in the visual phrases. The image result cards sequentially display thumbnail previews in their original proportions in the background.

Excel files are read row by row, and sheet names and cell values are indexed locally up to 500,000 cells. For example, even if the file name is only `Account List.xlsx`, if the searched equipment name is in a cell, the file name clue and cell clue are combined and prioritized as a `Name/Content Match`. Legacy `.xls`, standard `.xlsx`, macro `.xlsm`, and binary `.xlsb` formats are supported.

Even if asked indirectly, such as `files related to mullvad login`, the relationship between `login` and `account/계정/credential` is calculated together. Results where the search intent is fully confirmed in the file name take precedence over text word match results inside the installation folder. Even if individual AI inferences fail, the already calculated name, path, and text results are preserved instead of showing a blank screen. Even if context words are present together, like `network account related documents`, direct Korean compound noun clues like `ITTeam_AccountManagement` and `AccountManagementDocument` are shown first. General `document` requests include spreadsheets and presentations as well as PDFs and text.

## Currently Implemented Features

- Browse local/removable drives and currently connected network servers/shared folders from `This PC`.
- Automatic detection of UNC shared folders confirmed in the current Windows SMB session.
- Register folders, shared folders, `.lnk`, and `.url` shortcuts by dragging them into the `Favorites` tree under Quick Access.
- Drag guidance at the bottom left, folder right-click `Add to Favorites`, and drag-to-reorder inside favorites.
- Favorites right-click rename/remove, and permanent saving to the configuration file.
- Immediately after registering a favorite, the folder undergoes low-load automatic indexing before general drives.
- Choose whether the close button switches to the system tray or completely exits, depending on options.
- Double-click the tray icon to reopen, right-click to pause/resume indexing or exit completely.
- Access network shares via `This PC`, address input, or favorites without a separate network tree.
- Open general shared folder lists by entering only the top-level server address, like `192.168.0.10` or `\\NAS`.
- Windows reconnection/credential prompt and connection confirmation for disconnected network drives.
- Back, forward, up one folder, address input, and refresh.
- Support for mouse side back/forward buttons and keyboard BrowserBack/BrowserForward.
- Explorer-style shortcut keys: `Alt+←/→/↑`, `Backspace`, `Ctrl+L` / `Alt+D`, `Ctrl+F` / `Ctrl+E` / `F3`, `F5`, `Ctrl+W`.
- Open files and folders, new folder, copy, move, rename, and delete to Recycle Bin.
- Record photo/document viewers newly launched by AI Explorer and clean them up together when the app closes.
- Drag-and-drop copy and `Shift` drag-and-drop move.
- Windows Shell native folder, file format, and drive icons.
- Sort by name, date, type, and size.
- Search scopes for current folder and subfolders, current drive, favorites, and all locations.
- Default search scope is `Current folder and subfolders`.
- Exclude hidden/system items and temporary files/folders starting with `~` from navigation and all search indexing.
- File type intent classification for 3D models, CAD, documents, images, video, etc.
- Search uncatalogued extensions like `.ppk` or `.pem` via dot notation and file name metadata.
- Link natural language mixed with Korean and English like `aws ssh키` to the file name semantics of `key`, `ppk`, and `pem`.
- Maintain single-character core nouns like `AWS 키` as search intent after removing particles.
- Include the names, extension meanings, and parent paths of all regular files in the non-public metadata semantic index.
- Re-rank by combining file name, path, format, text, and E5 rankings without discarding weak candidates early.
- Specify weights, strengths, and percentages in natural language for creation date, modification date, name, path, format, text, semantics, and size.
- New searches are executed only by a deterministic interpreter, preventing the LLM from adding word, extension, or file/folder restrictions.
- The LLM explanation in 'Find again in results' also cannot change the verification conditions directly entered by the user.
- Find again via natural language strictly within the two current result panes and revert exactly to `To all results`.
- Interpret the inclusion of Korean, English, or numbers as conditions for file names, text, OCR, and Excel cells.
- Interpret `Files completely in Korean` and similar expressions as a dedicated condition for file names excluding the extension.
- Distinguish between `Korean file` and `Files containing Korean` as conditions for HWP format versus text inclusion.
- Immediately compress the re-search within results into a single-line condition to secure height for the result list.
- Horizontally split the navigation tree, file list, unified results, and quick name/path results, and automatically save the widths.
- Display unified results and quick name/path results simultaneously, switching to a selectable view only in narrow windows.
- Index creation and modification dates separately, applying user priorities within the relevance protection stage.
- Interpret modification time conditions such as today, yesterday, last week, last month, and this year.
- Local storage and reuse of scoped metadata, text, and semantic vector indices.
- OCR and pixel semantic search for JPG, PNG, BMP, GIF, TIFF, WebP, and HEIC images.
- OCR and screen semantic search for first, middle, and last page samples of PDFs.
- Low-load automatic indexing that sequentially checks all ready locations after launching the app.
- Display evidence for each result: `Exact Match`, `Name/Content Match`, `Text Match`, `Path Match`, `AI Candidate`, and `Visual Candidate`.
- Low-load asynchronous previews of image search result cards.
- Pixel-level virtualized scrolling of search results and pre-allocation of preview space.
- File name, folder name, and WD tag identity verification for character name image searches, excluding UI screenshots.
- Optional `Precision Re-evaluation` that re-sorts the top 50 results post-search using the full 768 dimensions of E5.
- Low-spec sequential indexing that prioritizes the user's Desktop, Documents, and Downloads.
- Precision file name and format re-search beyond the 100,000-item metadata limit.
- Collect images and PDFs outside the metadata limit as visual AI candidates even without name matches.
- Parent folder searches also evenly cyclically analyze image candidates by child folder.
- Maximum of 500 results per search, up to 240 semantic AI candidates, and up to 500 visual AI candidates.
- Immediately toggle between independent title searches and unified search results via bottom tabs.
- Single name searches display title index results immediately in both panes and skip AI inference.
- Read only completed indices during searches, and perform missing index generation during idle times separate from the search.
- Include file names and relative paths of 3D, CAD, archive, image, and video files without text in the E5 semantic index.
- Retain already found results even if stopped during an incremental search, and resume uncompleted indexing during the next idle time.
- Exception handling for search cancellation, access denied, corrupted documents, and disconnected networks.
- Check actual data path and usage in settings and safely change the save location.
- Save settings and indices in `_AIExplorer_Data` next to the EXE.
- Automatically switch to `%LOCALAPPDATA%\AIExplorer` in write-protected locations.

## Text Extraction Formats

- General Text: TXT, Markdown, CSV, TSV, LOG, JSON, XML, YAML, INI
- Source Code: C#, XAML, JavaScript, TypeScript, Python, Java, C/C++, Go, Rust, PHP, HTML, CSS, SQL, PowerShell, BAT, CMD
- Document Containers: DOCX, PPTX, XLSX, HWPX, ODT, ODS, ODP
- Image OCR: JPG, JPEG, PNG, BMP, GIF, TIFF, WebP, HEIC
- PDF: OCR by rendering up to 3 sample pages

Text files save the first 256KB, and extraction results save up to 12,000 characters. Encrypted documents, corrupted documents, and document containers exceeding 32MB are skipped. OCR uses the Windows language packs installed on the PC, and to protect low-spec systems, text OCR is skipped if images exceed 48MB or PDFs exceed 96MB. Since it sample-analyzes the first, middle, and last pages rather than the entire PDF pages, full-page search per page is not yet supported.

## Built-in Local AI

`build_release.cmd` verifies the following components with SHA-256 and includes them in the portable distribution folder. If bundle files are missing, such as during development execution, the same components are downloaded once upon the first run.

- [Multilingual E5 Base](https://huggingface.co/intfloat/multilingual-e5-base) Q4_K_M GGUF conversion (approx. 219MB)
- [SigLIP 2 Base Patch16 224](https://huggingface.co/google/siglip2-base-patch16-224) INT8 ONNX conversion and SentencePiece tokenizer (approx. 382MB)
- [WD ViT Tagger v3](https://huggingface.co/SmilingWolf/wd-vit-tagger-v3) ONNX model and fixed tag dictionary (approx. 379MB)
- [Qwen3 1.7B](https://huggingface.co/Qwen/Qwen3-1.7B) Q4_K_M GGUF conversion (approx. 1.28GB)
- [ONNX Runtime DirectML](https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider.html) 1.22.0 and CPU fallback
- [Microsoft.ML.Tokenizers](https://www.nuget.org/packages/Microsoft.ML.Tokenizers) 2.0.0
- [llama.cpp](https://github.com/ggml-org/llama.cpp) Windows x64 CPU executor

The total estimated download size is about 2.4GB, and at least 4GB of free space is checked during the initial installation or automatic recovery. E5 input is processed by separating `query:` and `passage:`, which is the recommended format for the model, and the 768-dimensional output is normalized and saved without reduction. The index file quantizes each dimension into 8 bits to reduce memory usage during searches.

The execution conditions for a new search are finalized by the deterministic interpreter. Qwen3 can assist in explaining 'Find again in results', but it cannot alter execution conditions by adding new word, extension, file/folder restrictions, or sorting to the `SearchPlan`. The processed context does not leave the authenticated llama.cpp server at `127.0.0.1`.

SigLIP 2 uses a common multilingual image/text space. It improves the recall rate of general objects, scenes, office materials, and illustration searches by comparing the original text of the search sentence, English visual expressions, and English transliterations of Korean proper nouns against multiple phrases. Vertically or horizontally long images are preserved as separate vectors: a white background frame showing the entirety, and a central detailed frame. The WD tagger extracts character names and appearance tags from images identified as anime/illustrations, and rating tags are used only as search evidence, not as filters to hide results. When a DirectML session is created, Intel/AMD integrated graphics are prioritized, and if it fails due to driver or computing compatibility issues, the same model is reopened as a CPU session to continue the search.

The models and executors are stored in the following locations:

```text
_AIExplorer_Data\models\semantic
_AIExplorer_Data\models\visual

```

When you turn on the app, it checks the favorites folder before general drives, then sequentially checks the prepared locations in the background, gradually preparing metadata, text, and OCR indices, as well as semantic and visual vectors. Even if you press the window's close button, it continues running in the system tray, and in this tray state, it rechecks low-load indexing every 5 minutes to avoid missing modified files. When the user starts a search, automatic indexing yields immediately. Foreground searches read the already secured file name, path, text, OCR, E5, SigLIP, and WD tag indices once, and do not repeat the same search to create new indices. Missing indices are continually prepared in idle background tasks after the search ends.

Even if you press `Stop` during a search, the results found so far do not disappear. Files that could not be finished in the first incremental analysis are subsequently indexed when the program becomes idle, making the next search faster. In the fast first stage, it prioritizes existing indices and file names/paths without running AI models. Saved AI vectors are searched before analyzing new files, and the text, semantic, and visual indices newly created during an active search proceed up to a maximum of 4 stages. The remaining tasks are passed to idle time to maintain input and navigation responsiveness.
To actually exit the program, right-click the tray icon and select `Exit completely`. From the same menu, you can pause or resume background indexing. Images or PDFs that are corrupted or currently undecodable save their failure time and are not reprocessed repeatedly in the same search, retrying when the file changes or after 24 hours have passed.

The default semantic index saves the full 768 dimensions of E5 as an 8-bit vector. The `Precision Re-evaluation` of search results re-compares the top 50 as uncompressed 768-dimensional floating-point vectors without installing additional large models. It also analyzes the file names and relative paths of files from which text extraction is impossible, and the model executor unloads the model from memory if not used for 5 minutes.

The internet is only used when downloading models and executors. File contents and search terms are not sent to external servers but are processed on a temporarily hosted, authenticated local executor at `127.0.0.1`.

AI models cannot be replaced or upgraded independently in the settings screen. Models, tokenizers, and search score criteria are fixed along with the app version, and only missing or corrupted components are automatically restored by the app using the same fixed bundle.

The current path and usage are displayed in the settings' save location. When changing the location, models, settings, and indices are copied to the new `_AIExplorer_Data` folder, preserving the original, and the new location is used after restarting the app.

## Development Execution

Windows 10/11 and the .NET 10 SDK are required.

1. Run `run_dev.cmd`.
2. Alternatively, open `AIExplorer.sln` in Visual Studio.

`NuGet.Config` in the project root locks the packages required for the build to be restored from the official nuget.org feed. If the `NU1100` error persists, verify whether `https://api.nuget.org/v3/index.json` is accessible in your browser, and ensure that your firewall, proxy, or security software is not blocking `api.nuget.org`.

## Verification and Portable AI Bundle Build

* `verify_source.cmd`: Release build and search/file service smoke testing
* `build_release.cmd`: Bundles the self-contained EXE, AI models, and CPU executor after verification
* Result: `dist\AIExplorer_v0.82.4-win-x64-portable.zip`

The EXE in the distribution folder contains the .NET runtime, so there is no need to separately install .NET or AI models on the target PC. Unzip and run while maintaining the folder structure. The .NET 10 SDK is only needed on the PC where you run development scripts or build a new distribution.

## Main Shortcut Keys

| Key | Function |
| --- | --- |
| `Alt+←` / `Alt+→` | Back / Forward |
| `Alt+↑` | Parent folder |
| `Ctrl+L` | Enter path |
| `Ctrl+Shift+N` | New folder |
| `Ctrl+C` / `Ctrl+X` / `Ctrl+V` | Copy / Cut / Paste |
| `F2` | Rename |
| `Delete` | Move to Recycle Bin |
| `F5` | Refresh |

## Safety Principles

* The search layer does not automatically modify files.
* The default deletion is not a permanent deletion but a move to the Windows Recycle Bin.
* The default drag-and-drop action is copying, and moving requires holding `Shift`.
* If an error occurs during a copy or move, only the newly created incomplete target is cleaned up, and the original is preserved.
* Network accounts and passwords are not saved; the current Windows login session is used.
* Exit cleanup targets only processes newly launched by AI Explorer, and does not terminate already running photo/document apps or processes opened separately by the user.

## Log Locations

App logs and local AI executor logs are saved in the following locations.

```text
_AIExplorer_Data\logs\AIExplorer_YYYYMMDD.log
_AIExplorer_Data\models\semantic\local-ai-runtime.log

```

If the folder containing the executable is write-protected, check under `%LOCALAPPDATA%\AIExplorer`. Errors during the build process can be saved separately using the following command.

```powershell
.\build_release.cmd > build_log.txt 2>&1

```

## Not Yet Implemented Features

* Index management screen, manual full re-indexing, and real-time reflection of file system changes
* Text extraction for all PDF pages and legacy DOC/XLS/PPT/HWP
* Display evidence sections per page/slide/sheet for OCR and documents
* Internal rendering of PSD/RAW/SVG and visual search for video frames
* Detailed selection window upon file collision and per-file transfer progress

Detailed internal structures and next steps are outlined in `docs/ARCHITECTURE.md` and `docs/ROADMAP.md`.

```

```
