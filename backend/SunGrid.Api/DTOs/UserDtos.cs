// File name: UserDtos.cs
// Project name: SunGrid
// Purpose of the file: Data Transfer Objects used for user management requests and responses.
// Author placeholder: SunGrid Development Team

using System.ComponentModel.DataAnnotations;
using SunGrid.Api.Enums;

namespace SunGrid.Api.DTOs
{
    /// <summary>
    /// Request payload for creating staff accounts (Backoffice or GridOperator).
    /// </summary>
    public class CreateStaffUserRequest
    {
        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Full Name must be between 2 and 100 characters.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [Phone(ErrorMessage = "Invalid phone number format.")]
        [StringLength(15, MinimumLength = 9, ErrorMessage = "Phone number must be between 9 and 15 digits.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Address is required.")]
        [StringLength(200, MinimumLength = 5, ErrorMessage = "Address must be between 5 and 200 characters.")]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm Password is required.")]
        [Compare("Password", ErrorMessage = "Password and Confirm Password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role is required.")]
        public UserRole Role { get; set; }
    }

    /// <summary>
    /// Request payload for updating permitted profile fields of a user.
    /// </summary>
    public class UpdateUserRequest
    {
        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Full Name must be between 2 and 100 characters.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [Phone(ErrorMessage = "Invalid phone number format.")]
        [StringLength(15, MinimumLength = 9, ErrorMessage = "Phone number must be between 9 and 15 digits.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Address is required.")]
        [StringLength(200, MinimumLength = 5, ErrorMessage = "Address must be between 5 and 200 characters.")]
        public string Address { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request payload for authenticated user password changes.
    /// </summary>
    public class ChangePasswordRequest
    {
        [Required(ErrorMessage = "Current password is required.")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "New password is required.")]
        [MinLength(8, ErrorMessage = "New password must be at least 8 characters long.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm new password is required.")]
        [Compare("NewPassword", ErrorMessage = "New password and Confirm new password do not match.")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response payload containing safe user information (excluding PasswordHash).
    /// </summary>
    public class UserResponse
    {
        public string Id { get; set; } = string.Empty;
        public string? Nic { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string AccountStatus { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? DeactivationRequestedAtUtc { get; set; }
        public DateTime? DeactivatedAtUtc { get; set; }
    }

    /// <summary>
    /// Paginated list response for user management queries.
    /// </summary>
    public class UserListResponse
    {
        public List<UserResponse> Items { get; set; } = new();
        public long TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    }
}
