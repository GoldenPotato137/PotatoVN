# GalgameManager Client (PotatoVN) - Detailed Knowledge Base

> After finishing editing, please remember to run GalgameManager.Test to ensure no tests are broken.
>
> If you did not change code about parser, you can skip running parser tests since they are making request to external services and can be very slow.

This document provides a detailed overview of the `GalgameManager` client application, which is the primary user-facing component of the PotatoVN project. It is intended for AI agents and developers needing a deeper understanding of the client's architecture, features, and key code areas.

## 1. Overview

*   **Component Name:** `GalgameManager`
*   **Type:** Client Application (Desktop)
*   **UI Framework:** WinUI 3
*   **Primary Role:** To provide a convenient game management platform for visual novel enthusiasts.
*   **Part of Solution:** `GalgameManager.sln` (Project Path: `GalgameManager/GalgameManager.csproj`)

## 2. Core Features

The client application implements the core functionalities of PotatoVN:

*   **Game Discovery:** Automatically searches specified folders to find visual novel games.
*   **Information Fetching:** Retrieves game metadata (details, cover art, etc.) from multiple online databases (currently supports Bangumi and Visual Novel Database).
*   **Status Synchronization:** Synchronizes game play status (e.g., playing, completed, planned) with user accounts on supported platforms (e.g., Bangumi).
*   **Cloud Save Sync (Conceptual):** Facilitates tracking of game save locations. Actual synchronization to cloud storage relies on third-party sync software (e.g., OneDrive, NextCloud) monitoring the designated save folders.
     *   **Playtime Tracking:** Monitors and records the time spent playing games. This includes individual play sessions (`PlayedTime`) and total play count (`PlayCount`). Both `PlayCount` and `PlayedTime` are synchronized with the `GalgameManager.Server`.
     *   **Process Detection:** `Helpers/GameProcessDetector.cs` finds processes whose executable path (via `ProcessExtensions.TryGetExecutablePath`, i.e. `QueryFullProcessImageName` — works across 32/64-bit, unlike `Process.MainModule`) lies under the installation directory. `GameLaunchService` uses it at launch only for Steam (no process object available); otherwise it tracks the started process directly. `RecordPlayTimeTask` handles launcher-style games (bootstrapper exits after spawning the main process): when the tracked process exits, it re-checks the install directory every 1s for up to 10s and attaches to a newly-appeared process (only processes not present when tracking started are candidates; `RecordPlayTimeTask.ProcessName` is updated in-memory for crash/tray recovery). Auto-detection never writes `LocalInstallationConfig.ProcessName` — that remains reserved for explicit manual selection (`SelectProcessDialog`), which is the fallback when directory detection fails (e.g. the real process lives outside the install directory).
    *   **Automated Game Processing:** Can extract games from compressed archives, attempt to identify them, and add them to the user's library.
    *   **Magpie Integration:** Allows users to toggle the use of Magpie (a screen scaling tool) for individual games. A global override setting is also available to always enable Magpie, regardless of individual game settings.

## 3. Architecture and Technology

*   **Programming Language:** C#
*   **Framework:** .NET
*   **UI Technology:** WinUI 3 (Windows UI Library 3)
*   **Architectural Pattern:** MVVM (Model-View-ViewModel)
    *   **Models:** Represent the application's data and business logic (found in `GalgameManager/Models/` and potentially `GalgameManager.Core/Models/`).
    *   **Views:** Define the UI structure and appearance (XAML files in `GalgameManager/Views/`).
    *   **ViewModels:** Act as intermediaries between Views and Models, handling UI logic and state (found in `GalgameManager/ViewModels/`).
*   **Development Framework Base:** Initially generated using TemplateStudio, providing a standardized project structure and common patterns.
  *   **Localization:** Supports multiple languages through localization files, managed via Crowdin (configuration in `crowdin.yml` at the repository root). String resources are located in `GalgameManager/Strings/` within language-specific subfolders (e.g., `zh-CN`, `en-US`), typically in `Resources.resw` files.
      *   **Implementing XAML Localization:**
          *   Use the `x:Uid` attribute on XAML elements to mark them for localization. For example: `<TextBlock x:Uid="MyUniqueControlUid" />`.
          *   In the `.resw` resource file (e.g., `Strings/zh-CN/Resources.resw`), create a `<data>` entry where the `name` attribute is the `x:Uid` value followed by a dot and the target property name. The property name varies by control type:
              *   `TextBlock`, `TextBox`, etc.: Use `.Text` (e.g., `MyUid.Text`)
              *   `AppBarButton`, `Button`: Use `.Label` (e.g., `MyUid.Label`)
              *   `ContentDialog`: Use `.Title` for titles
              *   `ToolTip`: Use `.ToolTipService.ToolTip`
          *   Example for TextBlock:
              *   XAML: `<TextBlock x:Uid="EditPlayTimeDialog_PlayCountLabel" />`
              *   `Resources.resw` entry:
                  ```xml
                  <data name="EditPlayTimeDialog_PlayCountLabel.Text" xml:space="preserve">
                    <value>游玩次数:</value>
                  </data>
                  ```
          *   Example for AppBarButton:
              *   XAML: `<AppBarButton x:Uid="GalgamePage_OpenInSteam" />`
              *   `Resources.resw` entry:
                  ```xml
                  <data name="GalgamePage_OpenInSteam.Label" xml:space="preserve">
                    <value>在Steam中打开</value>
                  </data>
                  ```
          *   The application's `GetLocalized()` extension method (found in `GalgameManager.Helpers.StringExtensions.GetLocalized()`) is used in C# code to retrieve localized strings, e.g., `Title = "EditPlayTimeDialog_Title".GetLocalized();`. This implies that for C# string localization, the resource key is used directly without a property suffix.
          * When Editing localizations files, you should *never* directly read or edit the .resw files.
          * Instead, you should call the python script `Strings/resw_tool.py` to search string or edit the string in the .resw files.
          * `resw_tool.py` must surgically edit `.resw` text (insert/replace/delete `<data>` blocks). Never rewrite the whole file via `xml.etree.ElementTree.ElementTree.write()` — that drops the ResX schema comment, remaps `xsd`/`msdata` namespaces, and produces huge noisy diffs.
          * Usage (note: on Windows PowerShell, use semicolon `;` to separate commands):
              ```bash
            cd GalgameManager/Strings #重要，这个脚本应该在Strings目录下运行
              # 搜索所有包含 "Theme" 的key
            python resw_tool.py search "*Theme*"
            # 搜索以 "Settings_" 开头的key
            python resw_tool.py search "Settings_*"
            # 搜索确切的key
            python resw_tool.py search "AppDisplayName"
            # 更新设置项
            python resw_tool.py update "Settings_Theme.Text" en-US="Theme" ja-JP="テーマ" zh-CN="主题"
            # 添加新的key
            python resw_tool.py update "NewFeature.Title" en-US="New Feature" ja-JP="新機能"
            # 删除key
            python resw_tool.py delete "ObsoleteKey.Text"
            # 校验所有语言 resw 是否合法（update/delete/normalize 写入后也会自动校验，失败则回滚）
            python resw_tool.py validate
            # On Windows PowerShell (recommended approach):
            cd GalgameManager/Strings; python resw_tool.py update "NewKey.Text" en-US="English" zh-CN="中文"
            ```

## 4. Key Files and Directories within `GalgameManager/`

This section highlights important files and directories specific to the client application.

*   **`GalgameManager.csproj`**: The MSBuild project file. Defines dependencies (NuGet packages, project references), build configurations, and included files for the client application.
*   **`App.xaml` / `App.xaml.cs`**:
    *   `App.xaml`: Declares application-level resources and styles.
    *   `App.xaml.cs`: The application's entry point. Handles application lifecycle events (startup, activation, suspension), initializes services, and sets up the main window.
*   **`MainWindow.xaml` / `MainWindow.xaml.cs`**:
    *   `MainWindow.xaml`: Defines the XAML structure for the main application window.
    *   `MainWindow.xaml.cs`: Contains the code-behind logic for the main window, including event handlers and interaction with ViewModels.
*   **`appsettings.json`**: Configuration file for the client application. May store settings like API keys (if not user-specific), default paths, feature flags, etc. Note that user-specific settings are typically managed by `LocalSettingsService.cs` and stored in `LocalSettings.json` or individual `data.{key}.json` files.
    *   **`ViewModels/`**: Contains ViewModel classes that drive the application's UI logic and data binding. These ViewModels often orchestrate interactions with dialogs for editing specific pieces of data (e.g., `PlayedTimeViewModel.cs` launching `EditPlayTimeDialog`).
        *   `SettingsViewModel.cs`: Manages application settings, including the `CustomTextFileExtensionsString` property for user-defined text file extensions, the `MagpiePath` property (with `SelectMagpiePathCommand`) for the Magpie executable path, and the `AlwaysEnableMagpie` property for globally overriding Magpie settings. It also includes a `IsSideloadVersion` property to control visibility of certain settings for non-Store versions of the application.
        *   `GalgameViewModel.cs`: Handles logic for the individual game page, including the "Open Text" feature which now uses the `CustomTextFileExtensions` setting. It also checks the global `AlwaysEnableMagpie` setting when determining Magpie activation for a game. Contains "Open in X" commands for external services like Bangumi, VNDB, Ymgal, Cngal, Hikarinagi, and Steam; the game details page groups the available commands under a single external-website flyout.
    *   `ShellViewModel.cs`: Manages the main application shell. It subscribes to `InfoService.OnEvent` and its `DisplayEventMsgAsync` method now handles the optional callback action and button text for event notifications. It creates `ShellEventViewModel` instances for display.
    *   `ShellEventViewModel.cs` (within `ShellViewModel.cs`): Represents an event notification displayed in the shell. It now includes `CallbackAction` and `CallbackButtonText` properties, along with an `ExecuteCallbackCommand` to invoke the action.
*   **`Views/`**: Contains XAML files defining the user interface pages and controls. Each View typically corresponds to a ViewModel.
    *   `SettingsPage.xaml`: Contains the UI for application settings, including the "Magpie executable path" setting in the "Game" section.
    *   `HomePage.xaml`: Contains the game list filter Flyout. Its filter panel visibility is controlled by settings keys in `KeyValues.cs` and properties/command in `HomeViewModel.cs`; `HomeFilterPanelVisibilityDialog` is the customization dialog for showing/hiding filter panels. The category (custom `CategoryGroup`) panel defaults to hidden (`HomeFilterShowCategoryPanel`), but `HomeViewModel.UpdateCategoryPanelForceVisibility()` force-shows it (`ForceShowCategoryPanel`) whenever an active `CategoryFilter` is not in the Status/Developer/Engine groups, so such filters are always removable from the panel.
    *   `GalgameSourcePage.xaml`: Contains the UI for individual game library configuration, including settings for auto-scan, auto-add/remove games, and per-library SaveMetaBackup toggle.
    *   `PluginPage.xaml` and `PluginViewModel.cs`: Contain the installed-plugin list and user-facing plugin add/remove flows; package install flows should extract archives before calling `IPluginService.AddPluginAsync` with `isDev: false`.
    *   `PluginStorePage.xaml`: Displays the list of available plugins. It uses `StorePlugin` as the data model and inlines the plugin item template (previously `PluginPrefab`) to display plugin details like name, short description, and logo.
    *   `ShellPage.xaml`: The main shell of the application. Its `ItemsRepeater` for displaying event notifications now includes a `HyperlinkButton` that is visible when `CallbackButtonText` is provided in the `ShellEventViewModel`. This button is bound to the `ExecuteCallbackCommand` and uses `VisibilityHelper.Convert` for its visibility.
    *   `ShellPage.xaml` / `ShellPage.xaml.cs`: Built-in sidebar entries remain declared in XAML, while plugin-provided sidebar buttons are inserted dynamically at runtime from `ISidebarService` snapshots.
*   **`Views/Dialog/`**: This subdirectory commonly houses `ContentDialog` XAML files used for focused editing tasks or user prompts (e.g., `EditPlayTimeDialog.xaml` for modifying game play history). These dialogs usually have a corresponding `.xaml.cs` for their logic and are instantiated and shown from ViewModels.
        *   `AddSourceDialog.xaml`: Dialog for adding new game library sources. Uses a ComboBox to select library type (local folder, archive/zip, Steam). The SelectedIndex is bound to the SelectItem property which determines the library type in LibraryViewModel.AddLibrary method.
        *   `ChangeSourceDialog.xaml`: Dialog for moving a game between libraries. The caller (`GalgameViewModel.MoveToSource`) drives the move via `IGalgameSourceCollectionService.MoveAsync`, which returns a `SourceMoveTask`; the dialog itself only collects choices. When moving out of a local folder or archive library, an extra checkbox is shown to optionally delete the on-disk files (game folder / archive) via `MoveAsync(deleteFiles: true)`.
        *   `MixedPhraserEnabledDialog.xaml`: Dialog for configuring which search engines/databases are enabled in the mixed phraser. Contains checkboxes for Bangumi, VNDB, Ymgal, Steam, and Hikarinagi. The dialog receives a `MixedPhraserEnabled` configuration object and allows users to enable/disable individual phrasers.
        *   **ContentDialog Localization Pattern**: ContentDialog elements follow specific localization conventions:
            *   Use `x:Uid` attribute on the ContentDialog root element for title and button text
            *   Localization keys use `.Title`, `.PrimaryButtonText`, `.SecondaryButtonText` suffixes
            *   Child elements like CheckBox use `.Content` suffix for their text content
            *   Example: `<ContentDialog x:Uid="MyDialog">` with localization key `MyDialog.Title`
*   **`Models/`**: Contains data model classes representing the entities and data structures used within the client.
    *   **`ScanResult.cs`**: Defines models for storing and displaying the results of a game source scan.
        *   `GalgameScanResult`: Represents the overall result of a scan operation for a specific source, including `SourceId`, `SourceName`, `ScanTime`, and a list of individual path results. Stored in LiteDB.
        *   `PathScanResultItem`: Represents the outcome of scanning a single path, including the `Path`, `ResultType` (e.g., Success, AlreadyExists, Failed), and a `Message`.
        *   `ScanResultType`: Enum defining the possible outcomes for a path scan (Information, Success, AlreadyExists, Failed).
    *   **`Models/Sources/GalgameSourceBase.cs`**: The base class for all game library sources, containing common properties and functionality:
        *   `ScanOnStart` (bool): Whether to automatically scan this library when the application starts.
        *   `CheckOnStart` (bool): Whether to check if the library and games still exist when starting the application. When disabled, the application will skip checking for non-existent libraries and games during startup, which can be useful for libraries on removable drives or network locations that may not always be available.
        *   `Detect` (bool): Whether to enable automatic detection of changes in the library folder.
        *   `SaveMetaBackup` (bool): Whether to save meta backup (meta.json and cover images) for games in this source. Defaults to false. This replaced the global `SaveBackupMetadata` setting to allow per-source control of meta backup functionality.
        *   Derived classes include `GalgameFolderSource`, `GalgameZipSource`, and `VirtualSource`.
    *   **`Models/Sources/GalgameZipSource.cs`**: Represents a local archive library (a folder of compressed game packs: zip/rar/7z/001). Game entries point to real archive file paths. `FxRegex` extracts the pack name (`game.part1.zip` -> `game`) used both for scanning and for the `.PotatoVN/<PackName>` meta-backup folder. `GetPackName` is the single source for both.
    *   **`Galgame.cs`**: A key model representing a game. It includes various properties like `Name`, `ImagePath`, as well as fields for tracking play history such as:
        *   `PlayedTime` (Dictionary<string, int>): Stores individual play sessions, mapping a date string to play duration in minutes.
        *   `PlayCount` (int): Stores the total number of times the game has been played.
        *   `TotalPlayTime` (int): Stores the sum of all play session durations in minutes.
        *   `MuteInBackground` (bool): A per-game setting to determine if the game audio should be muted when the application is not in the foreground.
        *   `PvnUpdate` (bool): A flag indicating if the game's data needs to be synced with the server.
        *   `PvnUploadProperties` (enum `PvnUploadProperties`): A flags enum specifying which particular properties of the game need to be uploaded to the server. The `PlayTime` flag is used to indicate that `PlayedTime`, `TotalPlayTime`, and `PlayCount` should be synced.
        *   `Ids` (string?[]): An array storing IDs from different data sources (Bangumi, VNDB, etc.). The array size is defined by `PhraserNumber` constant. All methods accessing this array include bounds checking to prevent `IndexOutOfRangeException` for legacy data with smaller arrays.
    *   **Plugin Models**:
        *   **`PluginX.cs`**: Represents a loaded plugin at runtime. It wraps the `IPlugin` instance and contains metadata like `Info`, `LoadContext`, and enabled status. It handles UI retrieval with timeout protection.
        *   **`StorePlugin.cs`**: Represents a plugin as displayed in the plugin store. It is a lightweight model used specifically for the store UI to avoid confusion with active plugins (`PluginX`). It includes properties like `DescriptionShort` for concise display in the store list.
    *   **Plugin Host API**:
        *   **`GalgameManager.WinApp.Base/Contracts/IPotatoVNApi.cs`** defines the host API surface exposed to plugins.
        *   The host API covers game lookup/lifecycle/parsing, local installation configuration, source management, categories, staff, game-list filters, plugin data, messaging/notifications, background tasks, host state, navigation/sidebar integration, and UI-thread/image utilities. Implementations are grouped in `GalgameManager/Services/PluginService/PluginService_ApiHost*.cs` partials and should delegate to the existing application services rather than duplicate business logic.
        *   Types used in `IPotatoVNApi` signatures must live in `GalgameManager.WinApp.Base`, which is the plugin SDK assembly. When an existing public type is moved there from the app assembly, keep a `TypeForwardedTo` entry in the app for binary compatibility.
        *   Plugin-facing collection APIs return collection snapshots. For mutations, the host resolves plugin-supplied games and sources back to the current application objects by stable UUID/ID before delegating, and local installation configuration is copied on both read and write.
        *   It exposes game creation helpers for plugins: `AddGameInstallation(...)` for local folder installations and `AddVirtualGame(...)` for non-local placeholder entries. These calls may show parse confirmation UI unless `requireConfirm` is disabled.
        *   Destructive plugin APIs keep physical file deletion opt-in (`removeFromDisk` / `deleteFiles` default to `false`); source deletion retains the host confirmation dialog.
        *   It exposes **game list page filters** (GameListPage) operations for adding/removing/clearing/replacing filters, searching available filters, applying the active filter set, and reading the current filter list via `GetFiltersAsync()` (snapshot).
        *   It exposes navigation helpers for both built-in pages (`PageEnum`) and plugin-owned WinUI `Page` types; plugin page navigation validates the page comes from the current plugin assembly, then routes through a built-in host page that instantiates the plugin page under the correct plugin XAML scope, avoiding direct `Frame.Navigate` failures for dynamically loaded page types.
        *   It exposes sidebar button registration APIs so plugins can add or remove shell sidebar entries with placement metadata and host-managed click dispatch.
        *   Plugin UI interfaces include full game detail page replacement and additive left/right panel injection points for the built-in game detail layout.
        *   The filter base types are in the shared library: `GalgameManager.WinApp.Base/Models/Filters/FilterBase.cs` and `GalgameManager.WinApp.Base/Contracts/IFilter.cs`.
 *   **`Services/`**: Houses service classes that encapsulate specific functionalities, such as:
     *   `PluginService.cs`: The plugin loader now registers each loaded plugin assembly with the host WinUI XAML metadata pipeline before plugin initialization, so plugin XAML can resolve nested custom controls/UserControls when the plugin output includes its generated `.pri` and compiled XAML resources.
     *   `PluginService.cs`: The plugin loader recognizes a plugin by scanning DLLs in the selected plugin output folder and finding a non-abstract type that implements `IPlugin`; it does not rely on the DLL name matching the folder or template project name.
     *   `SidebarService.cs`: Centralizes shell sidebar button metadata, plugin sidebar registrations, and persisted visibility settings for both built-in and plugin buttons.
     *   `CategoryService.cs`: Manages category groups/categories and publishes `CategoryGroupChangedArg` through `IMessenger` only for category-group structural changes (group add/remove, category add/remove from a group), so reactive UI/filter features can refresh without reacting to category content edits. Developer category image downloads are queued through `IBgTaskService` rather than a dedicated worker thread.
    *   `AccountServices/PvnService.cs`: Handles communication with the `GalgameManager.Server`, including uploading game data. It uses `PvnSyncTask.cs` for background synchronization.
    *   Fetching data from local or remote sources.
    *   File operations.
    *   Navigation within the application.
    *   Interaction with external APIs.
    *   `ScanResultService.cs`: Manages saving and retrieving `GalgameScanResult` objects to/from LiteDB. Implements `IScanResultService.cs`. The LiteDB collection name is "scan_results".
    *   `LocalSettingsService.cs`: Manages the storage and retrieval of local application settings, including the LiteDB database instance.
    *   `InfoService.cs`: Handles in-app notifications and event logging. Its `OnEvent` delegate and `Event` method now support an optional callback action and button text, allowing event notifications to include a custom action button. This is handled in `ShellViewModel.cs` and displayed in `ShellPage.xaml`.
    *   **`Services/SourceService/`**: Contains source-specific service implementations that handle different types of game libraries:
        *   `LocalFolderSourceService.cs`: Handles local folder-based game libraries, including meta backup/restore functionality, file system monitoring, and game move operations.
        *   `SteamSourceService.cs`: Handles Steam-based game libraries. Supports meta backup/restore functionality by creating `.PotatoVN` folders within Steam game directories to store `meta.json` and associated images. Does not support move operations or file system monitoring due to Steam's managed nature.
        *   `ZipSourceService.cs`: Handles local archive libraries (`GalgameSourceType.LocalZip`). Meta backup is stored under the archive library root: `<root>/.PotatoVN/<PackName>/meta.json` (PackName from `GalgameZipSource.GetPackName`, which strips `.part1`/extension). Move-in means "pack the game folder into a zip via `PackGameTask`" (the task also writes the sidecar backup immediately after packing); `SourceMoveTask` then registers the entry using the task's real `ZipPath`. Move-out of an archive library is currently not supported (unpack via the target local library's *Add game from archive* instead). Startup existence check for zip entries uses `File.Exists(entry.Path)` and honors the source's `CheckOnStart` flag.
        *   `VirtualSourceService.cs`: Handles virtual game libraries for organizational purposes.
        *   All source services implement `IGalgameSourceService` interface which defines standard operations like `SaveMetaAsync`, `LoadMetaAsync`, `RemoveMetaAsync` for meta information management.
 *   **`Helpers/`**: Contains utility classes and extension methods that provide common, reusable functions (e.g., file I/O helpers, string manipulation, UI helpers).
     *   `VisibilityHelper.cs`: Provides converters for XAML bindings, e.g., converting a string's null/empty status to a `Visibility` value.
     *   `GalgameManager/Services/PluginService/PluginXamlHost.cs`: Bridges dynamically loaded plugin assemblies into the host app's WinUI XAML system by loading plugin PRI resources and registering plugin `IXamlMetadataProvider` implementations before plugin UI is initialized. Its dev-plugin hot-reload staging directory (`HotReloadRoot`) must live under `AppStoragePaths.LocalDataPath`, never `AppContext.BaseDirectory`: for MSIX installs the base directory is the read-only `C:\Program Files\WindowsApps\...` folder, so writing there throws `UnauthorizedAccessException` (issue #693). In general, never write runtime files under `AppContext.BaseDirectory`; use `AppStoragePaths.LocalDataPath`/`TempPath` (they handle MSIX, portable, and unpackaged cases).
    *   **`Helpers/Phrase/`**: Contains the mixed phraser system for aggregating game information from multiple sources:
        *   `MixedPhraser.cs`: The main mixed phraser implementation that combines data from multiple game information sources (Bangumi, VNDB, Ymgal, Steam, Hikarinagi). It supports selective enabling/disabling of individual phrasers through the `MixedPhraserEnabled` configuration. `GetGalgameInfo` waits on all parallel source tasks with a unified soft timeout (`TimeoutSeconds`); incomplete/faulted sources are dropped before merge.
        *   `VndbPhraser.cs`: VNDB queries that use the `search` filter must sort by `searchrank`; ID-based queries should keep their native ordering. API calls that may be throttled should use the shared retry wrapper so a retry preserves the original query.
        *   `HikarinagiPhraser.cs`: Routes requests through the PotatoVN server proxy. On HTTP 429 it waits (honoring `Retry-After`, default 60s to match the server's 1-minute window) and retries up to 3 times, reporting a localized countdown (key `HikarinagiPhraser_RateLimitWaiting`) to the parsing-status UI via `GalgameParsingEventArgs`. Takes an optional `IMessenger` constructor arg (wired from `GalgameCollectionService`, mirroring `MixedPhraser`). Search items expose only one display title (often a translation), so when the best title/subtitle similarity is below 0.9, `SearchAsync` fetches details for the top 3 candidates and re-matches against `origin_title`/`trans_title` (fixes e.g. Japanese query "ライムライト・レモネードジャム" picking id 8008 instead of 1041).
        *   `MixedPhraserEnabled`: Configuration class that controls which individual phrasers are active. Has boolean properties for `BangumiEnabled`, `VndbEnabled`, `YmgalEnabled`, `SteamEnabled`, and `HikarinagiEnabled`, all defaulting to true. In default `MixedPhraserOrder`, Hikarinagi sits immediately before Bangumi wherever Bangumi appears, except `RatingOrder` (Hikarinagi provides no rating and the rating merge does no emptiness check) and `StaffOrder` (HikarinagiPhraser is not an `IGalStaffParser`).
        *   `MixedPhraserOrder`: Configuration class that defines the priority order for different game properties when merging data from multiple sources. Uses reflection to determine property orders and supports both Chinese and non-Chinese cultural preferences.
        *   `MixedPhraserData`: Container for `Order`, `Enabled`, and `TimeoutSeconds` (int seconds, default 30, `0` = no limit). Persisted via `KeyValues.MixedPhraserTimeout` and hot-reloaded in `GalgameCollectionService.OnSettingChanged`.
*   **`Contracts/`**: Defines interfaces and data contracts. Interfaces are crucial for decoupling components and enabling testability. Data contracts might define the structure of data exchanged with services or stored locally.
    *   `Contracts/Services/IScanResultService.cs`: Interface for `ScanResultService`.
    *   `Contracts/Services/`: Interfaces for service classes.
    *   `Contracts/ViewModels/`: Interfaces for ViewModel classes.
*   **`Assets/`**: Stores static resources used by the application:
    *   `Assets/Images/` or `Assets/Pictures/`: Application icons, default cover art, UI elements.
    *   `Assets/Fonts/`: Custom fonts.
    *   `Assets/Data/`: Potentially default data files or templates.
*   **`Strings/` (e.g., `Strings/en-US/Resources.resw`)**: Contains localized string resources for different languages, enabling internationalization.
*   **`Activation/`**: Includes classes responsible for handling different ways the application can be activated (e.g., normal launch, protocol activation, file association).
    *   `IActivationHandler.cs`: Interface for activation handlers.
    *   Specific handlers like `DefaultActivationHandler.cs`, `BgmOAuthActivationHandler.cs`.
*   **`Models/BgTasks/PvnSyncTask.cs`**: Responsible for the background synchronization logic with `GalgameManager.Server`. The `UploadGame` method within this class constructs the `GalgameUpdateDto` (from the generated `PotatoVN.Client.Model` namespace) to send updates to the server. When `PvnUploadProperties.PlayTime` is flagged, it includes `PlayCount`, `TotalPlayTime`, and the `PlayedTime` dictionary (converted to a list of `PlayLogDto`). When `PvnUploadProperties.HeaderImageLoc` is flagged, it handles header image synchronization by uploading manually selected images to OSS or using external URLs for automatically fetched images.
*   **`Models/BgTasks/PvnSyncTasks/`**: Contains specialized background tasks for PotatoVN synchronization:
    *   **`PvnSyncTask_PullGame.cs`**: A parallelized background task that inherits from `QueueTaskBase<GalgameDto>` to handle game data pulling from the server. It processes multiple games concurrently (up to 5 simultaneously) and handles game creation, updates, character synchronization, and playtime merging.
    *   **`PvnSyncTask_PullStaff.cs`**: A parallelized background task that inherits from `QueueTaskBase<StaffDto>` to handle staff data pulling from the server. It processes multiple staff records concurrently (up to 5 simultaneously) and handles staff creation, updates, deletion, image downloading, and game relationship management. This task was extracted from the main `PvnSyncTask` to enable parallel processing of staff synchronization.
    *   Background tasks that need to survive tray-mode restarts must register a short CLI token in `BgTaskService` and provide any required Json.NET converters there so `ResolvedBgTasksAsync()` can restore their queued payloads.
    *   Native key mapping installs its low-level keyboard and mouse hooks on a dedicated message-loop thread. It limits input handling to the tracked process or executables under the game's local installation roots, follows launcher replacement processes, ignores injected events, and keeps target keys/buttons held until the matching source release event.
*   **`Views/Dialog/PvnBatchUploadDialog.xaml`**: A dialog for selecting which game properties to upload in batch operations. Allows users to choose from available `PvnUploadProperties` flags before initiating bulk uploads to the server.
*   **`Behaviors/`**: Contains custom UI behaviors that can be attached to XAML elements to add specific functionalities or modify their behavior without extensive code-behind.
    *   `ScanResultRowStyleSelector.cs`: A `StyleSelector` used in `ScanResultPage.xaml` to apply different row background colors in the `ListView` based on the `ScanResultType` of each `PathScanResultItem`.
*   **`Enums/`**: Defines enumeration types used throughout the client application for representing sets of named constants (e.g., game status, filter types, page identifiers).
    *   `KeyValues.cs`: Contains constant strings for settings keys. Keys like `MagpieTotalSwitch`, `MagpiePath`, `MagpieHotkeys`, `AlwaysEnableMagpie`, `ShowGameNameInControl` (for controlling game name display in controls), and the new `AlwaysMuteInBackground` (for globally overriding per-game background mute settings) are defined here. The `CustomTextFileExtensions` key has also been added.
    *   `Enums/PotatoVN/PvnUploadProperties.cs`: Defines the `PvnUploadProperties` flags enum used to control which parts of a `Galgame` object are synchronized with the server. Includes `HeaderImageLoc` for header image synchronization.
    *   **Synchronization Settings Pattern**: The application follows a consistent pattern for implementing sync toggle settings:
        *   Settings keys are defined in `KeyValues.cs` with descriptive names (e.g., `SyncStaff`, `SyncGameCharacters`, `SyncHeaderImage`)
        *   Default values (typically `true`) are added to `LocalSettingsService.cs` in the `TryGetDefaultValue` method
        *   ViewModel properties are created in `AccountViewModel.cs` with corresponding change handlers that save to settings
        *   UI toggle switches are added to `AccountPage.xaml` using `SettingToggleSwitch` controls with localized `x:Uid` attributes
        *   Localization strings are managed via `resw_tool.py` script for title and description text
        *   Sync logic in background tasks (e.g., `PvnSyncTask.cs`, `GetHeaderFromRssTask.cs`) checks these settings before performing uploads
*   **`Styles/`**: May contain XAML resource dictionaries defining common styles and templates for UI controls, ensuring a consistent look and feel. (e.g., `Resource.xaml`)
*   **`Usings.cs`**: Often used in newer C# projects for global using directives to reduce boilerplate in individual files.

## 5. Interaction with Other Components

*   **`GalgameManager.Core`**: The client heavily relies on this library for shared business logic, data models, and core services that might also be used by other parts of the PotatoVN ecosystem (like the server, if applicable for certain models/contracts).
*   **`GalgameManager.Server`**: The client interacts with this server component for features like data synchronization and backup via its RESTful API. This includes synchronizing game details, play status, play time (`PlayedTime`, `TotalPlayTime`), and play count (`PlayCount`). The client uses a generated API client library (namespace `PotatoVN.Client`) to communicate with the server.
*   **External Databases (Bangumi, VNDB):** The client fetches game information directly from these online databases.
*   **Cloud Sync Software (OneDrive, etc.):** The client manages game save paths, but the actual file synchronization to the cloud is handled by external software chosen by the user.

## 6. Potential Areas for AI Agent Interaction/Analysis

*   **Code Generation/Modification:** Understanding the MVVM structure is key for adding new views, viewmodels, or modifying existing ones.
*   **Feature Implementation:** New features would likely involve creating or updating services, viewmodels, and views.
*   **Bug Fixing:** Debugging would require navigating through the MVVM layers and understanding data flow.
*   **UI/UX Enhancements:** Changes to XAML in `Views/` and potentially `Styles/`.
*   **Localization:** Adding new languages would involve updating files in `Strings/` and ensuring Crowdin integration.
*   **API Integration:** Modifying or adding interactions with external services (Bangumi, VNDB, `GalgameManager.Server`) would typically occur in `Services/` or dedicated API helper classes.
*   **Configuration Management:** Understanding `appsettings.json` for client-side settings.

This document provides a foundational knowledge base. For specific implementation details, direct code analysis of the mentioned files and directories will be necessary.

## 7. Multiple Local Installations

- `Galgame` is the logical game. Metadata, categories, review state, and play-time totals remain game-level.
- `GalgameAndPath` is a stable source entry identified by `EntryId`. A source implements `ILocalGalgameSource` to make its entries launchable local installations, including plugin-provided sources.
- `LocalInstallationConfig` stores exe path/arguments, process name, administrator/locale/DPI options, text path, detected/cloud save paths, and last successful launch time. Magpie, background mute, and key mappings remain game-level.
- `Galgame.PreferredInstallationId` identifies the default/last successful installation. New code must pass an explicit `GalgameAndPath` for file, launch, save, move, or delete operations; `LocalPath`, `ExePath`, and similar `Galgame` properties are compatibility views of the preferred installation only.
- A logical game can belong to multiple LocalFolder and Steam sources, but at most once per source. Removing the final installation keeps the logical game as a virtual game.
- `GalgameSourceCollectionService` exclusively owns source-entry/installation add, unlink, remove-files, and move operations. `GalgameCollectionService` owns logical-game identity, metadata, and whole-game lifecycle, delegating relationship changes to the source collection service.
- PVN server sync intentionally excludes sources, paths, and installation configuration. Full local export and versioned `.PotatoVN/meta.json` backups preserve them.
- `IPotatoVnApi` directly exposes installation snapshots, explicit installation launch, and `AddGameInstallation`; the old `AddGame` API is obsolete.

## 8. Service Unit Testing Pattern

Client services can be unit-tested in the plain NUnit process (no `App`, no WinAppSDK bootstrap) thanks to three production seams plus a shared test base. Use this pattern when making another service testable.

**Production seams:**
- `AdvancedCollectionView.Filter`'s setter short-circuits on `if (_filter == value) return;`, and `Delegate`'s `==` compares Method+Target rather than reference. A predicate that captures only `this` (e.g. a lambda/method group reading ViewModel properties) therefore compares **equal** across calls, so "re-assign `Filter` whenever the condition changes" silently never refreshes; a lambda capturing a local captures a fresh display-class Target each time and does refresh, which makes the difference easy to miss when refactoring one into the other. Assign `Filter` once and call `RefreshFilter()` on every condition change. Note `AdvancedCollectionView` also does not re-filter when an item's own properties change — the code that mutated the item must call `RefreshFilter()`. Characterization test: `GalgameManager.Test/Helpers/AdvancedCollectionViewFilterTest.cs`; note ACV must be constructed with an empty source in tests (a non-empty source triggers `MoveCurrentTo` → WinRT `CurrentChangingEventArgs`, which throws headless), then filled through the observable source.
- Both `UiThreadInvokeHelper`s fall back to inline execution when no UI thread exists, instead of throwing: `GalgameManager.WinApp.Base/Helpers/UiThreadInvokeHelper.cs` when its `DispatcherQueue` was never `Init`ed, and `GalgameManager/Helpers/UiThreadInvokeHelper.cs` when reading `App.DispatcherQueue` fails with `TypeInitializationException` (the `App` static ctor calls `DispatcherQueue.GetForCurrentThread()`, which throws COMException without the WinAppSDK runtime — one touch poisons the whole `App` type, so the helper caches the failure and never touches `App` again).
- Services that need a circular dependency (e.g. `GalgameSourceCollectionService` → `IGalgameCollectionService`, whose implementation already ctor-injects `IGalgameSourceCollectionService`) take `IServiceProvider` in the constructor and lazily resolve via a cached property (`GetRequiredService`); never add direct ctor injection that would create a DI cycle.
- `SourceServiceFactory.SetResolverForTest(Func<GalgameSourceType, IGalgameSourceService?>?)` replaces the static `App.GetService` lookup; pass `null` to restore. Always reset it in test teardown.
- Replace static `FileHelper.Save/Load/Delete` calls inside a service with an injected `IFileService` (Core, already registered in DI) rooted at `AppStoragePaths.LocalDataPath` — see `BgTaskService`. `FileHelper` lazily resolves `App.GetService<IFileService>()`, which is fatal in tests; `AppStoragePaths` itself is test-safe (env-var override).
- `BgTaskService.RegisterBgTaskType(Type, token)` is public so tests can register their own `BgTaskBase` subclasses for the `SaveBgTasksString`/`ResolvedBgTasksAsync` persistence loop (built-in registrations in the ctor go through the same method).
- The `Task` returned by `IBgTaskService.AddBgTask` completes when the task finished but **never faults**: `BgTaskService.HandleBgTaskCompletionAsync` catches the task's exception, reports it through `IInfoService` and returns normally (after a 500ms delay). Awaiting it therefore tells you "done", not "succeeded" — a caller that must react only to success has to observe state the task itself wrote, or the task must do the write-back internally at its success point. A task that needs to push results back into live UI state can hold the (non-serialized) model object it was constructed from and update it inside `UiThreadInvokeHelper.InvokeAsync` — safe even for tasks started before the UI exists, since the helper falls back to inline execution.
- For "did it succeed" checks on a completed sub-task, read `bgTask.Task.IsFaulted` (e.g. `SourceMoveTask` does this after awaiting the move sub-task). This is only reliable if the sub-task's `RunInternal` is declared `async`: a non-async `RunInternal` that throws before its first `return Task.Run(...)` escapes synchronously out of `BgTaskBase.Run()` **before** the `Task` property is assigned, leaving it as the default completed task — the failure is reported via `IInfoService` but `IsFaulted` stays false. Always write `protected override async Task RunInternal()` and `await` the inner work; never let precondition checks throw synchronously. (Regression test: `ZipSourceServiceTest.PackGameTask_ZipAlreadyExists_TaskFaults`.)
- Inside long-running bg tasks, keep any "advisory" logging/telemetry wrapped in its own try/catch: a failure in a purely informational path (e.g. `App.GetService<IInfoService>().Log(...)` after the real work succeeded) would otherwise be caught by the task's outer handler and turn a success into a failure (and, for `PackGameTask`, would even delete the just-created archive).
- If a service only uses interface members of a dependency, keep the field typed as the interface — do not down-cast to the concrete class. `CategoryService` used to store `(IGalgameCollectionService as GalgameCollectionService)!`; every member it touches (`Galgames`, collection/mutation events, `GetGalgameFromUuid`) is on `IGalgameCollectionService`, so the cast was removed and tests can inject a Moq mock. Check `GalgameCollectionService`'s ctor before adding a direct dependency the other way: today it does not depend on `ICategoryService`, so no cycle exists.
- Inside `GalgameCollectionService`, always use the injected `LocalSettingsService` (a static property set in the ctor) instead of `App.GetService<ILocalSettingsService>()` — two stragglers (`GetVndbData`, `MixedPhraserOrderUpdate`) were fixed this way; the former sits on the ctor path and was fatal in tests.
- `GalgameCollectionService`'s ctor news up all phrasers (`BgmPhraser`, `VndbPhraser`, `YmgalPhraser`, `CngalPhraser`, `HikarinagiPhraser`, `SteamParser`, `MixedPhraser`) — they are plain data objects and construct safely in tests (no network at ctor time). To isolate parsing from the network, replace a slot: `service.PhraserList[(int)RssType.Bangumi] = mockPhraser` (`PhraserList` is a public settable dictionary). Note `default(DisplayName)` is `ChineseName`, so with untouched settings `ParseAsync` overwrites `Name` with `CnName`.
- `BgTaskBase` subclasses use constructor injection and are never `new`ed at call sites: creation goes through `IBgTaskService.CreateBgTask<T>(params object[] args)` (implemented with `ActivatorUtilities` over the ctor-injected `IServiceProvider`; non-service ctor args like `UploadAllPlayStatusTask`'s two bools are matched positionally). Persisted-task restoration (`BgTaskService.ResolvedBgTasksAsync`) first builds a shell — `Activator.CreateInstance` when the type has a public parameterless ctor (the eight legacy serializable types: `RecordPlayTimeTask`, `GetGalgameInSourceTask`, `UnpackGameTask`, `CallMagpieTask`, `GameMuteTask`, `KeyMappingTask`, `GameSaveDetectorTask`), otherwise `ActivatorUtilities.CreateInstance` — then `JsonConvert.PopulateObject` fills the serialized state into it; failures are logged via `DeveloperEvent` and skipped. `SourceMoveTask` (parameterful ctor with non-container types) therefore no longer restores from persistence — logged and skipped, previously Newtonsoft would call its ctor with JSON-matched/defaulted args.

**Test base (`GalgameManager.Test/ServiceTestBase.cs`):**
- Derive fixtures from `ServiceTestBase`: per-test temp dir under `TestEnvironmentSetup.Root`, a real `LiteDatabase` (with the same `BsonMapper.Global` setup as `LocalSettingsService.InitDatabase` — `EnumAsInteger` + `Version` registration), `FakeLocalSettingsService` (dictionary-backed settings with Newtonsoft round-trip; export calls captured into `Exported`; other unimplemented members throw), and mocks for `IInfoService`, `IBgTaskService`, `IGalgameCollectionService`.
- Build the `IServiceProvider` argument with `CreateServiceProvider()` (a real `ServiceCollection` with the mock `IGalgameCollectionService` registered).
- `WaitUntilAsync(Func<bool>, string)` lives on the base class (50 × 100 ms poll, then `Assert.Fail`) — use it for any fire-and-forget path (`_ = SaveGalgameAsync(...)`, event handlers that kick off background async work).
- Do not abstract away `ILocalSettingsService.Database` — tests use a real LiteDB file, which is the point: `InitAsync` upgrade/migration logic runs for real against a fresh database.
- `GetLocalized()` is safe in tests: `ResourceLoader` misses fall back to returning the key.

**Upgrade E2E (`GalgameManager.Test/Upgrade/UnpackagedAppUpgradeUiTest.cs`):**
- Requires Microsoft WinApp CLI (`winget install --id Microsoft.WinAppCLI`) and an interactive Windows desktop session.
- The test sets `POTATOVN_UPGRADE_UI_TEST=1` and launches the real unpackaged UI against a copied historical JSON fixture. This mode skips unrelated network startup and preserves fixture sources whose historical paths do not exist on the test machine.
- UI assertions use stable AutomationIds on realized game, category-group, category, and source items plus scoped `winapp ui inspect` trees. Verify the exact Home game count/names; that group/source counts do not shrink; that historical group/category IDs or names survive; that category game counts match; and that a Status group exists. Match legacy system category groups by type because their display names are localized; only custom groups without historical IDs should fall back to name matching. End with `winapp ui send-keys alt+f4 --via send-input --allow-system-keys` and assert normal exit; do not inspect the upgraded LiteDB directly.
- `CategoryService.LiteDbUpgrade` must run `UpdateGameIndexFormat` before deleting `data.categoryGroups.json`: v1.7.x categories identify games by path, and deleting the JSON first silently leaves migrated categories with zero games.
- Keep this fixture under `Category("E2E")`; it launches the real WinUI process and should not run in the ordinary service-test loop.

**Known-untestable paths (avoid in unit tests, or refactor first):**
- `BgTaskBase` subclasses are safe to *construct* in tests now (ctor injection), but their `ProcessItemAsync`/`RunInternal` still hit network, files, and `App` — never let them actually `Run`. Mocked `IBgTaskService.AddBgTask` does not run tasks, so tasks created via a mocked factory are only enqueued. Pattern for code paths that create tasks (e.g. `AddGameAsync` → `GetHeaderFromRssTask`): `BgTaskService.Setup(x => x.CreateBgTask<GetHeaderFromRssTask>(It.IsAny<object[]>())).Returns(() => new GetHeaderFromRssTask(service, pvnMock.Object, Settings))` — the real task instance takes the real service under test plus mocks (see `GalgameCollectionServiceTest.AddGameAsync_VirtualSource_AddsParsedGameToListAndDatabase`).
- Anything creating `ContentDialog`/`App.MainWindow` (e.g. `DeleteGalgameFolderAsync`), and `Windows.Storage.ApplicationData` (packaged-only APIs).

- `FileService.Save`/`SaveWithoutJson` write via a background queue; `WaitForWriteFinishAsync` only waits until items are *dequeued*, not until they hit disk — in tests, poll for the file to appear instead.
- Event-driven service logic: raise events on Moq mocks with `mock.Raise(x => x.SomeEvent += null, arg)`. Handlers kick off fire-and-forget async work, so assertions must poll until the expected state appears — see `ServiceTestBase.WaitUntilAsync`.
- Moq: once `mock.Object` has been accessed, `mock.As<TInterface>()` throws ("Mock type has already been initialized") — set up all extra interfaces (e.g. `IGalStatusSync` on an `IGalInfoPhraser` mock) *before* first use of `.Object`.
- `GalgameCollectionService.GetNameFromPath` expects the game *folder* path, not the exe path inside it (the `path + '\'` trick makes `GetFileName` return the folder's last segment).
- `IGalgameCollectionService.GalgameMutated` is the unified UI-thread notification for completed additions, parser updates, and source-entry changes. `GalgameMutationEventArgs` carries `GalgameChangeKind` flags, `GalgameChangeOrigin`, and `GameParseType`; public collection operations emit it at most once, after their in-memory relationships and persistence are consistent. UI commands own their busy state with their awaited operation instead of listening for a global parser-completed event. Staff refreshes for additions carrying `Metadata` or parser updates whose `ParsedTypes` contains `GameInfo`, and PVN-origin additions must not be echoed back as uploads.
- `GalgameCollectionService` tombstones removed `Galgame` instances and serializes collection/database writes. Background parser or image tasks must confirm they still own the current in-memory instance before persisting, attaching source entries, or notifying; otherwise a task that completes after deletion can recreate the LiteDB row or overwrite a re-added game with the same UUID.
- `Galgame`'s `LockableProperty` fields (`Developer`, `Engine`): editing `.Value` always fires `GalPropertyChanged` — the constructor binds the initial instance, and `OnDeveloperChanging`/`OnEngineChanging` rebind via named methods (`HandleDeveloperValueChanged`/`HandleEngineValueChanged`) so `-=` unbinding hits by delegate equality (this was a real bug found by these tests: the initial instance was never bound, so `galgame.Developer.Value = ...` in `GalgameCollectionService` silently dropped the notification). Replacing the whole property still does NOT fire `GalPropertyChanged` by design — bulk-update paths are covered explicitly by `GalgameMutated` with the appropriate change flags.
- `ProducerDataHelper.Producers` really loads `Assets/Data/producers.json` next to `GalgameManager.dll` in the test output directory, and `CategoryService` normalizes developer names through it (e.g. "Palette" becomes its Japanese name). Use fictional developer names that cannot match the data file in tests.
- Reference examples: `GalgameManager.Test/Services/GalgameSourceCollectionServiceTest.cs`, `GalgameManager.Test/Services/BgTaskServiceTest.cs`, `GalgameManager.Test/Services/CategoryServiceTest.cs`, `GalgameManager.Test/Services/GalgameCollectionServiceTest.cs` (the last one groups tests by feature area with `#region`).

## 9. Client Packaging and Store Publication

- `.github/workflows/build-signed-package.yml` owns both sideload packaging and Microsoft Store publication. Sideload packages replace the Store identity and icon before SignPath signing; StoreUpload packages must keep the checked-in Store identity and must not use those sideload transformations.
- The `publish_store` job builds one `.msixupload` bundle for x86, x64, and ARM64, then uses `msstore publish` to commit it and start Microsoft Store certification. Stable submissions come from `released`; flight submissions come from `flight-released` and require `STORE_FLIGHT_ID`.
- A manual workflow dispatch with `store_only=true` skips the `build` job and its dependent R2 and GitHub Release jobs, allowing the independent Store publication job to be retried without duplicating release artifacts.
## 10. Character Persistence and Image Ownership

- `GalgameManager.WinApp.Base/Models/GalgameCharacter.cs` defines per-game character data. `Galgame.Characters` is embedded in the game's LiteDB document (`pvn_data.db`, collection `galgame`, keyed by `Galgame.Uuid`); there is no client character collection or name-based identity mapping. Character source IDs live in `Ids`; the model has no independent local UUID.
- `GalgameCharacterViewModel` edits the supplied object directly and saves the first game whose `Characters.Contains(character)` matches by object reference. The server stores separate `Character` rows with `GalgameId`; repository name matching is scoped to characters loaded for that game.
- Character images share the application `Images` directory. Scraping, manual character-image selection, PVN upload, and PVN download use `DownloadHelper.GetCharacterImageFileName`: `<game.Uuid:N>_<character.Name>_Large` / `_Preview`, with invalid filename characters removed and the image extension appended by the caller. Do not use only the character name or the last segment of a remote URL: different games can have characters with the same name.
- Character scraping accepts an optional owning game UUID. Background tasks pass it explicitly; legacy plugin calls resolve ownership by object reference, falling back to a temporary image namespace for characters not yet attached to a game. No character UUID is stored. Existing image paths remain readable; refreshing or replacing an image writes to the game-scoped filename, without deleting potentially shared legacy files.
