// File name: IBookingSlotService.cs
// Project name: SunGrid
// Purpose of the file: Interface contract for energy booking slot management and availability operations.
// Author placeholder: SunGrid Development Team

using SunGrid.Api.DTOs;

namespace SunGrid.Api.Services
{
    /// <summary>
    /// Contract for energy booking slot creation, updates, and availability filtering.
    /// </summary>
    public interface IBookingSlotService
    {
        /// <summary>
        /// Creates a new energy booking slot for a specified active station.
        /// </summary>
        Task<BookingSlotResponse> CreateSlotAsync(string stationId, CreateBookingSlotRequest request, string userId);

        /// <summary>
        /// Retrieves energy booking slots for a station subject to role visibility and date filters.
        /// </summary>
        Task<List<BookingSlotResponse>> GetSlotsForStationAsync(string stationId, DateTime? fromUtc, DateTime? toUtc, string? status, bool includePast, string userRole);

        /// <summary>
        /// Retrieves single booking slot by ID subject to role visibility.
        /// </summary>
        Task<BookingSlotResponse> GetSlotByIdAsync(string id, string userRole);

        /// <summary>
        /// Updates timing and total capacity for a future booking slot.
        /// </summary>
        Task<BookingSlotResponse> UpdateSlotAsync(string id, UpdateBookingSlotRequest request, string userId);

        /// <summary>
        /// Updates available capacity and adjusts slot status (Available/Full).
        /// </summary>
        Task<BookingSlotResponse> UpdateSlotAvailabilityAsync(string id, UpdateSlotAvailabilityRequest request, string userId);

        /// <summary>
        /// Performs soft closure of a booking slot if no active reservations exist.
        /// </summary>
        Task<BookingSlotResponse> CloseSlotAsync(string id, string userId);

        /// <summary>
        /// Reopens a future Closed booking slot.
        /// </summary>
        Task<BookingSlotResponse> ReopenSlotAsync(string id, string userId);

        /// <summary>
        /// Helper check to determine if a slot has Pending or Approved reservations.
        /// </summary>
        Task<bool> HasActiveReservationsForSlotAsync(string slotId);

        /// <summary>
        /// Atomically decrements slot available capacity by 1 if capacity is available. Sets status to Full when 0.
        /// </summary>
        Task<bool> TryReserveOneCapacityUnitAsync(string slotId);

        /// <summary>
        /// Atomically increments slot available capacity by 1 if below TotalCapacity. Sets non-closed slot status to Available.
        /// </summary>
        Task<bool> ReleaseOneCapacityUnitAsync(string slotId);
    }
}
