// File name: HealthDtos.cs
// Project name: SunGrid
// Purpose of the file: Data Transfer Objects for system and database health check endpoints.
// Author placeholder: SunGrid Development Team

namespace SunGrid.Api.DTOs
{
    /// <summary>
    /// Response model returned by public API health check endpoint.
    /// </summary>
    public class HealthResponse
    {
        public string Status { get; set; } = "Healthy";
        public string Message { get; set; } = "API is running";
        public string Application { get; set; } = "SunGrid";
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response model returned by public MongoDB database health check endpoint.
    /// </summary>
    public class DatabaseHealthResponse
    {
        public string Application { get; set; } = "SunGrid";
        public string ApiStatus { get; set; } = "Healthy";
        public string DatabaseStatus { get; set; } = "Healthy";
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
