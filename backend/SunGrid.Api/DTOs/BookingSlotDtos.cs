// File name: BookingSlotDtos.cs
// Project name: SunGrid
// Purpose of the file: Data Transfer Objects used for energy booking slot creation, updates, and responses.
// Author placeholder: SunGrid Development Team

using System.ComponentModel.DataAnnotations;

namespace SunGrid.Api.DTOs
{
    /// <summary>
    /// Request payload for creating a new energy booking slot at a station.
    /// </summary>
    public class CreateBookingSlotRequest
    {
        [Required(ErrorMessage = "StartTimeUtc is required.")]
        public DateTime StartTimeUtc { get; set; }

        [Required(ErrorMessage = "EndTimeUtc is required.")]
        public DateTime EndTimeUtc { get; set; }

        [Range(1, 1000, ErrorMessage = "TotalCapacity must be greater than zero.")]
        public int TotalCapacity { get; set; }
    }

    /// <summary>
    /// Request payload for updating an existing energy booking slot's timing and total capacity.
    /// </summary>
    public class UpdateBookingSlotRequest
    {
        [Required(ErrorMessage = "StartTimeUtc is required.")]
        public DateTime StartTimeUtc { get; set; }

        [Required(ErrorMessage = "EndTimeUtc is required.")]
        public DateTime EndTimeUtc { get; set; }

        [Range(1, 1000, ErrorMessage = "TotalCapacity must be greater than zero.")]
        public int TotalCapacity { get; set; }
    }

    /// <summary>
    /// Request payload for adjusting the available capacity of an energy booking slot.
    /// </summary>
    public class UpdateSlotAvailabilityRequest
    {
        [Range(0, 1000, ErrorMessage = "AvailableCapacity cannot be negative.")]
        public int AvailableCapacity { get; set; }
    }

    /// <summary>
    /// Response payload containing energy booking slot details.
    /// </summary>
    public class BookingSlotResponse
    {
        public string Id { get; set; } = string.Empty;
        public string StationId { get; set; } = string.Empty;
        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }
        public int TotalCapacity { get; set; }
        public int AvailableCapacity { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CreatedByUserId { get; set; } = string.Empty;
        public string UpdatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? ClosedAtUtc { get; set; }
    }
}
