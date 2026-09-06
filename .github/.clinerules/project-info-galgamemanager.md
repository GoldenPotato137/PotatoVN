# GalgameManager Client (PotatoVN) - Detailed Knowledge Base

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
    *   `GalgameSourcePage.xaml`: Contains the UI for individual game library configuration, including settings for auto-scan, auto-add/remove games, and per-library SaveMetaBackup toggle.
    *   `ShellPage.xaml`: The main shell of the application. Its `ItemsRepeater` for displaying event notifications now includes a `HyperlinkButton` that is visible when `CallbackButtonText` is provided in the `ShellEventViewModel`. This button is bound to the `ExecuteCallbackCommand` and uses `VisibilityHelper.Convert` for its visibility.
*   **`Views/Dialog/`**: This subdirectory commonly houses `ContentDialog` XAML files used for focused editing tasks or user prompts (e.g., `EditPlayTimeDialog.xaml` for modifying game play history). These dialogs usually have a corresponding `.xaml.cs` for their logic and are instantiated and shown from ViewModels.
        *   `AddSourceDialog.xaml`: Dialog for adding new game library sources. Uses a ComboBox to select library type (currently supports "本地库" for local folders). The SelectedIndex is bound to the SelectItem property which determines the library type in LibraryViewModel.AddLibrary method.
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
    *   **`Galgame.cs`**: A key model representing a game. It includes various properties like `Name`, `ImagePath`, as well as fields for tracking play history such as:
        *   `PlayedTime` (Dictionary<string, int>): Stores individual play sessions, mapping a date string to play duration in minutes.
        *   `PlayCount` (int): Stores the total number of times the game has been played.
        *   `TotalPlayTime` (int): Stores the sum of all play session durations in minutes.
        *   `MuteInBackground` (bool): A per-game setting to determine if the game audio should be muted when the application is not in the foreground.
        *   `PvnUpdate` (bool): A flag indicating if the game's data needs to be synced with the server.
        *   `PvnUploadProperties` (enum `PvnUploadProperties`): A flags enum specifying which particular properties of the game need to be uploaded to the server. The `PlayTime` flag is used to indicate that `PlayedTime`, `TotalPlayTime`, and `PlayCount` should be synced.
        *   `Ids` (string?[]): An array storing IDs from different data sources (Bangumi, VNDB, etc.). The array size is defined by `PhraserNumber` constant. All methods accessing this array include bounds checking to prevent `IndexOutOfRangeException` for legacy data with smaller arrays.
*   **`Services/`**: Houses service classes that encapsulate specific functionalities, such as:
    *   `PluginService.cs`: The plugin loader now registers each loaded plugin assembly with the host WinUI XAML metadata pipeline before plugin initialization, so plugin XAML can resolve nested custom controls/UserControls when the plugin output includes its generated `.pri` and compiled XAML resources.
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
        *   `VirtualSourceService.cs`: Handles virtual game libraries for organizational purposes.
        *   All source services implement `IGalgameSourceService` interface which defines standard operations like `SaveMetaAsync`, `LoadMetaAsync`, `RemoveMetaAsync` for meta information management.
*   **`Helpers/`**: Contains utility classes and extension methods that provide common, reusable functions (e.g., file I/O helpers, string manipulation, UI helpers).
    *   `VisibilityHelper.cs`: Provides converters for XAML bindings, e.g., converting a string's null/empty status to a `Visibility` value.
    *   `GalgameManager/Services/PluginService/PluginXamlHost.cs`: Bridges dynamically loaded plugin assemblies into the host app's WinUI XAML system by loading plugin PRI resources and registering plugin `IXamlMetadataProvider` implementations before plugin UI is initialized.
    *   **`Helpers/Phrase/`**: Contains the mixed phraser system for aggregating game information from multiple sources:
        *   `MixedPhraser.cs`: The main mixed phraser implementation that combines data from multiple game information sources (Bangumi, VNDB, Ymgal, Steam, Hikarinagi). It supports selective enabling/disabling of individual phrasers through the `MixedPhraserEnabled` configuration.
        *   `MixedPhraserEnabled`: Configuration class that controls which individual phrasers are active. Has boolean properties for `BangumiEnabled`, `VndbEnabled`, `YmgalEnabled`, `SteamEnabled`, and `HikarinagiEnabled`, all defaulting to true. In default `MixedPhraserOrder`, Hikarinagi sits immediately before Bangumi wherever Bangumi appears, except `RatingOrder` (Hikarinagi provides no rating and the rating merge does no emptiness check) and `StaffOrder` (HikarinagiPhraser is not an `IGalStaffParser`).
        *   `MixedPhraserOrder`: Configuration class that defines the priority order for different game properties when merging data from multiple sources. Uses reflection to determine property orders and supports both Chinese and non-Chinese cultural preferences.
        *   `MixedPhraserData`: Container class that holds both the `MixedPhraserEnabled` settings and `MixedPhraserOrder` configuration for the mixed phraser.
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

- `Galgame` represents a logical game; `GalgameAndPath` represents a stable source entry. Sources implementing `ILocalGalgameSource` make their entries launchable installations.
- Per-installation launch/path/save settings live in `LocalInstallationConfig`. Game-level metadata, play time, Magpie, mute, and key mappings remain on `Galgame`.
- Always pass an explicit source entry for launch, file, save, move, and delete operations. Legacy `Galgame.LocalPath`/`ExePath` properties expose only the preferred installation.
- Multiple LocalFolder and Steam sources may reference one game, with one path per source. Removing the last installation keeps a virtual logical game.
- `GalgameSourceCollectionService` owns all installation/source-entry relationship and file-move operations; `GalgameCollectionService` owns logical-game identity, metadata, and whole-game lifecycle.
- Installation data is local/export-only and is not part of PVN server sync. `IPotatoVnApi` directly exposes installation snapshots and explicit installation launch.
