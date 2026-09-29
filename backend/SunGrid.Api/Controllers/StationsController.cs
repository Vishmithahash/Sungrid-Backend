// File name: StationsController.cs
// Project name: SunGrid
// Purpose of the file: API endpoints for solar microgrid station management, schedule updates, and Haversine GPS nearby search.
// Author placeholder: SunGrid Development Team

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SunGrid.Api.DTOs;
using SunGrid.Api.Services;

namespace SunGrid.Api.Controllers
{
    /// <summary>
    /// Controller serving solar microgrid station management and location-based search endpoints.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class StationsController : ControllerBase
    {
        private readonly IStationService _stationService;

        /// <summary>
        /// Initializes StationsController with injected StationService dependency.
        /// </summary>
        public StationsController(IStationService stationService)
        {
            _stationService = stationService;
        }

        /// <summary>
        /// Creates a new solar microgrid station with Active status (Backoffice only).
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Backoffice")]
        [ProducesResponseType(typeof(StationResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateStation([FromBody] CreateStationRequest request)
        {
            var userId = GetAuthenticatedUserId();
            var station = await _stationService.CreateStationAsync(request, userId);
            return CreatedAtAction(nameof(GetStationById), new { id = station.Id }, station);
        }

        /// <summary>
        /// Retrieves a paginated list of solar stations (Backoffice and GridOperator only).
        /// Grid Operators receive only Active stations.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Backoffice,GridOperator")]
        [ProducesResponseType(typeof(StationListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetStations(
            [FromQuery] string? search,
            [FromQuery] string? status,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var userRole = GetAuthenticatedUserRole();
            var response = await _stationService.GetStationsAsync(search, status, pageNumber, pageSize, userRole);
            return Ok(response);
        }

        /// <summary>
        /// Retrieves all Active stations for web and mobile clients.
        /// </summary>
        [HttpGet("active")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<StationResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetActiveStations()
        {
            var stations = await _stationService.GetActiveStationsAsync();
            return Ok(stations);
        }

        /// <summary>
        /// Searches Active solar stations within a given radius using Haversine GPS formula.
        /// </summary>
        [HttpGet("nearby")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<NearbyStationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetNearbyStations(
            [FromQuery] double latitude,
            [FromQuery] double longitude,
            [FromQuery] double radiusKm = 10.0)
        {
            var nearbyStations = await _stationService.GetNearbyStationsAsync(latitude, longitude, radiusKm);
            return Ok(nearbyStations);
        }

        /// <summary>
        /// Retrieves a single solar station by ID.
        /// Grid Operators and Prosumers can only access Active stations.
        /// </summary>
        [HttpGet("{id}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(StationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetStationById([FromRoute] string id)
        {
            var userRole = GetAuthenticatedUserRole();
            var station = await _stationService.GetStationByIdAsync(id, userRole);
            return Ok(station);
        }

        /// <summary>
        /// Updates basic information for an existing solar station (Backoffice only).
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Backoffice")]
        [ProducesResponseType(typeof(StationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStation([FromRoute] string id, [FromBody] UpdateStationRequest request)
        {
            var userId = GetAuthenticatedUserId();
            var updatedStation = await _stationService.UpdateStationAsync(id, request, userId);
            return Ok(updatedStation);
        }

        /// <summary>
        /// Replaces the weekly operating schedule for a solar station (Backoffice only).
        /// </summary>
        [HttpPut("{id}/schedule")]
        [Authorize(Roles = "Backoffice")]
        [ProducesResponseType(typeof(StationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStationSchedule([FromRoute] string id, [FromBody] UpdateStationScheduleRequest request)
        {
            var userId = GetAuthenticatedUserId();
            var updatedStation = await _stationService.UpdateStationScheduleAsync(id, request, userId);
            return Ok(updatedStation);
        }

        /// <summary>
        /// Soft-deactivates a station (sets status to Inactive) if no active reservations exist (Backoffice only).
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Backoffice")]
        [ProducesResponseType(typeof(StationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeactivateStation([FromRoute] string id)
        {
            var userId = GetAuthenticatedUserId();
            var deactivatedStation = await _stationService.DeactivateStationAsync(id, userId);
            return Ok(deactivatedStation);
        }

        /// <summary>
        /// Reactivates an Inactive station (Backoffice only).
        /// </summary>
        [HttpPatch("{id}/reactivate")]
        [Authorize(Roles = "Backoffice")]
        [ProducesResponseType(typeof(StationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReactivateStation([FromRoute] string id)
        {
            var userId = GetAuthenticatedUserId();
            var reactivatedStation = await _stationService.ReactivateStationAsync(id, userId);
            return Ok(reactivatedStation);
        }

        /// <summary>
        /// Extracts authenticated User ID from NameIdentifier claim.
        /// </summary>
        private string GetAuthenticatedUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("Authenticated user claim missing from request token.");
            }
            return userId;
        }

        /// <summary>
        /// Extracts authenticated User Role from Role claim.
        /// </summary>
        private string GetAuthenticatedUserRole()
        {
            return User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        }
    }
}
