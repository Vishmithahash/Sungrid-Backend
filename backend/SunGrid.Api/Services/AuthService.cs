// File name: AuthService.cs
// Project name: SunGrid
// Purpose of the file: Implementation of authentication business logic, password hashing, and login authentication.
// Author placeholder: SunGrid Development Team

using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SunGrid.Api.Data;
using SunGrid.Api.DTOs;
using SunGrid.Api.Enums;
using SunGrid.Api.Middleware;
using SunGrid.Api.Models;
using SunGrid.Api.Settings;

namespace SunGrid.Api.Services
{
    /// <summary>
    /// Service implementing registration and authentication rules.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly MongoDbContext _context;
        private readonly IJwtService _jwtService;
        private readonly JwtSettings _jwtSettings;

        /// <summary>
        /// Initializes the AuthService with MongoDbContext, JwtService, and JwtSettings dependencies.
        /// </summary>
        public AuthService(MongoDbContext context, IJwtService jwtService, IOptions<JwtSettings> jwtSettings)
        {
            _context = context;
            _jwtService = jwtService;
            _jwtSettings = jwtSettings.Value;
        }

        /// <summary>
        /// Registers a new Prosumer with normalized NIC/email and Pending account status.
        /// Throws a ConflictException if email or NIC is already registered.
        /// </summary>
        public async Task<UserResponse> RegisterProsumerAsync(RegisterProsumerRequest request)
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var normalizedNic = request.Nic.Trim().ToUpperInvariant();

            // Check duplicate email
            var existingEmail = await _context.UserDetails
                .Find(u => u.Email == normalizedEmail)
                .FirstOrDefaultAsync();

            if (existingEmail != null)
            {
                throw new ConflictException("A user with this email address already exists.");
            }

            // Check duplicate NIC
            var existingNic = await _context.UserDetails
                .Find(u => u.Nic == normalizedNic)
                .FirstOrDefaultAsync();

            if (existingNic != null)
            {
                throw new ConflictException("A user with this NIC already exists.");
            }

            // Create new Prosumer document
            var prosumer = new User
            {
                Nic = normalizedNic,
                FullName = request.FullName.Trim(),
                Email = normalizedEmail,
                PhoneNumber = request.PhoneNumber.Trim(),
                Address = request.Address.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = UserRole.Prosumer,
                AccountStatus = AccountStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _context.UserDetails.InsertOneAsync(prosumer);

            return MapToUserResponse(prosumer);
        }

        /// <summary>
        /// Validates user credentials and account active status, returning JWT authentication payload.
        /// </summary>
        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            // Find user by normalized email
            var user = await _context.UserDetails
                .Find(u => u.Email == normalizedEmail)
                .FirstOrDefaultAsync();

            // Return generic message for invalid email or password check
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Invalid email or password.");
            }

            // Verify account status is Active
            if (user.AccountStatus != AccountStatus.Active)
            {
                throw new InvalidOperationException($"Your account is currently in '{user.AccountStatus}' status. Only Active users can log in.");
            }

            // Generate JWT token
            var token = _jwtService.GenerateToken(user);

            return new LoginResponse
            {
                Token = token,
                ExpiryMinutes = _jwtSettings.ExpiryMinutes,
                UserId = user.Id,
                FullName = user.FullName,
                Role = user.Role.ToString()
            };
        }

        /// <summary>
        /// Helper method to map a User domain model to a safe UserResponse DTO.
        /// </summary>
        private static UserResponse MapToUserResponse(User user)
        {
            return new UserResponse
            {
                Id = user.Id,
                Nic = user.Nic,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address,
                Role = user.Role.ToString(),
                AccountStatus = user.AccountStatus.ToString(),
                CreatedAtUtc = user.CreatedAtUtc,
                UpdatedAtUtc = user.UpdatedAtUtc,
                DeactivationRequestedAtUtc = user.DeactivationRequestedAtUtc,
                DeactivatedAtUtc = user.DeactivatedAtUtc
            };
        }
    }
}
