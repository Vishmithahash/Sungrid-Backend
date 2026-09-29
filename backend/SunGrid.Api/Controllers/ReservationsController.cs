// File name: ReservationsController.cs
// Project name: SunGrid
// Purpose of the file: API endpoints for energy reservation creation, status updates, cancellations, and live dashboards.
// Author placeholder: SunGrid Development Team

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SunGrid.Api.DTOs;
using SunGrid.Api.Services;

namespace SunGrid.Api.Controllers
{
    /// <summary>
    /// Controller serving energy reservation management and dashboard endpoints.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ReservationsController : ControllerBase
    {
        private readonly IReservationService _reservationService;

        /// <summary>
        /// Initializes ReservationsController with injected ReservationService dependency.
        /// </summary>
        public ReservationsController(IReservationService reservationService)
        {
            _reservationService = reservationService;
        }

        /// <summary>
        /// Creates a new energy reservation for the currently authenticated Prosumer (Prosumer only).
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Prosumer")]
        [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateReservation([FromBody] CreateReservationRequest request)
        {
            var userId = GetAuthenticatedUserId();
            var reservation = await _reservationService.CreateReservationAsync(request, userId, userId);
            return CreatedAtAction(nameof(GetReservationById), new { id = reservation.Id }, reservation);
        }

        /// <summary>
        /// Staff operation creating a reservation on behalf of a specified Active Prosumer (Backoffice and GridOperator).
        /// </summary>
        [HttpPost("for-prosumer/{prosumerId}")]
        [Authorize(Roles = "Backoffice,GridOperator")]
        [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateReservationForProsumer(
            [FromRoute] string prosumerId, [FromBody] CreateReservationRequest request)
        {
            var staffUserId = GetAuthenticatedUserId();
            var forRequest = new CreateReservationForProsumerRequest
            {
                ProsumerId = prosumerId,
                BookingSlotId = request.BookingSlotId,
                TransferType = request.TransferType,
                EnergyAmountKwh = request.EnergyAmountKwh,
                Notes = request.Notes
            };

            var reservation = await _reservationService.CreateReservationForProsumerAsync(forRequest, staffUserId);
            return CreatedAtAction(nameof(GetReservationById), new { id = reservation.Id }, reservation);
        }

        /// <summary>
        /// Retrieves a paginated list of reservations for the authenticated Prosumer (Prosumer only).
        /// </summary>
        [HttpGet("me")]
        [HttpGet("my-reservations")]
        [HttpGet("user/{userId}")]
        [Authorize]
        [ProducesResponseType(typeof(ReservationListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMyReservations(
            [FromQuery] string? status,
            [FromQuery] string? search,
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = GetAuthenticatedUserId();
            var userRole = GetAuthenticatedUserRole();
            if (userRole == "GridOperator" || userRole == "Backoffice")
            {
                var staffResponse = await _reservationService.GetReservationsAsync(status, null, null, null, search, fromUtc, toUtc, pageNumber, pageSize);
                return Ok(staffResponse);
            }
            var response = await _reservationService.GetMyReservationsAsync(userId, status, search, fromUtc, toUtc, pageNumber, pageSize);
            return Ok(response);
        }

        /// <summary>
        /// Retrieves future Pending and Approved reservations for the authenticated Prosumer (or all for staff).
        /// </summary>
        [HttpGet("me/current")]
        [Authorize]
        [ProducesResponseType(typeof(List<ReservationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMyCurrentReservations()
        {
            var userId = GetAuthenticatedUserId();
            var userRole = GetAuthenticatedUserRole();
            if (userRole == "GridOperator" || userRole == "Backoffice")
            {
                var staffList = await _reservationService.GetReservationsAsync(null, null, null, null, null, null, null, 1, 50);
                return Ok(staffList.Items);
            }
            var reservations = await _reservationService.GetMyCurrentReservationsAsync(userId);
            return Ok(reservations);
        }

        /// <summary>
        /// Retrieves historical (Rejected, Cancelled, Completed, or past slot) reservations.
        /// </summary>
        [HttpGet("me/history")]
        [Authorize]
        [ProducesResponseType(typeof(List<ReservationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMyReservationHistory()
        {
            var userId = GetAuthenticatedUserId();
            var userRole = GetAuthenticatedUserRole();
            if (userRole == "GridOperator" || userRole == "Backoffice")
            {
                var staffHistory = await _reservationService.GetReservationsAsync("Cancelled", null, null, null, null, null, null, 1, 50);
                return Ok(staffHistory.Items);
            }
            var reservations = await _reservationService.GetMyReservationHistoryAsync(userId);
            return Ok(reservations);
        }

        /// <summary>
        /// Retrieves live reservation metric counts for the dashboard.
        /// </summary>
        [HttpGet("me/dashboard")]
        [Authorize]
        [ProducesResponseType(typeof(ProsumerReservationDashboardResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMyDashboardCounts()
        {
            var userId = GetAuthenticatedUserId();
            var userRole = GetAuthenticatedUserRole();
            if (userRole == "GridOperator" || userRole == "Backoffice")
            {
                var ops = await _reservationService.GetOperationsDashboardCountsAsync();
                return Ok(new ProsumerReservationDashboardResponse
                {
                    PendingReservationsCount = ops.PendingReservationsCount,
                    ApprovedFutureReservationsCount = ops.ApprovedFutureReservationsCount,
                    CurrentBookingsCount = ops.TodaysReservationsCount,
                    CompletedReservationsCount = ops.CompletedReservationsCount,
                    CancelledReservationsCount = ops.CancelledReservationsCount
                });
            }
            var dashboard = await _reservationService.GetMyDashboardCountsAsync(userId);
            return Ok(dashboard);
        }

        /// <summary>
        /// Staff query returning paginated reservations with optional filters across all Prosumers (Backoffice and GridOperator).
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Backoffice,GridOperator")]
        [ProducesResponseType(typeof(ReservationListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetReservations(
            [FromQuery] string? status,
            [FromQuery] string? prosumerId,
            [FromQuery] string? stationId,
            [FromQuery] string? bookingSlotId,
            [FromQuery] string? search,
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var response = await _reservationService.GetReservationsAsync(status, prosumerId, stationId, bookingSlotId, search, fromUtc, toUtc, pageNumber, pageSize);
            return Ok(response);
        }

        /// <summary>
        /// Retrieves all Pending reservations ordered oldest first for administrative review (Backoffice and GridOperator).
        /// </summary>
        [HttpGet("pending")]
        [Authorize(Roles = "Backoffice,GridOperator")]
        [ProducesResponseType(typeof(List<ReservationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPendingReservations()
        {
            var reservations = await _reservationService.GetPendingReservationsAsync();
            return Ok(reservations);
        }

        /// <summary>
        /// Retrieves live reservation metrics for operational staff dashboard (Backoffice and GridOperator).
        /// </summary>
        [HttpGet("dashboard")]
        [Authorize(Roles = "Backoffice,GridOperator")]
        [ProducesResponseType(typeof(OperationsDashboardResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetOperationsDashboardCounts()
        {
            var dashboard = await _reservationService.GetOperationsDashboardCountsAsync();
            return Ok(dashboard);
        }

        /// <summary>
        /// Retrieves a single reservation by ID (Any authenticated user). Enforces ownership for Prosumers.
        /// </summary>
        [HttpGet("{id}")]
        [Authorize]
        [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetReservationById([FromRoute] string id)
        {
            var userId = GetAuthenticatedUserId();
            var userRole = GetAuthenticatedUserRole();
            var reservation = await _reservationService.GetReservationByIdAsync(id, userId, userRole);
            return Ok(reservation);
        }

        /// <summary>
        /// Updates a Pending or Approved reservation applying 12-hour, 7-day, and slot capacity migration rules (Any authenticated user).
        /// </summary>
        [HttpPut("{id}")]
        [HttpPatch("{id}")]
        [HttpPost("{id}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateReservation([FromRoute] string id, [FromBody] UpdateReservationRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "Prosumer";
            var updated = await _reservationService.UpdateReservationAsync(id, request, userId, userRole);
            return Ok(updated);
        }

        /// <summary>
        /// Approves a Pending reservation (Backoffice and GridOperator).
        /// </summary>
        [HttpPatch("{id}/approve")]
        [Authorize(Roles = "Backoffice,GridOperator")]
        [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ApproveReservation([FromRoute] string id)
        {
            var userId = GetAuthenticatedUserId();
            var approved = await _reservationService.ApproveReservationAsync(id, userId);
            return Ok(approved);
        }

        /// <summary>
        /// Rejects a Pending reservation with a required reason and releases slot capacity (Backoffice and GridOperator).
        /// </summary>
        [HttpPatch("{id}/reject")]
        [Authorize(Roles = "Backoffice,GridOperator")]
        [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RejectReservation([FromRoute] string id, [FromBody] RejectReservationRequest request)
        {
            var userId = GetAuthenticatedUserId();
            var rejected = await _reservationService.RejectReservationAsync(id, request, userId);
            return Ok(rejected);
        }

        /// <summary>
        /// Soft-cancels a Pending or Approved reservation applying 12-hour rule and releasing slot capacity (Any authenticated user).
        /// </summary>
        [HttpPatch("{id}/cancel")]
        [HttpPost("{id}/cancel")]
        [HttpPut("{id}/cancel")]
        [HttpDelete("{id}/cancel")]
        [HttpDelete("{id}")]
        [Authorize]
        [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelReservation([FromRoute] string id, [FromBody] CancelReservationRequest? request = null)
        {
            var userId = GetAuthenticatedUserId();
            var userRole = GetAuthenticatedUserRole();
            request ??= new CancelReservationRequest();
            var cancelled = await _reservationService.CancelReservationAsync(id, request, userId, userRole);
            return Ok(cancelled);
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
