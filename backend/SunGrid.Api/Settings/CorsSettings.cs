// File name: CorsSettings.cs
// Project name: SunGrid
// Purpose of the file: Configuration class for allowed CORS origins.
// Author placeholder: SunGrid Development Team

namespace SunGrid.Api.Settings
{
    /// <summary>
    /// Holds CORS allowed origins settings mapped from configuration.
    /// </summary>
    public class CorsSettings
    {
        public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    }
}
