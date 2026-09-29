// File name: DataSeeder.cs
// Project name: SunGrid
// Purpose of the file: Automatic database seeder for creating the initial Backoffice administrator account.
// Author placeholder: SunGrid Development Team

using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SunGrid.Api.Enums;
using SunGrid.Api.Models;
using SunGrid.Api.Settings;

namespace SunGrid.Api.Data
{
    /// <summary>
    /// Handles application database initialization and initial seed data creation.
    /// </summary>
    public class DataSeeder
    {
        private readonly MongoDbContext _context;
        private readonly SeedAdminSettings _adminSettings;
        private readonly ILogger<DataSeeder> _logger;

        /// <summary>
        /// Initializes the seeder with database context, admin settings, and logger dependencies.
        /// </summary>
        public DataSeeder(MongoDbContext context, IOptions<SeedAdminSettings> adminSettings, ILogger<DataSeeder> logger)
        {
            _context = context;
            _adminSettings = adminSettings.Value;
            _logger = logger;
        }

        /// <summary>
        /// Seeds the initial Backoffice account, sample staff/prosumer users, solar stations, booking slots, and reservations.
        /// </summary>
        public async Task SeedInitialAdminAsync()
        {
            if (!_adminSettings.Enabled)
            {
                _logger.LogInformation("Initial database seeding is disabled in configuration.");
                return;
            }

            // 1. Seed Backoffice Admin
            var adminEmail = string.IsNullOrWhiteSpace(_adminSettings.Email) ? "admin@sungrid.com" : _adminSettings.Email.Trim().ToLowerInvariant();
            var adminPassword = string.IsNullOrWhiteSpace(_adminSettings.Password) ? "AdminPassword123!" : _adminSettings.Password;

            var existingAdmin = await _context.UserDetails
                .Find(u => u.Email == adminEmail)
                .FirstOrDefaultAsync();

            User adminUser;
            if (existingAdmin == null)
            {
                adminUser = new User
                {
                    Email = adminEmail,
                    FullName = string.IsNullOrWhiteSpace(_adminSettings.FullName) ? "System Administrator" : _adminSettings.FullName.Trim(),
                    PhoneNumber = "+94112345678",
                    Address = "SunGrid Microgrid Headquarters, Colombo",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                    Role = UserRole.Backoffice,
                    AccountStatus = AccountStatus.Active,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                await _context.UserDetails.InsertOneAsync(adminUser);
                _logger.LogInformation("Successfully created initial Backoffice admin account: {Email}", adminEmail);
            }
            else
            {
                var update = Builders<User>.Update.Set(u => u.PasswordHash, BCrypt.Net.BCrypt.HashPassword(adminPassword));
                await _context.UserDetails.UpdateOneAsync(u => u.Id == existingAdmin.Id, update);
                adminUser = existingAdmin;
            }

            // 2. Seed Grid Operator User
            var operatorEmail = "operator@sungrid.com";
            var existingOperator = await _context.UserDetails.Find(u => u.Email == operatorEmail).FirstOrDefaultAsync();
            User operatorUser;
            if (existingOperator == null)
            {
                operatorUser = new User
                {
                    Email = operatorEmail,
                    FullName = "SunGrid Operator",
                    Nic = "199012345678V",
                    PhoneNumber = "+94771234567",
                    Address = "Grid Operations Office, Kandy",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("OperatorPassword123!"),
                    Role = UserRole.GridOperator,
                    AccountStatus = AccountStatus.Active,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                await _context.UserDetails.InsertOneAsync(operatorUser);
                _logger.LogInformation("Successfully created Grid Operator account: {Email}", operatorEmail);
            }
            else
            {
                operatorUser = existingOperator;
            }

            // 3. Clean up legacy dummy prosumers and sample test reservations
            var deletedReservations = await _context.EnergyReservations.DeleteManyAsync(r => 
                r.ReservationReference == "RES-20260922-000001" || 
                r.ReservationReference == "RES-20260922-000002");
            if (deletedReservations.DeletedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} legacy sample reservations", deletedReservations.DeletedCount);
            }

            var deletedUsers = await _context.UserDetails.DeleteManyAsync(u => 
                u.Email == "prosumer1@sungrid.com" || 
                u.Email == "prosumer2@sungrid.com");
            if (deletedUsers.DeletedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} dummy seed prosumers from database", deletedUsers.DeletedCount);
            }

            // 4. Seed Solar Stations
            var defaultOperatingSchedule = Enum.GetValues<DayOfWeek>().Select(day => new DayOperatingSchedule
            {
                DayOfWeek = day,
                OpeningTime = "08:00",
                ClosingTime = "18:00",
                IsClosed = day == DayOfWeek.Sunday
            }).ToList();

            // 4. Clean up legacy Colombo station and slots to maintain exact 2 active stations in the database
            var colomboStation = await _context.SolarStationInfo.Find(s => s.StationCode == "ST-COL-001").FirstOrDefaultAsync();
            if (colomboStation != null)
            {
                await _context.EnergyBookingSlots.DeleteManyAsync(s => s.StationId == colomboStation.Id);
                await _context.SolarStationInfo.DeleteOneAsync(s => s.Id == colomboStation.Id);
                _logger.LogInformation("Removed legacy Colombo station (ST-COL-001) and associated slots to match database fleet.");
            }

            // Ensure Kandy station exists
            var kandyStation = await _context.SolarStationInfo.Find(s => s.StationCode == "ST-KND-002").FirstOrDefaultAsync();
            if (kandyStation == null)
            {
                kandyStation = new SolarStation
                {
                    StationCode = "ST-KND-002",
                    Name = "Kandy Hillside Microgrid Station",
                    Address = "Peradeniya Road, Kandy",
                    Latitude = 7.2906,
                    Longitude = 80.6337,
                    CapacityKwh = 350.0,
                    TotalBatteryStorageSlots = 8,
                    OperatingSchedule = defaultOperatingSchedule,
                    Status = StationStatus.Active,
                    CreatedByUserId = adminUser.Id,
                    UpdatedByUserId = adminUser.Id,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                await _context.SolarStationInfo.InsertOneAsync(kandyStation);
                _logger.LogInformation("Successfully created Solar Station: ST-KND-002");
            }
        }
    }
}
