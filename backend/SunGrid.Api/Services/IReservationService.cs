// File name: IReservationService.cs
// Project name: SunGrid
// Purpose of the file: Interface contract for energy reservation creation, updates, status transitions, and dashboard metrics.
// Author placeholder: SunGrid Development Team

using SunGrid.Api.DTOs;

namespace SunGrid.Api.Services
{
    /// <summary>
    /// Contract for energy reservation management and business rules enforcement.
    /// </summary>
    public interface IReservationService
    {
        /// <summary>
        /// Creates a new energy reservation for the authenticated Prosumer.
        /// </summary>
        Task<ReservationResponse> CreateReservationAsync(CreateReservationRequest request, string prosumerId, string createdByUserId);

        /// <summary>
        /// Staff operation creating a reservation on behalf of a specified Active Prosumer.
        /// </summary>
        Task<ReservationResponse> CreateReservationForProsumerAsync(CreateReservationForProsumerRequest request, string createdByUserId);

        /// <summary>
        /// Retrieves paginated reservations for the authenticated Prosumer.
        /// </summary>
        Task<ReservationListResponse> GetMyReservationsAsync(string prosumerId, string? status, string? search, DateTime? fromUtc, DateTime? toUtc, int pageNumber, int pageSize);

        /// <summary>
        /// Retrieves future Pending and Approved reservations for the authenticated Prosumer.
        /// </summary>
        Task<List<ReservationResponse>> GetMyCurrentReservationsAsync(string prosumerId);

        /// <summary>
        /// Retrieves historical (Rejected, Cancelled, Completed, or past slot) reservations for the authenticated Prosumer.
        /// </summary>
        Task<List<ReservationResponse>> GetMyReservationHistoryAsync(string prosumerId);

        /// <summary>
        /// Returns live reservation dashboard counts for the authenticated Prosumer.
        /// </summary>
        Task<ProsumerReservationDashboardResponse> GetMyDashboardCountsAsync(string prosumerId);

        /// <summary>
        /// Staff query returning paginated reservations with filters across all Prosumers and stations.
        /// </summary>
        Task<ReservationListResponse> GetReservationsAsync(string? status, string? prosumerId, string? stationId, string? bookingSlotId, string? search, DateTime? fromUtc, DateTime? toUtc, int pageNumber, int pageSize);

        /// <summary>
        /// Returns all Pending reservations ordered oldest first for administrative approval.
        /// </summary>
        Task<List<ReservationResponse>> GetPendingReservationsAsync();

        /// <summary>
        /// Returns live reservation metrics for operational staff (Backoffice / GridOperator).
        /// </summary>
        Task<OperationsDashboardResponse> GetOperationsDashboardCountsAsync();

        /// <summary>
        /// Retrieves a single reservation by ID subject to ownership check for Prosumers.
        /// </summary>
        Task<ReservationResponse> GetReservationByIdAsync(string id, string userId, string userRole);

        /// <summary>
        /// Updates a Pending or Approved reservation applying 12-hour, 7-day, and slot capacity migration rules.
        /// </summary>
        Task<ReservationResponse> UpdateReservationAsync(string id, UpdateReservationRequest request, string userId, string userRole);

        /// <summary>
        /// Approves a Pending reservation (Backoffice or GridOperator).
        /// </summary>
        Task<ReservationResponse> ApproveReservationAsync(string id, string userId);

        /// <summary>
        /// Rejects a Pending reservation and releases slot capacity (Backoffice or GridOperator).
        /// </summary>
        Task<ReservationResponse> RejectReservationAsync(string id, RejectReservationRequest request, string userId);

        /// <summary>
        /// Soft-cancels a Pending or Approved reservation applying 12-hour rule and releasing slot capacity.
        /// </summary>
        Task<ReservationResponse> CancelReservationAsync(string id, CancelReservationRequest request, string userId, string userRole);
    }
}
