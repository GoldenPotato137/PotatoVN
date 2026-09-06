# GalgameManager.Server - Detailed Knowledge Base

This document provides a detailed overview of the `GalgameManager.Server` application, the server-side component of the PotatoVN project. It is intended for AI agents and developers needing a deeper understanding of the server's architecture, features, and key code areas.

## 1. Overview

*   **Component Name:** `GalgameManager.Server`
*   **Type:** Server Application (ASP.NET Core Web API)
*   **Primary Role:** To provide a backend for the `GalgameManager` client, enabling data synchronization and backup across devices.
*   **Part of Solution:** `GalgameManager.sln` (Project Path: `GalgameManager.Server/GalgameManager.Server.csproj`)
*   **Technology Stack:** ASP.NET Core, Entity Framework Core.
*   **Database:** PostgreSQL.
*   **Object Storage:** S3-compatible (e.g., Minio).

## 2. Core Features

*   **User Authentication:** Manages user accounts and authentication using JWT.
*   **Data Synchronization:**
    *   Provides RESTful API endpoints for the client to upload and download game-related data.
    *   Synchronizes game information (metadata, cover art links).
    *   Synchronizes game play status (`PlayType`).
    *   Synchronizes detailed play history (`PlayLog`, representing daily play duration) and total play time (`TotalPlayTime`).
    *   Synchronizes total play count (`PlayCount`).
    *   Synchronizes user comments and ratings for games.
    *   Synchronizes character information associated with games.
    *   Synchronizes staff (developer/creator) information.
*   **Data Storage:**
    *   Uses PostgreSQL to store structured data (user info, game metadata, play logs, etc.).
    *   Uses S3-compatible object storage for files like game cover images and character images.
*   **API for Client:** Exposes a RESTful API for the `GalgameManager` client. Client-side DTOs (e.g., `PotatoVN.Client.Model.GalgameUpdateDto`) are typically generated from this API's specification (e.g., OpenAPI/Swagger).

## 3. Architecture and Key Components

*   **`GalgameManager.Server.csproj`**: The MSBuild project file. Defines dependencies, build configurations.
*   **`Program.cs`**: Application entry point for ASP.NET Core, configures services and middleware.
*   **`appsettings.json`**: Configuration file (database connection strings, JWT keys, Minio settings are typically set via environment variables or user secrets for security).
*   **`Controllers/`**: Contains API controllers that handle incoming HTTP requests.
    *   **`GalgameController.cs`**: Handles all CRUD (Create, Read, Update, Delete) operations related to galgames.
        *   `GetGalgamesAsync`: Fetches a paginated list of games modified after a given timestamp.
        *   `GetGalgameAsync`: Fetches detailed information for a single game.
        *   `AddOrUpdateGalgameAsync`: Creates a new game or updates an existing one. This is the primary endpoint for synchronizing game data from the client. It accepts a `GalgameUpdateDto`.
        *   `AddPlayLogAsync`: Adds a specific play log entry for a game.
        *   `DeleteGalgameAsync`: Deletes a game.
    *   **`UserController.cs`**: Handles user registration, login, and profile management.
    *   **`OssController.cs`**: Likely handles direct interactions or signed URL generation for Object Storage.
    *   **`StaffController.cs`**: Handles CRUD operations for staff information.
*   **`Services/`**: Contains service classes that encapsulate business logic.
    *   **`GalgameService.cs` (`IGalgameService`)**: Implements the core logic for managing galgame data.
        *   `AddOrUpdateGalgameAsync(int userId, GalgameUpdateDto payload)`: This method is crucial for synchronization. It takes the `GalgameUpdateDto` from the controller, finds or creates a `Galgame` entity, and updates its properties based on the DTO. This includes mapping `payload.PlayCount`, `payload.TotalPlayTime`, and converting `payload.PlayTime` (list of `PlayLogDto`) to `PlayLog` entities.
    *   **`UserService.cs` (`IUserService`)**: Manages user-related operations.
    *   **`OssService.cs` (`IOssService`)**: Handles interactions with the S3-compatible object storage.
*   **`Repositories/`**: Contains repository classes that abstract data access logic (typically interacting with Entity Framework Core).
    *   `GalgameRepository.cs` (`IGalgameRepository`)
    *   `PlayLogRepository.cs` (`IPlayLogRepository`)
    *   `CharacterRepository.cs` (`ICharacterRepository`)
    *   `UserRepository.cs` (`IUserRepository`)
*   **`Data/`**:
    *   **`DataContext.cs`**: The Entity Framework Core `DbContext` class. Defines the database schema and provides access to tables (`DbSet` properties like `Galgames`, `Users`, `PlayLogs`).
*   **`Models/`**: Contains data model classes (entities) that map to database tables and DTOs (Data Transfer Objects) used for API communication.
    *   **`Galgame.cs`**: The EF Core entity representing a game in the database. Contains properties like `Name`, `Developer`, `PlayType`, `TotalPlayTime`, `PlayCount`, `HeaderImageUrl`, `HeaderImageOssPosition` and a collection of `PlayLog` entities.
    *   **`User.cs`**: Entity for user information.
    *   **`PlayLog.cs`**: Entity representing a single play session record (date and duration in minutes). Linked to a `Galgame`.
    *   **`Character.cs`**: Entity for game character information.
    *   **`Staff.cs`**: Entity for staff information.
    *   **`Dtos/`**: Contains Data Transfer Objects.
        *   **`ServerInfoDto.cs`**: DTO for server information. Includes properties like `BangumiOAuth2Enable`, `DefaultLoginEnable`, `BangumiLoginEnable`, `GalgameStaffAvailable`, `StaffEnable`, and `ServerVersion`. The `ServerVersion` is read from the assembly's `InformationalVersion`.
        *   **`GalgameDto.cs`**:
            *   `GalgameDto`: Used for sending game data *to* the client. It contains `HeaderImageUrl` which is determined by `Galgame.HeaderImageUrl` and `Galgame.HeaderImageOssPosition`.
            *   `GalgameUpdateDto`: Used for receiving game update data *from* the client. This DTO includes properties like `Id`, `Name`, `PlayType`, `TotalPlayTime`, `PlayCount`, `HeaderImageUrl`, `HeaderImageOssPosition` and `PlayTime` (as `List<PlayLogDto>`). It's important that this DTO matches the structure expected by the client's generated API code.
        *   `PlayLogDto.cs`: DTO for play log entries.
        *   `CharacterDto.cs`, `CharacterUpdateDto.cs`: DTOs for character information.
*   **`Migrations/`**: Contains Entity Framework Core database migration files, tracking changes to the database schema. Adding `PlayCount` to `Galgame.cs` required a new migration.
*   **`Helpers/`**: Utility classes, extension methods.
*   **`Enums/`**: Server-side enumeration types.

## 4. Data Synchronization Flow (Focus on Play Count/Time)

1.  **Client Modification:** The user plays a game, or game data (like `PlayCount`) is modified on the client.
2.  **Client Flagging:** The client application marks the `Galgame` object with `PvnUpdate = true` and sets the `PvnUploadProperties.PlayTime` flag (which now covers `PlayCount`, `TotalPlayTime`, and `PlayedTime`).
3.  **Client Sync Task (`PvnSyncTask.cs`):**
    *   Identifies games marked for update.
    *   Constructs a `PotatoVN.Client.Model.GalgameUpdateDto` object.
    *   If `PvnUploadProperties.PlayTime` is set, it populates `PlayCount`, `TotalPlayTime`, and converts the client's `PlayedTime` dictionary into a list of `PlayLogDto` for the DTO's `PlayTime` property.
    *   Sends this DTO to the server via a `PATCH` request to the `/galgame` endpoint (handled by `GalgameController.AddOrUpdateGalgameAsync`).
4.  **Server Controller (`GalgameController.cs`):**
    *   Receives the `GalgameUpdateDto`.
    *   Calls `GalgameService.AddOrUpdateGalgameAsync` with the received DTO.
5.  **Server Service (`GalgameService.cs`):**
    *   Retrieves or creates the `Galgame` entity.
    *   Updates the entity's properties based on the DTO, including:
        *   `galgame.PlayCount = payload.PlayCount ?? galgame.PlayCount;`
        *   `galgame.TotalPlayTime = payload.TotalPlayTime ?? galgame.TotalPlayTime;`
        *   Replaces existing `PlayLog` entries for the game with new ones created from `payload.PlayTime`.
    *   Saves changes to the database using `IGalgameRepository` and `IPlayLogRepository`.
6.  **Database:** The `Galgames` table (with the `PlayCount` column) and `PlayLogs` table are updated.

## 5. Server Versioning

The server version is determined at build time and embedded into the assembly's `InformationalVersion` attribute. At runtime, both the `/info` endpoint (via `ServerController.cs`) and the Swagger/OpenAPI documentation (in `Program.cs`) read this `InformationalVersion`.

The versioning is configured in `GalgameManager.Server.csproj`:
```xml
<PropertyGroup>
  <_BaseVersion>1.4</_BaseVersion>
  <_BuildNumberSuffix Condition=" '$(CI_BUILD_SUFFIX)' != '' ">.$(CI_BUILD_SUFFIX)</_BuildNumberSuffix>
  <InformationalVersion>$(_BaseVersion)$(_BuildNumberSuffix)</InformationalVersion>
</PropertyGroup>
```
- `_BaseVersion` (currently "1.4") defines the major and minor parts of the version.
- `CI_BUILD_SUFFIX` is an MSBuild property that should be set by the CI/CD environment (e.g., GitHub Actions). It's expected to be the GitHub run number.
  - Example for GitHub Actions: `dotnet build /p:CI_BUILD_SUFFIX=$GITHUB_RUN_NUMBER`
- The resulting `InformationalVersion` will be in the format `MAJOR.MINOR` (e.g., "1.4") if `CI_BUILD_SUFFIX` is not provided (local build), or `MAJOR.MINOR.BUILD` (e.g., "1.4.123") if `CI_BUILD_SUFFIX` is provided (CI build).

This `InformationalVersion` is then retrieved in C# using:
`Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion`

## 6. Key Considerations for AI Agents

*   **API Client Generation:** Changes to server-side DTOs (like adding `PlayCount` to `GalgameUpdateDto`) require the client-side API library (`PotatoVN.Client`) to be regenerated so that client code can correctly serialize and deserialize data.
*   **Database Migrations:** Changes to server-side entities (like adding `PlayCount` to `Galgame.cs`) require new EF Core migrations to be created and applied to the database.
*   **DTO vs. Entity:** Understand the distinction between DTOs (for API communication, e.g., `GalgameUpdateDto`) and Entities (for database representation, e.g., `Galgame.cs`). AutoMapper is often used for mapping between them, but in this service, it's done manually for updates.

## 7. Hikarinagi OAuth Proxy

*   `HikarinagiController` and `HikarinagiService` provide the confidential-server portion of Hikarinagi's Authorization Code flow with PKCE. The desktop client owns and verifies `state` and retains the PKCE verifier; the server stores the Hikarinagi client secret and exchanges authorization codes and refresh tokens.
*   Configure `AppSettings:Hikarinagi:OAuth2Enable`, `ClientId`, `ClientSecret`, and `RedirectUri`. Enabling either the Hikarinagi catalog proxy or OAuth requires the client credentials; enabling OAuth also requires the redirect URI, normally `potato-vn://oauth-hikarinagi`.
*   The authorization request uses scopes `status:write offline_access`, `prompt=consent`, and PKCE S256. Hikarinagi refresh tokens rotate, so every successful refresh response must be returned to the client and persisted in place of the previous token.
*   `/server/info` advertises OAuth availability through `HikarinagiOAuth2Enable`. This flag is independent from the anonymous Hikarinagi catalog proxy setting.

This document provides a foundational knowledge base for the `GalgameManager.Server`. For specific implementation details, direct code analysis of the mentioned files and directories will be necessary.
