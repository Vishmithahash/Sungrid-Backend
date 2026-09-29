// File name: IJwtService.cs
// Project name: SunGrid
// Purpose of the file: Interface contract for JWT token creation service.
// Author placeholder: SunGrid Development Team

using SunGrid.Api.Models;

namespace SunGrid.Api.Services
{
    /// <summary>
    /// Contract for generating JWT bearer tokens for authenticated users.
    /// </summary>
    public interface IJwtService
    {
        /// <summary>
        /// Generates a signed JWT token containing essential user claims (Id, Email, Role).
        /// </summary>
        string GenerateToken(User user);
    }
}
