// File name: EnergyReservation.cs
// Project name: SunGrid
// Purpose of the file: MongoDB document model representing an energy reservation in the EnergyReservations collection.
// Author placeholder: SunGrid Development Team

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SunGrid.Api.Enums;

namespace SunGrid.Api.Models
{
    /// <summary>
    /// Energy reservation entity stored in MongoDB.
    /// </summary>
    public class EnergyReservation
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string ReservationReference { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string ProsumerId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string StationId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string BookingSlotId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.String)]
        public EnergyTransferType TransferType { get; set; }

        public double EnergyAmountKwh { get; set; }

        [BsonRepresentation(BsonType.String)]
        public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

        public string? Notes { get; set; }

        public string CreatedByUserId { get; set; } = string.Empty;

        public string UpdatedByUserId { get; set; } = string.Empty;

        public string? ApprovedByUserId { get; set; }

        public string? RejectedByUserId { get; set; }

        public string? CancelledByUserId { get; set; }

        public string? CompletedByUserId { get; set; }

        public string? RejectionReason { get; set; }

        public string? CancellationReason { get; set; }

        public string? CompletionNotes { get; set; }

        public double? ActualEnergyAmountKwh { get; set; }

        [BsonIgnoreIfNull]
        public string? QrTokenHash { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? QrIssuedAtUtc { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? QrExpiresAtUtc { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? QrUsedAtUtc { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? QrRevokedAtUtc { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? ApprovedAtUtc { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? RejectedAtUtc { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? CancelledAtUtc { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? CompletedAtUtc { get; set; }
    }
}
