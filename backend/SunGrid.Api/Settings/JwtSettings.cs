// File name: JwtSettings.cs
// Project name: SunGrid
// Purpose of the file: Configuration class for JWT token generation and validation.
// Author placeholder: SunGrid Development Team

namespace SunGrid.Api.Settings
{
    /// <summary>
    /// Holds JWT authentication settings mapped from configuration.
    /// </summary>
    public class JwtSettings
    {
        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = "SunGridApi";
        public string Audience { get; set; } = "SunGridClients";
        public int ExpiryMinutes { get; set; } = 120;
    }
}
