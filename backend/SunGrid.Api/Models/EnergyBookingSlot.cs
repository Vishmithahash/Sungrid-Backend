// File name: EnergyBookingSlot.cs
// Project name: SunGrid
// Purpose of the file: MongoDB document model representing an energy booking slot in the EnergyBookingSlots collection.
// Author placeholder: SunGrid Development Team

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SunGrid.Api.Enums;

namespace SunGrid.Api.Models
{
    /// <summary>
    /// Energy booking slot entity stored in MongoDB.
    /// </summary>
    public class EnergyBookingSlot
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string StationId { get; set; } = string.Empty;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime StartTimeUtc { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime EndTimeUtc { get; set; }

        public int TotalCapacity { get; set; }

        public int AvailableCapacity { get; set; }

        [BsonRepresentation(BsonType.String)]
        public BookingSlotStatus Status { get; set; } = BookingSlotStatus.Available;

        public string CreatedByUserId { get; set; } = string.Empty;

        public string UpdatedByUserId { get; set; } = string.Empty;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? ClosedAtUtc { get; set; }
    }
}
