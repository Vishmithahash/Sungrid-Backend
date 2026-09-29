// File name: MongoDbSettings.cs
// Project name: SunGrid
// Purpose of the file: Configuration class for MongoDB connection parameters.
// Author placeholder: SunGrid Development Team

namespace SunGrid.Api.Settings
{
    /// <summary>
    /// Holds MongoDB connection settings mapped from configuration.
    /// </summary>
    public class MongoDbSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = "sungrid";
        public string UserCollectionName { get; set; } = "UserDetails";
        public string StationCollectionName { get; set; } = "SolarStationInfo";
        public string SlotCollectionName { get; set; } = "EnergyBookingSlots";
        public string ReservationCollectionName { get; set; } = "EnergyReservations";
    }
}
