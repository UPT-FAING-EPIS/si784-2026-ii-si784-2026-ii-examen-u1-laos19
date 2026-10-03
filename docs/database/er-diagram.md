# Diagrama entidad-relación

> Documento generado automáticamente por `MusicStream.DocGen` (workflow `generate-documentation.yml`). No editar manualmente.

```mermaid
erDiagram
    albums {
        uuid Id PK
        uuid ArtistId FK
        varchar(500) CoverUrl
        integer ReleaseYear
        varchar(150) Title
    }
    artists {
        uuid Id PK
        varchar(1000) Bio
        varchar(60) Genre
        varchar(500) ImageUrl
        varchar(120) Name
    }
    payments {
        uuid Id PK
        numeric Amount
        timestamptz PaidAt
        uuid SubscriptionId FK
    }
    plans {
        uuid Id PK
        boolean AllowsPremiumContent
        varchar(500) Description
        integer DurationDays
        boolean IsActive
        integer MaxPlaylists
        varchar(80) Name UK
        numeric Price
    }
    playback_history {
        uuid Id PK
        timestamptz PlayedAt
        uuid SongId FK
        uuid UserId FK
    }
    playlist_songs {
        uuid PlaylistId PK,FK
        uuid SongId PK,FK
        timestamptz AddedAt
        integer Position
    }
    playlists {
        uuid Id PK
        timestamptz CreatedAt
        varchar(500) Description
        boolean IsPublic
        varchar(100) Name
        uuid UserId FK
    }
    reports {
        uuid Id PK
        timestamptz GeneratedAt
        uuid GeneratedByUserId FK
        timestamptz PeriodEnd
        timestamptz PeriodStart
        text ResultJson
        varchar(20) Type
    }
    songs {
        uuid Id PK
        uuid AlbumId FK
        uuid ArtistId FK
        varchar(500) AudioUrl
        integer DurationSeconds
        varchar(60) Genre
        boolean IsPremium
        varchar(150) Title
    }
    subscriptions {
        uuid Id PK
        boolean AutoRenew
        timestamptz CancelledAt
        timestamptz EndDate
        uuid PlanId FK
        timestamptz StartDate
        varchar(20) Status
        uuid UserId FK
    }
    users {
        uuid Id PK
        timestamptz CreatedAt
        varchar(150) Email UK
        varchar(120) FullName
        boolean IsActive
        varchar(256) PasswordHash
        varchar(20) Role
    }
    artists ||--o{ albums : "ArtistId"
    subscriptions ||--o{ payments : "SubscriptionId"
    songs ||--o{ playback_history : "SongId"
    users ||--o{ playback_history : "UserId"
    playlists ||--o{ playlist_songs : "PlaylistId"
    songs ||--o{ playlist_songs : "SongId"
    users ||--o{ playlists : "UserId"
    users ||--o{ reports : "GeneratedByUserId"
    albums |o--o{ songs : "AlbumId"
    artists ||--o{ songs : "ArtistId"
    plans ||--o{ subscriptions : "PlanId"
    users ||--o{ subscriptions : "UserId"
```
