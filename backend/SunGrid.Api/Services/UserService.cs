// File name: UserService.cs
// Project name: SunGrid
// Purpose of the file: Service handling user administration, profile management, status transitions, and soft deletion.
// Author placeholder: SunGrid Development Team

using MongoDB.Bson;
using MongoDB.Driver;
using SunGrid.Api.Data;
using SunGrid.Api.DTOs;
using SunGrid.Api.Enums;
using SunGrid.Api.Middleware;
using SunGrid.Api.Models;

namespace SunGrid.Api.Services
{
    /// <summary>
    /// Service implementing user management business rules and MongoDB operations.
    /// </summary>
    public class UserService : IUserService
    {
        private readonly MongoDbContext _context;

        /// <summary>
        /// Initializes the UserService with MongoDbContext dependency.
        /// </summary>
        public UserService(MongoDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves a user document by MongoDB ObjectId string and maps to UserResponse.
        /// </summary>
        public async Task<UserResponse> GetUserByIdAsync(string id)
        {
            ValidateObjectId(id);

            var user = await _context.UserDetails
                .Find(u => u.Id == id)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID '{id}' was not found.");
            }

            return MapToUserResponse(user);
        }

        /// <summary>
        /// Updates only permitted profile fields (FullName, PhoneNumber, Address) for a specified user.
        /// </summary>
        public async Task<UserResponse> UpdateUserProfileAsync(string id, UpdateUserRequest request)
        {
            ValidateObjectId(id);

            var filter = Builders<User>.Filter.Eq(u => u.Id, id);
            var update = Builders<User>.Update
                .Set(u => u.FullName, request.FullName.Trim())
                .Set(u => u.PhoneNumber, request.PhoneNumber.Trim())
                .Set(u => u.Address, request.Address.Trim())
                .Set(u => u.UpdatedAtUtc, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After };
            var updatedUser = await _context.UserDetails.FindOneAndUpdateAsync(filter, update, options);

            if (updatedUser == null)
            {
                throw new KeyNotFoundException($"User with ID '{id}' was not found.");
            }

            return MapToUserResponse(updatedUser);
        }

        /// <summary>
        /// Validates current password using BCrypt and updates user PasswordHash to new hashed password.
        /// </summary>
        public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request)
        {
            ValidateObjectId(userId);

            var user = await _context.UserDetails
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID '{userId}' was not found.");
            }

            // Verify current password against stored BCrypt hash
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Incorrect current password.");
            }

            // Prevent new password from being identical to current password
            if (BCrypt.Net.BCrypt.Verify(request.NewPassword, user.PasswordHash))
            {
                throw new ArgumentException("New password cannot be identical to the current password.");
            }

            // Hash new password and update database
            var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
            var update = Builders<User>.Update
                .Set(u => u.PasswordHash, newPasswordHash)
                .Set(u => u.UpdatedAtUtc, DateTime.UtcNow);

            await _context.UserDetails.UpdateOneAsync(filter, update);
        }

        /// <summary>
        /// Flags an Active Prosumer account as DeactivationRequested.
        /// </summary>
        public async Task<UserResponse> RequestDeactivationAsync(string userId)
        {
            ValidateObjectId(userId);

            var user = await _context.UserDetails
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID '{userId}' was not found.");
            }

            if (user.Role != UserRole.Prosumer)
            {
                throw new InvalidOperationException("Only Prosumers can request self-deactivation.");
            }

            if (user.AccountStatus != AccountStatus.Active)
            {
                throw new InvalidOperationException($"Cannot request deactivation for an account with status '{user.AccountStatus}'. Account must be Active.");
            }

            var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
            var update = Builders<User>.Update
                .Set(u => u.AccountStatus, AccountStatus.DeactivationRequested)
                .Set(u => u.DeactivationRequestedAtUtc, DateTime.UtcNow)
                .Set(u => u.UpdatedAtUtc, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After };
            var updatedUser = await _context.UserDetails.FindOneAndUpdateAsync(filter, update, options);

            return MapToUserResponse(updatedUser!);
        }

        /// <summary>
        /// Creates a Backoffice or GridOperator staff account with Active status.
        /// Rejects Prosumer role creation on staff endpoint.
        /// </summary>
        public async Task<UserResponse> CreateStaffUserAsync(CreateStaffUserRequest request)
        {
            if (request.Role == UserRole.Prosumer)
            {
                throw new ArgumentException("Cannot create Prosumer accounts through the staff endpoint.");
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            var existingUser = await _context.UserDetails
                .Find(u => u.Email == normalizedEmail)
                .FirstOrDefaultAsync();

            if (existingUser != null)
            {
                throw new ConflictException("A user with this email address already exists.");
            }

            var staffUser = new User
            {
                FullName = request.FullName.Trim(),
                Email = normalizedEmail,
                PhoneNumber = request.PhoneNumber.Trim(),
                Address = request.Address.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = request.Role,
                AccountStatus = AccountStatus.Active,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _context.UserDetails.InsertOneAsync(staffUser);

            return MapToUserResponse(staffUser);
        }

        /// <summary>
        /// Fetches paginated user records with optional filters for role, status, or search string.
        /// </summary>
        public async Task<UserListResponse> GetUsersAsync(string? role, string? status, string? search, int pageNumber, int pageSize)
        {
            pageNumber = pageNumber < 1 ? 1 : pageNumber;
            pageSize = pageSize < 1 ? 10 : pageSize;

            var builder = Builders<User>.Filter;
            var filter = builder.Empty;

            // Filter by role if provided
            if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<UserRole>(role, true, out var parsedRole))
            {
                filter &= builder.Eq(u => u.Role, parsedRole);
            }

            // Filter by status if provided
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AccountStatus>(status, true, out var parsedStatus))
            {
                filter &= builder.Eq(u => u.AccountStatus, parsedStatus);
            }

            // Filter by search query across NIC, Email, or FullName
            if (!string.IsNullOrWhiteSpace(search))
            {
                var query = search.Trim();
                var searchFilter = builder.Or(
                    builder.Regex(u => u.FullName, new BsonRegularExpression(query, "i")),
                    builder.Regex(u => u.Email, new BsonRegularExpression(query, "i")),
                    builder.Regex(u => u.Nic, new BsonRegularExpression(query, "i"))
                );
                filter &= searchFilter;
            }

            var totalCount = await _context.UserDetails.CountDocumentsAsync(filter);

            var users = await _context.UserDetails
                .Find(filter)
                .SortByDescending(u => u.CreatedAtUtc)
                .Skip((pageNumber - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return new UserListResponse
            {
                Items = users.Select(MapToUserResponse).ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        /// <summary>
        /// Retrieves all Prosumer accounts currently in Pending status.
        /// </summary>
        public async Task<List<UserResponse>> GetPendingProsumersAsync()
        {
            var filter = Builders<User>.Filter.And(
                Builders<User>.Filter.Eq(u => u.Role, UserRole.Prosumer),
                Builders<User>.Filter.Eq(u => u.AccountStatus, AccountStatus.Pending)
            );

            var pendingUsers = await _context.UserDetails
                .Find(filter)
                .SortByDescending(u => u.CreatedAtUtc)
                .ToListAsync();

            return pendingUsers.Select(MapToUserResponse).ToList();
        }

        /// <summary>
        /// Transition account status from Pending to Active for Prosumers.
        /// </summary>
        public async Task<UserResponse> ApproveProsumerAsync(string id)
        {
            ValidateObjectId(id);

            var user = await _context.UserDetails.Find(u => u.Id == id).FirstOrDefaultAsync();
            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID '{id}' was not found.");
            }

            if (user.AccountStatus != AccountStatus.Pending)
            {
                throw new InvalidOperationException($"Only Pending accounts can be approved. Current status: '{user.AccountStatus}'.");
            }

            var filter = Builders<User>.Filter.Eq(u => u.Id, id);
            var update = Builders<User>.Update
                .Set(u => u.AccountStatus, AccountStatus.Active)
                .Set(u => u.UpdatedAtUtc, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After };
            var updatedUser = await _context.UserDetails.FindOneAndUpdateAsync(filter, update, options);

            return MapToUserResponse(updatedUser!);
        }

        /// <summary>
        /// Transition account status from Pending to Rejected for Prosumers.
        /// </summary>
        public async Task<UserResponse> RejectProsumerAsync(string id)
        {
            ValidateObjectId(id);

            var user = await _context.UserDetails.Find(u => u.Id == id).FirstOrDefaultAsync();
            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID '{id}' was not found.");
            }

            if (user.AccountStatus != AccountStatus.Pending)
            {
                throw new InvalidOperationException($"Only Pending accounts can be rejected. Current status: '{user.AccountStatus}'.");
            }

            var filter = Builders<User>.Filter.Eq(u => u.Id, id);
            var update = Builders<User>.Update
                .Set(u => u.AccountStatus, AccountStatus.Rejected)
                .Set(u => u.UpdatedAtUtc, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After };
            var updatedUser = await _context.UserDetails.FindOneAndUpdateAsync(filter, update, options);

            return MapToUserResponse(updatedUser!);
        }

        /// <summary>
        /// Performs soft deletion of a user account by setting status to Deactivated.
        /// </summary>
        public async Task<UserResponse> DeactivateUserAsync(string id)
        {
            ValidateObjectId(id);

            var user = await _context.UserDetails.Find(u => u.Id == id).FirstOrDefaultAsync();
            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID '{id}' was not found.");
            }

            if (user.AccountStatus == AccountStatus.Deactivated)
            {
                throw new InvalidOperationException("Account is already deactivated.");
            }

            var filter = Builders<User>.Filter.Eq(u => u.Id, id);
            var update = Builders<User>.Update
                .Set(u => u.AccountStatus, AccountStatus.Deactivated)
                .Set(u => u.DeactivatedAtUtc, DateTime.UtcNow)
                .Set(u => u.UpdatedAtUtc, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After };
            var updatedUser = await _context.UserDetails.FindOneAndUpdateAsync(filter, update, options);

            return MapToUserResponse(updatedUser!);
        }

        /// <summary>
        /// Restores a Deactivated account to Active status.
        /// </summary>
        public async Task<UserResponse> ReactivateUserAsync(string id)
        {
            ValidateObjectId(id);

            var user = await _context.UserDetails.Find(u => u.Id == id).FirstOrDefaultAsync();
            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID '{id}' was not found.");
            }

            if (user.AccountStatus != AccountStatus.Deactivated)
            {
                throw new InvalidOperationException($"Only Deactivated accounts can be reactivated. Current status: '{user.AccountStatus}'.");
            }

            var filter = Builders<User>.Filter.Eq(u => u.Id, id);
            var update = Builders<User>.Update
                .Set(u => u.AccountStatus, AccountStatus.Active)
                .Unset(u => u.DeactivatedAtUtc)
                .Set(u => u.UpdatedAtUtc, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After };
            var updatedUser = await _context.UserDetails.FindOneAndUpdateAsync(filter, update, options);

            return MapToUserResponse(updatedUser!);
        }

        /// <summary>
        /// Validates whether a given string is a valid 24-character MongoDB hex ObjectId.
        /// </summary>
        private static void ValidateObjectId(string id)
        {
            if (!ObjectId.TryParse(id, out _))
            {
                throw new ArgumentException($"Invalid MongoDB ObjectId format: '{id}'.");
            }
        }

        /// <summary>
        /// Maps a User model to a UserResponse DTO.
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
