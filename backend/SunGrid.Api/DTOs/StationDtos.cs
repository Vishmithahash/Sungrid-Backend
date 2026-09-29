// File name: StationDtos.cs
// Project name: SunGrid
// Purpose of the file: Data Transfer Objects used for solar microgrid station requests, responses, and GPS search.
// Author placeholder: SunGrid Development Team

using System.ComponentModel.DataAnnotations;
using SunGrid.Api.Enums;

namespace SunGrid.Api.DTOs
{
    /// <summary>
    /// DTO representing the operating schedule for a single day of the week.
    /// </summary>
    public class DayOperatingScheduleDto
    {
        [Required(ErrorMessage = "DayOfWeek is required.")]
        public DayOfWeek DayOfWeek { get; set; }

        [Required(ErrorMessage = "OpeningTime is required.")]
        [RegularExpression(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "OpeningTime must be in 24-hour HH:mm format (e.g. 08:00).")]
        public string OpeningTime { get; set; } = "08:00";

        [Required(ErrorMessage = "ClosingTime is required.")]
        [RegularExpression(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "ClosingTime must be in 24-hour HH:mm format (e.g. 18:00).")]
        public string ClosingTime { get; set; } = "18:00";

        public bool IsClosed { get; set; } = false;
    }

    /// <summary>
    /// Request payload for creating a new solar microgrid station.
    /// </summary>
    public class CreateStationRequest
    {
        [Required(ErrorMessage = "StationCode is required.")]
        [StringLength(20, MinimumLength = 3, ErrorMessage = "StationCode must be between 3 and 20 characters.")]
        public string StationCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Station Name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Station Name must be between 2 and 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Address is required.")]
        [StringLength(200, MinimumLength = 5, ErrorMessage = "Address must be between 5 and 200 characters.")]
        public string Address { get; set; } = string.Empty;

        [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90 degrees.")]
        public double Latitude { get; set; }

        [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180 degrees.")]
        public double Longitude { get; set; }

        [Range(0.01, 100000.0, ErrorMessage = "CapacityKwh must be greater than zero.")]
        public double CapacityKwh { get; set; }

        [Range(1, 1000, ErrorMessage = "TotalBatteryStorageSlots must be greater than zero.")]
        public int TotalBatteryStorageSlots { get; set; }

        public List<DayOperatingScheduleDto> OperatingSchedule { get; set; } = new();
    }

    /// <summary>
    /// Request payload for updating details of an existing solar microgrid station.
    /// </summary>
    public class UpdateStationRequest
    {
        [Required(ErrorMessage = "Station Name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Station Name must be between 2 and 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Address is required.")]
        [StringLength(200, MinimumLength = 5, ErrorMessage = "Address must be between 5 and 200 characters.")]
        public string Address { get; set; } = string.Empty;

        [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90 degrees.")]
        public double Latitude { get; set; }

        [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180 degrees.")]
        public double Longitude { get; set; }

        [Range(0.01, 100000.0, ErrorMessage = "CapacityKwh must be greater than zero.")]
        public double CapacityKwh { get; set; }

        [Range(1, 1000, ErrorMessage = "TotalBatteryStorageSlots must be greater than zero.")]
        public int TotalBatteryStorageSlots { get; set; }
    }

    /// <summary>
    /// Request payload for replacing/updating a station's weekly operating schedule.
    /// </summary>
    public class UpdateStationScheduleRequest
    {
        [Required(ErrorMessage = "OperatingSchedule is required.")]
        public List<DayOperatingScheduleDto> OperatingSchedule { get; set; } = new();
    }

    /// <summary>
    /// Response payload containing safe solar station information.
    /// </summary>
    public class StationResponse
    {
        public string Id { get; set; } = string.Empty;
        public string StationCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double CapacityKwh { get; set; }
        public int TotalBatteryStorageSlots { get; set; }
        public int TotalSlots => TotalBatteryStorageSlots;
        public int AvailableSlots { get; set; }
        public int ReservedSlots { get; set; }
        public int Slots => AvailableSlots;
        public double ReceivedEnergyKwh { get; set; }
        public double DispatchedEnergyKwh { get; set; }
        public double CurrentStoredEnergyKwh { get; set; }
        public double PendingIntakeKwh { get; set; }
        public double AvailableIntakeKwh { get; set; }
        public double BatteryStoragePercentage => CapacityKwh > 0 ? Math.Round((CurrentStoredEnergyKwh / CapacityKwh) * 100, 1) : 0;
        public double NetEnergyStoredKwh => Math.Round(ReceivedEnergyKwh - DispatchedEnergyKwh, 2);
        public bool IsOutOfStorage { get; set; }
        public bool CanReceiveEnergy => !IsOutOfStorage && AvailableIntakeKwh > 0 && AvailableSlots > 0 && Status == "Active";
        public List<DayOperatingScheduleDto> OperatingSchedule { get; set; } = new();
        public string Status { get; set; } = string.Empty;
        public string CreatedByUserId { get; set; } = string.Empty;
        public string UpdatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? DeactivatedAtUtc { get; set; }
    }

    /// <summary>
    /// Response payload for nearby station search including calculated Haversine distance in kilometers.
    /// </summary>
    public class NearbyStationResponse : StationResponse
    {
        public double DistanceKm { get; set; }
    }

    /// <summary>
    /// Paginated list response for station administration queries.
    /// </summary>
    public class StationListResponse
    {
        public List<StationResponse> Items { get; set; } = new();
        public long TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    }
}
