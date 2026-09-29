// File name: SeedAdminSettings.cs
// Project name: SunGrid
// Purpose of the file: Configuration class for initial Backoffice account seeding.
// Author placeholder: SunGrid Development Team

namespace SunGrid.Api.Settings
{
    /// <summary>
    /// Holds initial admin account seed values read from configuration.
    /// </summary>
    public class SeedAdminSettings
    {
        public bool Enabled { get; set; } = true;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }
}
