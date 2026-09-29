// File name: IStationService.cs
// Project name: SunGrid
// Purpose of the file: Interface contract for solar microgrid station management and GPS nearby search.
// Author placeholder: SunGrid Development Team

using SunGrid.Api.DTOs;

namespace SunGrid.Api.Services
{
    /// <summary>
    /// Contract for solar station management and location-based search business logic.
    /// </summary>
    public interface IStationService
    {
        /// <summary>
        /// Creates a new solar microgrid station with Active status.
        /// </summary>
        Task<StationResponse> CreateStationAsync(CreateStationRequest request, string userId);

        /// <summary>
        /// Retrieves a paginated list of solar stations filtered by search, status, and role visibility.
        /// </summary>
        Task<StationListResponse> GetStationsAsync(string? search, string? status, int pageNumber, int pageSize, string userRole);

        /// <summary>
        /// Returns all Active solar stations for client view.
        /// </summary>
        Task<List<StationResponse>> GetActiveStationsAsync();

        /// <summary>
        /// Searches Active solar stations within a given radius from GPS coordinates using Haversine formula.
        /// </summary>
        Task<List<NearbyStationResponse>> GetNearbyStationsAsync(double latitude, double longitude, double radiusKm);

        /// <summary>
        /// Retrieves details of a single solar station by ID subject to role visibility.
        /// </summary>
        Task<StationResponse> GetStationByIdAsync(string id, string userRole);

        /// <summary>
        /// Updates profile information for an existing solar station.
        /// </summary>
        Task<StationResponse> UpdateStationAsync(string id, UpdateStationRequest request, string userId);

        /// <summary>
        /// Replaces the weekly operating schedule for a solar station.
        /// </summary>
        Task<StationResponse> UpdateStationScheduleAsync(string id, UpdateStationScheduleRequest request, string userId);

        /// <summary>
        /// Performs soft deactivation of a station if no active reservations exist.
        /// </summary>
        Task<StationResponse> DeactivateStationAsync(string id, string userId);

        /// <summary>
        /// Reactivates an Inactive solar station.
        /// </summary>
        Task<StationResponse> ReactivateStationAsync(string id, string userId);

        /// <summary>
        /// Helper check to determine if a station has Pending or Approved reservations.
        /// </summary>
        Task<bool> HasActiveReservationsForStationAsync(string stationId);
    }
}
