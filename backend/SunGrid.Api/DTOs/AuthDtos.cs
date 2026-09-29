// File name: AuthDtos.cs
// Project name: SunGrid
// Purpose of the file: Data Transfer Objects used for authentication and registration requests/responses.
// Author placeholder: SunGrid Development Team

using System.ComponentModel.DataAnnotations;

namespace SunGrid.Api.DTOs
{
    /// <summary>
    /// Request payload for registering a new Prosumer.
    /// </summary>
    public class RegisterProsumerRequest
    {
        [Required(ErrorMessage = "NIC is required for Prosumers.")]
        [RegularExpression(@"^([0-9]{9}[vVxX]|[0-9]{12})$", ErrorMessage = "Invalid Sri Lankan NIC format. Enter 9 digits followed by V/X or 12 digits.")]
        public string Nic { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Full Name must be between 2 and 100 characters.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        public string Email { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
        public string Password { get; set; } = string.Empty;

        public string ConfirmPassword { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request payload for user login.
    /// </summary>
    public class LoginRequest
    {
        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response payload returned upon successful user login.
    /// </summary>
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public int ExpiryMinutes { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
