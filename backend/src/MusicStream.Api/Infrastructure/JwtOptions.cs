namespace MusicStream.Api.Infrastructure;

/// <summary>Configuración de emisión de tokens JWT.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "MusicStream";
    public string Audience { get; set; } = "MusicStream.Clients";

    /// <summary>Clave HMAC (mínimo 32 caracteres). Se inyecta vía variable de entorno Jwt__Key.</summary>
    public string Key { get; set; } = string.Empty;

    public int ExpirationMinutes { get; set; } = 120;
}
