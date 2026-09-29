// File name: IAuthService.cs
// Project name: SunGrid
// Purpose of the file: Interface contract for authentication operations (prosumer registration and login).
// Author placeholder: SunGrid Development Team

using SunGrid.Api.DTOs;

namespace SunGrid.Api.Services
{
    /// <summary>
    /// Contract for user registration and authentication business logic.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Registers a new Prosumer account with Pending status.
        /// </summary>
        Task<UserResponse> RegisterProsumerAsync(RegisterProsumerRequest request);

        /// <summary>
        /// Authenticates user credentials and returns a JWT login response for Active accounts.
        /// </summary>
        Task<LoginResponse> LoginAsync(LoginRequest request);
    }
}
