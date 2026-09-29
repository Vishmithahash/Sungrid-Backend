// File name: IUserService.cs
// Project name: SunGrid
// Purpose of the file: Interface contract for user management operations (profile updates, staff creation, approvals, soft deletes).
// Author placeholder: SunGrid Development Team

using SunGrid.Api.DTOs;

namespace SunGrid.Api.Services
{
    /// <summary>
    /// Contract for user administration and profile management operations.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Retrieves a single user profile by ID.
        /// </summary>
        Task<UserResponse> GetUserByIdAsync(string id);

        /// <summary>
        /// Updates profile information for a specified user (permitted fields only).
        /// </summary>
        Task<UserResponse> UpdateUserProfileAsync(string id, UpdateUserRequest request);

        /// <summary>
        /// Validates current password and updates to new BCrypt hashed password for authenticated user.
        /// </summary>
        Task ChangePasswordAsync(string userId, ChangePasswordRequest request);

        /// <summary>
        /// Allows a Prosumer to request deactivation of their own account.
        /// </summary>
        Task<UserResponse> RequestDeactivationAsync(string userId);

        /// <summary>
        /// Creates a new staff account (Backoffice or GridOperator) with Active status.
        /// </summary>
        Task<UserResponse> CreateStaffUserAsync(CreateStaffUserRequest request);

        /// <summary>
        /// Returns a paginated list of users filtered by role, status, or search query.
        /// </summary>
        Task<UserListResponse> GetUsersAsync(string? role, string? status, string? search, int pageNumber, int pageSize);

        /// <summary>
        /// Returns all Prosumer accounts currently in Pending status.
        /// </summary>
        Task<List<UserResponse>> GetPendingProsumersAsync();

        /// <summary>
        /// Approves a Pending Prosumer account, setting status to Active.
        /// </summary>
        Task<UserResponse> ApproveProsumerAsync(string id);

        /// <summary>
        /// Rejects a Pending Prosumer account, setting status to Rejected.
        /// </summary>
        Task<UserResponse> RejectProsumerAsync(string id);

        /// <summary>
        /// Performs soft deletion of a user by setting status to Deactivated.
        /// </summary>
        Task<UserResponse> DeactivateUserAsync(string id);

        /// <summary>
        /// Reactivates a Deactivated user account, restoring status to Active.
        /// </summary>
        Task<UserResponse> ReactivateUserAsync(string id);
    }
}
