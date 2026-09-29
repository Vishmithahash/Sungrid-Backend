// File name: QrTransactionDtos.cs
// Project name: SunGrid
// Purpose of the file: Data Transfer Objects for Phase 4 QR generation, status checks, verification, and transfer completion.
// Author placeholder: SunGrid Development Team

using System.ComponentModel.DataAnnotations;

namespace SunGrid.Api.DTOs
{
    /// <summary>
    /// Response payload containing generated raw QR token string and expiration details returned to Prosumer.
    /// </summary>
    public class GenerateQrResponse
    {
        public string ReservationId { get; set; } = string.Empty;
        public string ReservationReference { get; set; } = string.Empty;
        public string QrPayload { get; set; } = string.Empty;
        public DateTime IssuedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }

    /// <summary>
    /// Safe metadata response indicating status of QR token for a reservation without exposing raw token or hash.
    /// </summary>
    public class QrStatusResponse
    {
        public bool HasQr { get; set; }
        public bool IsExpired { get; set; }
        public bool IsUsed { get; set; }
        public bool IsRevoked { get; set; }
        public DateTime? IssuedAtUtc { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public string ReservationStatus { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request payload sent by Grid Operator containing scanned QR payload string or reservation reference.
    /// </summary>
    public class VerifyQrRequest
    {
        public string? QrPayload { get; set; }
        public string? Qr_Payload { get => QrPayload; set => QrPayload = value; }
        public string? ReservationId { get; set; }
        public string? Reservation_Id { get => ReservationId; set => ReservationId = value; }
        public string? BookingId { get; set; }
        public string? Booking_Id { get => BookingId; set => BookingId = value; }
    }

    /// <summary>
    /// Detailed verification response payload returned to Grid Operator upon scanning a valid QR code.
    /// </summary>
    public class VerifyQrResponse
    {
        public bool IsValid { get; set; }
        public bool CanComplete { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ReservationId { get; set; } = string.Empty;
        public string ReservationReference { get; set; } = string.Empty;
        public string ProsumerId { get; set; } = string.Empty;
        public string ProsumerName { get; set; } = string.Empty;
        public string? ProsumerNic { get; set; }
        public string StationId { get; set; } = string.Empty;
        public string StationCode { get; set; } = string.Empty;
        public string StationName { get; set; } = string.Empty;
        public string BookingSlotId { get; set; } = string.Empty;
        public DateTime SlotStartTimeUtc { get; set; }
        public DateTime SlotEndTimeUtc { get; set; }
        public string TransferType { get; set; } = string.Empty;
        public double ExpectedEnergyAmountKwh { get; set; }
        public string ReservationStatus { get; set; } = string.Empty;
        public DateTime CompletionWindowStartsUtc { get; set; }
        public DateTime QrExpiresAtUtc { get; set; }
    }

    /// <summary>
    /// Request payload sent by Grid Operator to finalize an energy transfer.
    /// </summary>
    public class CompleteEnergyTransferRequest
    {
        public string? QrPayload { get; set; }
        public string? Qr_Payload { get => QrPayload; set => QrPayload = value; }

        public string? ReservationId { get; set; }
        public string? Reservation_Id { get => ReservationId; set => ReservationId = value; }

        public string? BookingId { get; set; }
        public string? Booking_Id { get => BookingId; set => BookingId = value; }

        public double? ActualEnergyAmountKwh { get; set; }
        public double? Actual_Energy_Amount_Kwh { get => ActualEnergyAmountKwh; set => ActualEnergyAmountKwh = value; }
        public double? EnergyAmountKwh { get => ActualEnergyAmountKwh; set => ActualEnergyAmountKwh = value; }

        public string? CompletionNotes { get; set; }
        public string? Notes { get => CompletionNotes; set => CompletionNotes = value; }
    }

    /// <summary>
    /// Response payload returned after attempting energy transfer completion.
    /// </summary>
    public class CompleteEnergyTransferResponse
    {
        public string ReservationId { get; set; } = string.Empty;
        public string ReservationReference { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double ActualEnergyAmountKwh { get; set; }
        public DateTime CompletedAtUtc { get; set; }
        public string CompletedByUserId { get; set; } = string.Empty;
        public bool AlreadyCompleted { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
