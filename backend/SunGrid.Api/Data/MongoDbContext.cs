// File name: MongoDbContext.cs
// Project name: SunGrid
// Purpose of the file: Central MongoDB database context providing access to collections and initializing database indexes.
// Author placeholder: SunGrid Development Team

using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SunGrid.Api.Models;
using SunGrid.Api.Settings;

namespace SunGrid.Api.Data
{
    /// <summary>
    /// Encapsulates access to the MongoDB database and collections.
    /// </summary>
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        /// <summary>
        /// Initializes the MongoDB client, connects to the database, and exposes collections.
        /// </summary>
        public MongoDbContext(IOptions<MongoDbSettings> settings)
        {
            var mongoSettings = settings.Value;
            var connectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING")
                ?? Environment.GetEnvironmentVariable("MONGODB_URI")
                ?? (string.IsNullOrWhiteSpace(mongoSettings.ConnectionString) ? "mongodb://localhost:27017" : mongoSettings.ConnectionString);
            
            var clientSettings = MongoClientSettings.FromConnectionString(connectionString);
            clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
            var client = new MongoClient(clientSettings);
            _database = client.GetDatabase(mongoSettings.DatabaseName);

            UserDetails = _database.GetCollection<User>(mongoSettings.UserCollectionName);
            SolarStationInfo = _database.GetCollection<SolarStation>(mongoSettings.StationCollectionName);
            EnergyBookingSlots = _database.GetCollection<EnergyBookingSlot>(mongoSettings.SlotCollectionName);
            EnergyReservations = _database.GetCollection<EnergyReservation>(mongoSettings.ReservationCollectionName);


            // Ensure MongoDB indexes are created asynchronously on context initialization without blocking constructor
            Task.Run(async () => await CreateIndexesAsync());
        }

        /// <summary>
        /// Sends a ping command to MongoDB to verify server connectivity.
        /// </summary>
        public async Task<bool> PingDatabaseAsync()
        {
            try
            {
                var command = new MongoDB.Bson.BsonDocument("ping", 1);
                await _database.RunCommandAsync<MongoDB.Bson.BsonDocument>(command);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// MongoDB collection for user management documents.
        /// </summary>
        public IMongoCollection<User> UserDetails { get; }

        /// <summary>
        /// MongoDB collection for solar microgrid station documents.
        /// </summary>
        public IMongoCollection<SolarStation> SolarStationInfo { get; }

        /// <summary>
        /// MongoDB collection for energy booking slot documents.
        /// </summary>
        public IMongoCollection<EnergyBookingSlot> EnergyBookingSlots { get; }

        /// <summary>
        /// MongoDB collection for energy reservation documents.
        /// </summary>
        public IMongoCollection<EnergyReservation> EnergyReservations { get; }

        /// <summary>
        /// Creates necessary database indexes asynchronously for UserDetails, SolarStationInfo, EnergyBookingSlots, and EnergyReservations collections.
        /// </summary>
        private async Task CreateIndexesAsync()
        {
            try
            {
                // UserDetails: Unique index for normalized Email
                var emailIndexKeys = Builders<User>.IndexKeys.Ascending(u => u.Email);
                var emailIndexOptions = new CreateIndexOptions { Unique = true, Name = "UX_User_Email" };
                await UserDetails.Indexes.CreateOneAsync(new CreateIndexModel<User>(emailIndexKeys, emailIndexOptions));

                // UserDetails: Sparse unique index for NIC (indexes only non-null NIC values)
                var nicIndexKeys = Builders<User>.IndexKeys.Ascending(u => u.Nic);
                var nicIndexOptions = new CreateIndexOptions
                {
                    Unique = true,
                    Sparse = true,
                    Name = "UX_User_Nic_Sparse"
                };
                await UserDetails.Indexes.CreateOneAsync(new CreateIndexModel<User>(nicIndexKeys, nicIndexOptions));

                // SolarStationInfo: Unique index for StationCode
                var stationCodeKeys = Builders<SolarStation>.IndexKeys.Ascending(s => s.StationCode);
                var stationCodeOptions = new CreateIndexOptions { Unique = true, Name = "UX_Station_Code" };
                await SolarStationInfo.Indexes.CreateOneAsync(new CreateIndexModel<SolarStation>(stationCodeKeys, stationCodeOptions));

                // EnergyBookingSlots: Index for StationId
                var slotStationKeys = Builders<EnergyBookingSlot>.IndexKeys.Ascending(s => s.StationId);
                await EnergyBookingSlots.Indexes.CreateOneAsync(new CreateIndexModel<EnergyBookingSlot>(slotStationKeys, new CreateIndexOptions { Name = "IX_Slot_StationId" }));

                // EnergyBookingSlots: Index for StartTimeUtc
                var slotStartKeys = Builders<EnergyBookingSlot>.IndexKeys.Ascending(s => s.StartTimeUtc);
                await EnergyBookingSlots.Indexes.CreateOneAsync(new CreateIndexModel<EnergyBookingSlot>(slotStartKeys, new CreateIndexOptions { Name = "IX_Slot_StartTimeUtc" }));

                // EnergyBookingSlots: Compound index for StationId and StartTimeUtc
                var compoundKeys = Builders<EnergyBookingSlot>.IndexKeys
                    .Ascending(s => s.StationId)
                    .Ascending(s => s.StartTimeUtc);
                await EnergyBookingSlots.Indexes.CreateOneAsync(new CreateIndexModel<EnergyBookingSlot>(compoundKeys, new CreateIndexOptions { Name = "IX_Slot_StationId_StartTimeUtc" }));

                // EnergyReservations: Unique index for ReservationReference
                var resRefKeys = Builders<EnergyReservation>.IndexKeys.Ascending(r => r.ReservationReference);
                await EnergyReservations.Indexes.CreateOneAsync(new CreateIndexModel<EnergyReservation>(resRefKeys, new CreateIndexOptions { Unique = true, Name = "UX_Reservation_Reference" }));

                // EnergyReservations: Single-field indexes
                await EnergyReservations.Indexes.CreateOneAsync(new CreateIndexModel<EnergyReservation>(Builders<EnergyReservation>.IndexKeys.Ascending(r => r.ProsumerId), new CreateIndexOptions { Name = "IX_Reservation_ProsumerId" }));
                await EnergyReservations.Indexes.CreateOneAsync(new CreateIndexModel<EnergyReservation>(Builders<EnergyReservation>.IndexKeys.Ascending(r => r.BookingSlotId), new CreateIndexOptions { Name = "IX_Reservation_BookingSlotId" }));
                await EnergyReservations.Indexes.CreateOneAsync(new CreateIndexModel<EnergyReservation>(Builders<EnergyReservation>.IndexKeys.Ascending(r => r.StationId), new CreateIndexOptions { Name = "IX_Reservation_StationId" }));
                await EnergyReservations.Indexes.CreateOneAsync(new CreateIndexModel<EnergyReservation>(Builders<EnergyReservation>.IndexKeys.Ascending(r => r.Status), new CreateIndexOptions { Name = "IX_Reservation_Status" }));

                // EnergyReservations: Compound indexes
                var prosumerCreatedKeys = Builders<EnergyReservation>.IndexKeys.Ascending(r => r.ProsumerId).Descending(r => r.CreatedAtUtc);
                await EnergyReservations.Indexes.CreateOneAsync(new CreateIndexModel<EnergyReservation>(prosumerCreatedKeys, new CreateIndexOptions { Name = "IX_Reservation_ProsumerId_CreatedAtUtc" }));

                var statusCreatedKeys = Builders<EnergyReservation>.IndexKeys.Ascending(r => r.Status).Descending(r => r.CreatedAtUtc);
                await EnergyReservations.Indexes.CreateOneAsync(new CreateIndexModel<EnergyReservation>(statusCreatedKeys, new CreateIndexOptions { Name = "IX_Reservation_Status_CreatedAtUtc" }));

                // EnergyReservations: Sparse unique index for QrTokenHash
                var qrHashKeys = Builders<EnergyReservation>.IndexKeys.Ascending(r => r.QrTokenHash);
                await EnergyReservations.Indexes.CreateOneAsync(new CreateIndexModel<EnergyReservation>(qrHashKeys, new CreateIndexOptions { Unique = true, Sparse = true, Name = "UX_Reservation_QrTokenHash_Sparse" }));
            }
            catch
            {
                // Index creation errors are swallowed if indexes already exist
            }
        }
    }
}
