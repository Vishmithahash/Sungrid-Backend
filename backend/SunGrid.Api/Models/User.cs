// File name: User.cs
// Project name: SunGrid
// Purpose of the file: MongoDB document model representing a user in the UserDetails collection.
// Author placeholder: SunGrid Development Team

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SunGrid.Api.Enums;

namespace SunGrid.Api.Models
{
    /// <summary>
    /// User entity stored in the UserDetails collection in MongoDB.
    /// </summary>
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string? Nic { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.String)]
        public UserRole Role { get; set; }

        [BsonRepresentation(BsonType.String)]
        public AccountStatus AccountStatus { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? DeactivationRequestedAtUtc { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? DeactivatedAtUtc { get; set; }
    }
}
