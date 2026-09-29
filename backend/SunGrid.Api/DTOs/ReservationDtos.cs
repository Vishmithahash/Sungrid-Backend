// File name: ReservationDtos.cs
// Project name: SunGrid
// Purpose of the file: Data Transfer Objects used for energy reservation requests, responses, and live dashboard metrics.
// Author placeholder: SunGrid Development Team

using System.ComponentModel.DataAnnotations;
using SunGrid.Api.Enums;

namespace SunGrid.Api.DTOs
{
    /// <summary>
    /// Request payload for Prosumer self-service reservation creation.
    /// </summary>
    public class CreateReservationRequest
    {
        [Required(ErrorMessage = "BookingSlotId is required.")]
        public string BookingSlotId { get; set; } = string.Empty;

        public string? StationId { get; set; }

        [Required(ErrorMessage = "TransferType is required.")]
        public EnergyTransferType TransferType { get; set; }

        [Range(0.01, 100000.0, ErrorMessage = "EnergyAmountKwh must be greater than zero.")]
        public double EnergyAmountKwh { get; set; }

        public string? ScheduledTime { get; set; }

        public string? SlotStartTimeUtc { get; set; }

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Request payload for staff creation of a reservation on behalf of a Prosumer.
    /// </summary>
    public class CreateReservationForProsumerRequest
    {
        [Required(ErrorMessage = "ProsumerId is required.")]
        public string ProsumerId { get; set; } = string.Empty;

        [Required(ErrorMessage = "BookingSlotId is required.")]
        public string BookingSlotId { get; set; } = string.Empty;

        public string? StationId { get; set; }

        [Required(ErrorMessage = "TransferType is required.")]
        public EnergyTransferType TransferType { get; set; }

        [Range(0.01, 100000.0, ErrorMessage = "EnergyAmountKwh must be greater than zero.")]
        public double EnergyAmountKwh { get; set; }

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Request payload for updating an existing energy reservation.
    /// </summary>
    public class UpdateReservationRequest
    {
        public string? BookingSlotId { get; set; }

        public EnergyTransferType TransferType { get; set; } = EnergyTransferType.EnergyDropOff;

        public double EnergyAmountKwh { get; set; }

        public string? ScheduledTime { get; set; }

        public string? SlotStartTimeUtc { get; set; }

        public string? StationId { get; set; }

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Request payload for rejecting a Pending reservation.
    /// </summary>
    public class RejectReservationRequest
    {
        [Required(ErrorMessage = "RejectionReason is required.")]
        [StringLength(300, MinimumLength = 3, ErrorMessage = "RejectionReason must be between 3 and 300 characters.")]
        public string RejectionReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request payload for cancelling a reservation.
    /// </summary>
    public class CancelReservationRequest
    {
        [StringLength(300, ErrorMessage = "CancellationReason cannot exceed 300 characters.")]
        public string? CancellationReason { get; set; }
    }

    /// <summary>
    /// Complete response payload containing safe reservation details and associated entity descriptions.
    /// </summary>
    public class ReservationResponse
    {
        public string Id { get; set; } = string.Empty;
        public string ReservationReference { get; set; } = string.Empty;
        public string ProsumerId { get; set; } = string.Empty;
        public string ProsumerFullName { get; set; } = string.Empty;
        public string? ProsumerNic { get; set; }
        public string StationId { get; set; } = string.Empty;
        public string StationCode { get; set; } = string.Empty;
        public string StationName { get; set; } = string.Empty;
        public string BookingSlotId { get; set; } = string.Empty;
        public DateTime SlotStartTimeUtc { get; set; }
        public DateTime SlotEndTimeUtc { get; set; }
        public DateTime ScheduledStartTime => SlotStartTimeUtc;
        public DateTime ScheduledEndTime => SlotEndTimeUtc;
        public string ScheduledTime => SlotStartTimeUtc.ToString("yyyy-MM-dd HH:mm");
        public string TransferType { get; set; } = string.Empty;
        public double EnergyAmountKwh { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string? RejectionReason { get; set; }
        public string? CancellationReason { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public string UpdatedByUserId { get; set; } = string.Empty;
        public string? ApprovedByUserId { get; set; }
        public string? RejectedByUserId { get; set; }
        public string? CancelledByUserId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? ApprovedAtUtc { get; set; }
        public DateTime? RejectedAtUtc { get; set; }
        public DateTime? CancelledAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
    }

    /// <summary>
    /// Paginated list response for reservation queries.
    /// </summary>
    public class ReservationListResponse
    {
        public List<ReservationResponse> Items { get; set; } = new();
        public long TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    }

    /// <summary>
    /// Response payload for Prosumer live dashboard counts.
    /// </summary>
    public class ProsumerReservationDashboardResponse
    {
        public long PendingReservationsCount { get; set; }
        public long ApprovedFutureReservationsCount { get; set; }
        public long CurrentBookingsCount { get; set; }
        public long CompletedReservationsCount { get; set; }
        public long CancelledReservationsCount { get; set; }
        public long ActiveBookings => CurrentBookingsCount;
        public long ActiveBookingsCount => CurrentBookingsCount;
        public long TotalActive => CurrentBookingsCount;
        public double TotalEnergyTraded { get; set; }
        public double TotalEnergyTradedKwh => TotalEnergyTraded;
        public double TotalEnergyKwhTraded => TotalEnergyTraded;
        public double TotalEnergySoldKwh { get; set; }
        public double TotalEnergyBoughtKwh { get; set; }
        public double NetEnergyKwh => Math.Round(TotalEnergySoldKwh - TotalEnergyBoughtKwh, 2);
    }

    /// <summary>
    /// Response payload for operational staff (Backoffice / GridOperator) live dashboard counts.
    /// </summary>
    public class OperationsDashboardResponse
    {
        public long PendingReservationsCount { get; set; }
        public long ApprovedFutureReservationsCount { get; set; }
        public long TodaysReservationsCount { get; set; }
        public long CancelledReservationsCount { get; set; }
        public long CompletedReservationsCount { get; set; }
    }
}
