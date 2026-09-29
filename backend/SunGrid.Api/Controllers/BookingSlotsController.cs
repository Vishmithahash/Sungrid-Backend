// File name: BookingSlotsController.cs
// Project name: SunGrid
// Purpose of the file: API endpoints for energy booking slot creation, queries, capacity adjustments, and soft closures.
// Author placeholder: SunGrid Development Team

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SunGrid.Api.DTOs;
using SunGrid.Api.Services;

namespace SunGrid.Api.Controllers
{
    /// <summary>
    /// Controller serving energy booking slot management endpoints.
    /// </summary>
    [ApiController]
    public class BookingSlotsController : ControllerBase
    {
        private readonly IBookingSlotService _slotService;

        /// <summary>
        /// Initializes BookingSlotsController with injected BookingSlotService dependency.
        /// </summary>
        public BookingSlotsController(IBookingSlotService slotService)
        {
            _slotService = slotService;
        }

        /// <summary>
        /// Creates a new energy booking slot for a specified station (Backoffice only).
        /// </summary>
        [HttpPost("api/stations/{stationId}/slots")]
        [Authorize(Roles = "Backoffice")]
        [ProducesResponseType(typeof(BookingSlotResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateSlot([FromRoute] string stationId, [FromBody] CreateBookingSlotRequest request)
        {
            var userId = GetAuthenticatedUserId();
            var slot = await _slotService.CreateSlotAsync(stationId, request, userId);
            return CreatedAtAction(nameof(GetSlotById), new { id = slot.Id }, slot);
        }

        /// <summary>
        /// Retrieves energy booking slots for a specified station (Any authenticated user).
        /// Apply role-based visibility restrictions: Prosumers see future Available slots only.
        /// </summary>
        [HttpGet("api/stations/{stationId}/slots")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<BookingSlotResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSlotsForStation(
            [FromRoute] string stationId,
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] string? status,
            [FromQuery] bool includePast = false)
        {
            var userRole = GetAuthenticatedUserRole();
            var slots = await _slotService.GetSlotsForStationAsync(stationId, fromUtc, toUtc, status, includePast, userRole);
            return Ok(slots);
        }

        /// <summary>
        /// Retrieves a single energy booking slot by ID (Any authenticated user).
        /// </summary>
        [HttpGet("api/booking-slots/{id}")]
        [Authorize]
        [ProducesResponseType(typeof(BookingSlotResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSlotById([FromRoute] string id)
        {
            var userRole = GetAuthenticatedUserRole();
            var slot = await _slotService.GetSlotByIdAsync(id, userRole);
            return Ok(slot);
        }

        /// <summary>
        /// Updates timing and total capacity for a future booking slot (Backoffice only).
        /// </summary>
        [HttpPut("api/booking-slots/{id}")]
        [Authorize(Roles = "Backoffice")]
        [ProducesResponseType(typeof(BookingSlotResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateSlot([FromRoute] string id, [FromBody] UpdateBookingSlotRequest request)
        {
            var userId = GetAuthenticatedUserId();
            var updatedSlot = await _slotService.UpdateSlotAsync(id, request, userId);
            return Ok(updatedSlot);
        }

        /// <summary>
        /// Updates available capacity and adjusts slot status automatically (Backoffice and GridOperator).
        /// </summary>
        [HttpPatch("api/booking-slots/{id}/availability")]
        [Authorize(Roles = "Backoffice,GridOperator")]
        [ProducesResponseType(typeof(BookingSlotResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateSlotAvailability([FromRoute] string id, [FromBody] UpdateSlotAvailabilityRequest request)
        {
            var userId = GetAuthenticatedUserId();
            var updatedSlot = await _slotService.UpdateSlotAvailabilityAsync(id, request, userId);
            return Ok(updatedSlot);
        }

        /// <summary>
        /// Soft-closes a booking slot (sets status to Closed) if no active reservations exist (Backoffice only).
        /// </summary>
        [HttpDelete("api/booking-slots/{id}")]
        [Authorize(Roles = "Backoffice")]
        [ProducesResponseType(typeof(BookingSlotResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CloseSlot([FromRoute] string id)
        {
            var userId = GetAuthenticatedUserId();
            var closedSlot = await _slotService.CloseSlotAsync(id, userId);
            return Ok(closedSlot);
        }

        /// <summary>
        /// Reopens a future Closed booking slot (Backoffice only).
        /// </summary>
        [HttpPatch("api/booking-slots/{id}/reopen")]
        [Authorize(Roles = "Backoffice")]
        [ProducesResponseType(typeof(BookingSlotResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReopenSlot([FromRoute] string id)
        {
            var userId = GetAuthenticatedUserId();
            var reopenedSlot = await _slotService.ReopenSlotAsync(id, userId);
            return Ok(reopenedSlot);
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
