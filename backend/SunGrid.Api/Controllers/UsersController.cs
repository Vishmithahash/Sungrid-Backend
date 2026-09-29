// File name: UsersController.cs
// Project name: SunGrid
// Purpose of the file: API endpoints for profile management (/me) and Backoffice user administration operations.
// Author placeholder: SunGrid Development Team

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SunGrid.Api.DTOs;
using SunGrid.Api.Services;

namespace SunGrid.Api.Controllers
{
    /// <summary>
    /// Controller serving user management and profile operations.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        /// <summary>
        /// Initializes UsersController with injected UserService dependency.
        /// </summary>
        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Retrieves current authenticated user's profile details.
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetCurrentUserProfile()
        {
            var userId = GetAuthenticatedUserId();
            var user = await _userService.GetUserByIdAsync(userId);
            return Ok(user);
        }

        /// <summary>
        /// Updates profile information (FullName, PhoneNumber, Address) for current authenticated user.
        /// </summary>
        [HttpPut("me")]
        [Authorize]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateCurrentUserProfile([FromBody] UpdateUserRequest request)
        {
            var userId = GetAuthenticatedUserId();
            var updatedUser = await _userService.UpdateUserProfileAsync(userId, request);
            return Ok(updatedUser);
        }

        /// <summary>
        /// Changes password for the currently authenticated user after verifying current password.
        /// </summary>
        [HttpPatch("me/change-password")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = GetAuthenticatedUserId();
            await _userService.ChangePasswordAsync(userId, request);
            return Ok(new { message = "Password updated successfully." });
        }

        /// <summary>
        /// Submits account deactivation request for the current authenticated Prosumer.
        /// </summary>
        [HttpPatch("me/request-deactivation")]
        [Authorize(Roles = "Prosumer")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RequestSelfDeactivation()
        {
            var userId = GetAuthenticatedUserId();
            var updatedUser = await _userService.RequestDeactivationAsync(userId);
            return Ok(updatedUser);
        }

        /// <summary>
        /// Creates a Backoffice or GridOperator staff user account with Active status.
        /// </summary>
        [HttpPost("staff")]
        [Authorize(Roles = "Backoffice")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateStaffUser([FromBody] CreateStaffUserRequest request)
        {
            var user = await _userService.CreateStaffUserAsync(request);
            return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
        }

        /// <summary>
        /// Retrieves a paginated list of users with optional role, status, and search filters.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Backoffice")]
        [ProducesResponseType(typeof(UserListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetUsers(
            [FromQuery] string? role,
            [FromQuery] string? status,
            [FromQuery] string? search,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var response = await _userService.GetUsersAsync(role, status, search, pageNumber, pageSize);
            return Ok(response);
        }

        /// <summary>
        /// Retrieves all Pending Prosumer accounts awaiting approval.
        /// </summary>
        [HttpGet("prosumers/pending")]
        [Authorize(Roles = "Backoffice")]
        [ProducesResponseType(typeof(List<UserResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPendingProsumers()
        {
            var pendingUsers = await _userService.GetPendingProsumersAsync();
            return Ok(pendingUsers);
        }

        /// <summary>
        /// Retrieves single user by MongoDB ID.
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Roles = "Backoffice")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUserById([FromRoute] string id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            return Ok(user);
        }

        /// <summary>
        /// Updates profile information for a specified user by ID.
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Backoffice")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateUser([FromRoute] string id, [FromBody] UpdateUserRequest request)
        {
            var updatedUser = await _userService.UpdateUserProfileAsync(id, request);
            return Ok(updatedUser);
        }

        /// <summary>
        /// Approves a Pending Prosumer account, transitioning status to Active.
        /// </summary>
        [HttpPatch("{id}/approve")]
        [Authorize(Roles = "Backoffice")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ApproveProsumer([FromRoute] string id)
        {
            var approvedUser = await _userService.ApproveProsumerAsync(id);
            return Ok(approvedUser);
        }

        /// <summary>
        /// Rejects a Pending Prosumer account, transitioning status to Rejected.
        /// </summary>
        [HttpPatch("{id}/reject")]
        [Authorize(Roles = "Backoffice")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RejectProsumer([FromRoute] string id)
        {
            var rejectedUser = await _userService.RejectProsumerAsync(id);
            return Ok(rejectedUser);
        }

        /// <summary>
        /// Performs soft deletion by transitioning account status to Deactivated.
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Backoffice")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeactivateUser([FromRoute] string id)
        {
            var deactivatedUser = await _userService.DeactivateUserAsync(id);
            return Ok(deactivatedUser);
        }

        /// <summary>
        /// Reactivates a Deactivated account, restoring status to Active.
        /// </summary>
        [HttpPatch("{id}/reactivate")]
        [Authorize(Roles = "Backoffice")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReactivateUser([FromRoute] string id)
        {
            var reactivatedUser = await _userService.ReactivateUserAsync(id);
            return Ok(reactivatedUser);
        }

        /// <summary>
        /// Extracts and validates the authenticated user ID from token NameIdentifier claim.
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
    }
}
