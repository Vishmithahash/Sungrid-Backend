// File name: SolarStation.cs
// Project name: SunGrid
// Purpose of the file: MongoDB document model representing a solar microgrid station in the SolarStationInfo collection.
// Author placeholder: SunGrid Development Team

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SunGrid.Api.Enums;

namespace SunGrid.Api.Models
{
    /// <summary>
    /// Operating schedule entry for a single day of the week.
    /// </summary>
    public class DayOperatingSchedule
    {
        [BsonRepresentation(BsonType.String)]
        public DayOfWeek DayOfWeek { get; set; }

        public string OpeningTime { get; set; } = "08:00";

        public string ClosingTime { get; set; } = "18:00";

        public bool IsClosed { get; set; } = false;
    }

    /// <summary>
    /// Solar microgrid station entity stored in MongoDB.
    /// </summary>
    public class SolarStation
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string StationCode { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public double CapacityKwh { get; set; }

        public int TotalBatteryStorageSlots { get; set; }

        public List<DayOperatingSchedule> OperatingSchedule { get; set; } = new();

        [BsonRepresentation(BsonType.String)]
        public StationStatus Status { get; set; } = StationStatus.Active;

        public string CreatedByUserId { get; set; } = string.Empty;

        public string UpdatedByUserId { get; set; } = string.Empty;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? DeactivatedAtUtc { get; set; }
    }
}
