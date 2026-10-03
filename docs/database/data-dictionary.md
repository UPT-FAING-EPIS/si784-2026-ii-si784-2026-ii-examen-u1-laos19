# Diccionario de datos

> Documento generado automáticamente por `MusicStream.DocGen` (workflow `generate-documentation.yml`). No editar manualmente.

Motor de base de datos: **PostgreSQL 16** (producción) / SQLite (desarrollo y pruebas).

| Tabla | Entidad | Columnas |
|---|---|---|
| [`albums`](#albums) | Album | 5 |
| [`artists`](#artists) | Artist | 5 |
| [`payments`](#payments) | Payment | 4 |
| [`plans`](#plans) | Plan | 8 |
| [`playback_history`](#playback_history) | PlaybackHistory | 4 |
| [`playlist_songs`](#playlist_songs) | PlaylistSong | 4 |
| [`playlists`](#playlists) | Playlist | 6 |
| [`reports`](#reports) | Report | 7 |
| [`songs`](#songs) | Song | 8 |
| [`subscriptions`](#subscriptions) | Subscription | 8 |
| [`users`](#users) | User | 7 |

## albums

Entidad: `MusicStream.Api.Domain.Album`

| Columna | Tipo | Nulo | PK | FK | Único | Longitud / Precisión | Descripción |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | No | ✔ |  |  | — | Identificador único |
| `ArtistId` | `uuid` | No |  | → `artists.` |  | — | Referencia a Artist |
| `CoverUrl` | `character varying(500)` | Sí |  |  |  | 500 | cover url |
| `ReleaseYear` | `integer` | No |  |  |  | — | release year |
| `Title` | `character varying(150)` | No |  |  |  | 150 | title |

**Índices**

- `IX_albums_ArtistId` (ArtistId)

**Relaciones**

- `ArtistId` → `artists` (ON DELETE CASCADE)

## artists

Entidad: `MusicStream.Api.Domain.Artist`

| Columna | Tipo | Nulo | PK | FK | Único | Longitud / Precisión | Descripción |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | No | ✔ |  |  | — | Identificador único |
| `Bio` | `character varying(1000)` | Sí |  |  |  | 1000 | bio |
| `Genre` | `character varying(60)` | No |  |  |  | 60 | genre |
| `ImageUrl` | `character varying(500)` | Sí |  |  |  | 500 | image url |
| `Name` | `character varying(120)` | No |  |  |  | 120 | name |

**Índices**

- `IX_artists_Name` (Name)

## payments

Entidad: `MusicStream.Api.Domain.Payment`

| Columna | Tipo | Nulo | PK | FK | Único | Longitud / Precisión | Descripción |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | No | ✔ |  |  | — | Identificador único |
| `Amount` | `numeric(10,2)` | No |  |  |  | 10,2 | amount |
| `PaidAt` | `timestamp with time zone` | No |  |  |  | — | paid at |
| `SubscriptionId` | `uuid` | No |  | → `subscriptions.` |  | — | Referencia a Subscription |

**Índices**

- `IX_payments_SubscriptionId` (SubscriptionId)

**Relaciones**

- `SubscriptionId` → `subscriptions` (ON DELETE CASCADE)

## plans

Entidad: `MusicStream.Api.Domain.Plan`

| Columna | Tipo | Nulo | PK | FK | Único | Longitud / Precisión | Descripción |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | No | ✔ |  |  | — | Identificador único |
| `AllowsPremiumContent` | `boolean` | No |  |  |  | — | allows premium content |
| `Description` | `character varying(500)` | No |  |  |  | 500 | description |
| `DurationDays` | `integer` | No |  |  |  | — | duration days |
| `IsActive` | `boolean` | No |  |  |  | — | is active |
| `MaxPlaylists` | `integer` | No |  |  |  | — | max playlists |
| `Name` | `character varying(80)` | No |  |  | ✔ | 80 | name |
| `Price` | `numeric(10,2)` | No |  |  |  | 10,2 | price |

**Índices**

- `IX_plans_Name` (Name) — único

## playback_history

Entidad: `MusicStream.Api.Domain.PlaybackHistory`

| Columna | Tipo | Nulo | PK | FK | Único | Longitud / Precisión | Descripción |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | No | ✔ |  |  | — | Identificador único |
| `PlayedAt` | `timestamp with time zone` | No |  |  |  | — | played at |
| `SongId` | `uuid` | No |  | → `songs.` |  | — | Referencia a Song |
| `UserId` | `uuid` | No |  | → `users.` |  | — | Referencia a User |

**Índices**

- `IX_playback_history_SongId` (SongId)
- `IX_playback_history_UserId_PlayedAt` (UserId, PlayedAt)

**Relaciones**

- `SongId` → `songs` (ON DELETE CASCADE)
- `UserId` → `users` (ON DELETE CASCADE)

## playlist_songs

Entidad: `MusicStream.Api.Domain.PlaylistSong`

| Columna | Tipo | Nulo | PK | FK | Único | Longitud / Precisión | Descripción |
|---|---|---|---|---|---|---|---|
| `PlaylistId` | `uuid` | No | ✔ | → `playlists.` |  | — | Identificador único |
| `SongId` | `uuid` | No | ✔ | → `songs.` |  | — | Identificador único |
| `AddedAt` | `timestamp with time zone` | No |  |  |  | — | added at |
| `Position` | `integer` | No |  |  |  | — | position |

**Índices**

- `IX_playlist_songs_SongId` (SongId)

**Relaciones**

- `PlaylistId` → `playlists` (ON DELETE CASCADE)
- `SongId` → `songs` (ON DELETE CASCADE)

## playlists

Entidad: `MusicStream.Api.Domain.Playlist`

| Columna | Tipo | Nulo | PK | FK | Único | Longitud / Precisión | Descripción |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | No | ✔ |  |  | — | Identificador único |
| `CreatedAt` | `timestamp with time zone` | No |  |  |  | — | created at |
| `Description` | `character varying(500)` | Sí |  |  |  | 500 | description |
| `IsPublic` | `boolean` | No |  |  |  | — | is public |
| `Name` | `character varying(100)` | No |  |  |  | 100 | name |
| `UserId` | `uuid` | No |  | → `users.` |  | — | Referencia a User |

**Índices**

- `IX_playlists_UserId` (UserId)

**Relaciones**

- `UserId` → `users` (ON DELETE CASCADE)

## reports

Entidad: `MusicStream.Api.Domain.Report`

| Columna | Tipo | Nulo | PK | FK | Único | Longitud / Precisión | Descripción |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | No | ✔ |  |  | — | Identificador único |
| `GeneratedAt` | `timestamp with time zone` | No |  |  |  | — | generated at |
| `GeneratedByUserId` | `uuid` | No |  | → `users.` |  | — | Referencia a User |
| `PeriodEnd` | `timestamp with time zone` | No |  |  |  | — | period end |
| `PeriodStart` | `timestamp with time zone` | No |  |  |  | — | period start |
| `ResultJson` | `text` | No |  |  |  | — | result json |
| `Type` | `character varying(20)` | No |  |  |  | 20 | type |

**Índices**

- `IX_reports_GeneratedByUserId` (GeneratedByUserId)

**Relaciones**

- `GeneratedByUserId` → `users` (ON DELETE CASCADE)

## songs

Entidad: `MusicStream.Api.Domain.Song`

| Columna | Tipo | Nulo | PK | FK | Único | Longitud / Precisión | Descripción |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | No | ✔ |  |  | — | Identificador único |
| `AlbumId` | `uuid` | Sí |  | → `albums.` |  | — | Referencia a Album |
| `ArtistId` | `uuid` | No |  | → `artists.` |  | — | Referencia a Artist |
| `AudioUrl` | `character varying(500)` | No |  |  |  | 500 | audio url |
| `DurationSeconds` | `integer` | No |  |  |  | — | duration seconds |
| `Genre` | `character varying(60)` | No |  |  |  | 60 | genre |
| `IsPremium` | `boolean` | No |  |  |  | — | is premium |
| `Title` | `character varying(150)` | No |  |  |  | 150 | title |

**Índices**

- `IX_songs_AlbumId` (AlbumId)
- `IX_songs_ArtistId` (ArtistId)
- `IX_songs_Title` (Title)

**Relaciones**

- `AlbumId` → `albums` (ON DELETE SETNULL)
- `ArtistId` → `artists` (ON DELETE CASCADE)

## subscriptions

Entidad: `MusicStream.Api.Domain.Subscription`

| Columna | Tipo | Nulo | PK | FK | Único | Longitud / Precisión | Descripción |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | No | ✔ |  |  | — | Identificador único |
| `AutoRenew` | `boolean` | No |  |  |  | — | auto renew |
| `CancelledAt` | `timestamp with time zone` | Sí |  |  |  | — | cancelled at |
| `EndDate` | `timestamp with time zone` | No |  |  |  | — | end date |
| `PlanId` | `uuid` | No |  | → `plans.` |  | — | Referencia a Plan |
| `StartDate` | `timestamp with time zone` | No |  |  |  | — | start date |
| `Status` | `character varying(20)` | No |  |  |  | 20 | status |
| `UserId` | `uuid` | No |  | → `users.` |  | — | Referencia a User |

**Índices**

- `IX_subscriptions_PlanId` (PlanId)
- `IX_subscriptions_UserId_Status` (UserId, Status)

**Relaciones**

- `PlanId` → `plans` (ON DELETE RESTRICT)
- `UserId` → `users` (ON DELETE CASCADE)

## users

Entidad: `MusicStream.Api.Domain.User`

| Columna | Tipo | Nulo | PK | FK | Único | Longitud / Precisión | Descripción |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | No | ✔ |  |  | — | Identificador único |
| `CreatedAt` | `timestamp with time zone` | No |  |  |  | — | created at |
| `Email` | `character varying(150)` | No |  |  | ✔ | 150 | email |
| `FullName` | `character varying(120)` | No |  |  |  | 120 | full name |
| `IsActive` | `boolean` | No |  |  |  | — | is active |
| `PasswordHash` | `character varying(256)` | No |  |  |  | 256 | password hash |
| `Role` | `character varying(20)` | No |  |  |  | 20 | role |

**Índices**

- `IX_users_Email` (Email) — único
