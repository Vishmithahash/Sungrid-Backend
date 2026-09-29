// File name: StationService.cs
// Project name: SunGrid
// Purpose of the file: Implementation of solar station administration, Haversine nearby GPS search, and soft deactivation checks.
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
    /// Service implementing solar microgrid station administration and GPS proximity calculations.
    /// </summary>
    public class StationService : IStationService
    {
        private readonly MongoDbContext _context;

        /// <summary>
        /// Initializes StationService with injected MongoDbContext dependency.
        /// </summary>
        public StationService(MongoDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Creates a new solar microgrid station after validating StationCode uniqueness and operating schedule.
        /// </summary>
        public async Task<StationResponse> CreateStationAsync(CreateStationRequest request, string userId)
        {
            var normalizedCode = request.StationCode.Trim().ToUpperInvariant();

            // Check duplicate StationCode
            var existingStation = await _context.SolarStationInfo
                .Find(s => s.StationCode == normalizedCode)
                .FirstOrDefaultAsync();

            if (existingStation != null)
            {
                throw new ConflictException($"A solar station with station code '{normalizedCode}' already exists.");
            }

            // Validate schedule
            ValidateOperatingSchedule(request.OperatingSchedule);

            var station = new SolarStation
            {
                StationCode = normalizedCode,
                Name = request.Name.Trim(),
                Address = request.Address.Trim(),
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                CapacityKwh = request.CapacityKwh,
                TotalBatteryStorageSlots = request.TotalBatteryStorageSlots,
                OperatingSchedule = MapScheduleDtoToModel(request.OperatingSchedule),
                Status = StationStatus.Active,
                CreatedByUserId = userId,
                UpdatedByUserId = userId,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _context.SolarStationInfo.InsertOneAsync(station);
            var response = MapToStationResponse(station);
            await EnrichStationMetricsAsync(response);
            return response;
        }

        /// <summary>
        /// Fetches paginated station records subject to search query and role-based status visibility.
        /// Grid Operators receive only Active stations; Backoffice can view all statuses.
        /// </summary>
        public async Task<StationListResponse> GetStationsAsync(string? search, string? status, int pageNumber, int pageSize, string userRole)
        {
            pageNumber = pageNumber < 1 ? 1 : pageNumber;
            pageSize = pageSize < 1 ? 10 : pageSize;

            var builder = Builders<SolarStation>.Filter;
            var filter = builder.Empty;

            // Enforce Active status for GridOperator role
            if (userRole.Equals(UserRole.GridOperator.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                filter &= builder.Eq(s => s.Status, StationStatus.Active);
            }
            else if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<StationStatus>(status, true, out var parsedStatus))
            {
                filter &= builder.Eq(s => s.Status, parsedStatus);
            }

            // Apply search query across StationCode, Name, or Address
            if (!string.IsNullOrWhiteSpace(search))
            {
                var query = search.Trim();
                var searchFilter = builder.Or(
                    builder.Regex(s => s.StationCode, new BsonRegularExpression(query, "i")),
                    builder.Regex(s => s.Name, new BsonRegularExpression(query, "i")),
                    builder.Regex(s => s.Address, new BsonRegularExpression(query, "i"))
                );
                filter &= searchFilter;
            }

            var totalCount = await _context.SolarStationInfo.CountDocumentsAsync(filter);

            var stations = await _context.SolarStationInfo
                .Find(filter)
                .SortByDescending(s => s.CreatedAtUtc)
                .Skip((pageNumber - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            var items = stations.Select(MapToStationResponse).ToList();
            foreach (var item in items)
            {
                await EnrichStationMetricsAsync(item);
            }

            return new StationListResponse
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        /// <summary>
        /// Returns all Active solar stations for public or client selection.
        /// </summary>
        public async Task<List<StationResponse>> GetActiveStationsAsync()
        {
            var filter = Builders<SolarStation>.Filter.Eq(s => s.Status, StationStatus.Active);
            var stations = await _context.SolarStationInfo.Find(filter).ToListAsync();
            var items = stations.Select(MapToStationResponse).ToList();
            foreach (var item in items)
            {
                await EnrichStationMetricsAsync(item);
            }
            return items;
        }

        /// <summary>
        /// Calculates distances to all Active stations using the Haversine formula and returns those within radiusKm.
        /// </summary>
        public async Task<List<NearbyStationResponse>> GetNearbyStationsAsync(double latitude, double longitude, double radiusKm)
        {
            if (latitude < -90.0 || latitude > 90.0)
            {
                throw new ArgumentException("Latitude must be between -90 and 90 degrees.");
            }

            if (longitude < -180.0 || longitude > 180.0)
            {
                throw new ArgumentException("Longitude must be between -180 and 180 degrees.");
            }

            radiusKm = radiusKm <= 0 ? 10.0 : radiusKm;

            var activeFilter = Builders<SolarStation>.Filter.Eq(s => s.Status, StationStatus.Active);
            var activeStations = await _context.SolarStationInfo.Find(activeFilter).ToListAsync();

            var nearbyList = new List<NearbyStationResponse>();

            foreach (var station in activeStations)
            {
                var distance = CalculateHaversineDistanceKm(latitude, longitude, station.Latitude, station.Longitude);
                if (distance <= radiusKm)
                {
                    var nearbyResponse = MapToNearbyStationResponse(station, distance);
                    await EnrichStationMetricsAsync(nearbyResponse);
                    nearbyList.Add(nearbyResponse);
                }
            }

            return nearbyList.OrderBy(s => s.DistanceKm).ToList();
        }

        /// <summary>
        /// Retrieves a single station by ID. Restricts Grid Operators and Prosumers to Active stations only.
        /// </summary>
        public async Task<StationResponse> GetStationByIdAsync(string id, string userRole)
        {
            SolarStation? station;
            if (ObjectId.TryParse(id, out _))
            {
                station = await _context.SolarStationInfo.Find(s => s.Id == id).FirstOrDefaultAsync();
            }
            else
            {
                station = await _context.SolarStationInfo.Find(s => s.StationCode == id).FirstOrDefaultAsync();
            }

            if (station == null)
            {
                throw new KeyNotFoundException($"Solar station with ID or Code '{id}' was not found.");
            }

            // Restrict non-Backoffice users from viewing Inactive stations
            if (!userRole.Equals(UserRole.Backoffice.ToString(), StringComparison.OrdinalIgnoreCase) && station.Status != StationStatus.Active)
            {
                throw new KeyNotFoundException($"Solar station with ID '{id}' was not found.");
            }

            var response = MapToStationResponse(station);
            await EnrichStationMetricsAsync(response);
            return response;
        }

        /// <summary>
        /// Updates basic station details (Name, Address, Coordinates, Capacity) for an existing station.
        /// </summary>
        public async Task<StationResponse> UpdateStationAsync(string id, UpdateStationRequest request, string userId)
        {
            ValidateObjectId(id);

            var filter = Builders<SolarStation>.Filter.Eq(s => s.Id, id);
            var update = Builders<SolarStation>.Update
                .Set(s => s.Name, request.Name.Trim())
                .Set(s => s.Address, request.Address.Trim())
                .Set(s => s.Latitude, request.Latitude)
                .Set(s => s.Longitude, request.Longitude)
                .Set(s => s.CapacityKwh, request.CapacityKwh)
                .Set(s => s.TotalBatteryStorageSlots, request.TotalBatteryStorageSlots)
                .Set(s => s.UpdatedByUserId, userId)
                .Set(s => s.UpdatedAtUtc, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<SolarStation> { ReturnDocument = ReturnDocument.After };
            var updatedStation = await _context.SolarStationInfo.FindOneAndUpdateAsync(filter, update, options);

            if (updatedStation == null)
            {
                throw new KeyNotFoundException($"Solar station with ID '{id}' was not found.");
            }

            var response = MapToStationResponse(updatedStation);
            await EnrichStationMetricsAsync(response);
            return response;
        }

        /// <summary>
        /// Replaces the weekly operating schedule for a solar station after validating day uniqueness and times.
        /// </summary>
        public async Task<StationResponse> UpdateStationScheduleAsync(string id, UpdateStationScheduleRequest request, string userId)
        {
            ValidateObjectId(id);
            ValidateOperatingSchedule(request.OperatingSchedule);

            var scheduleModels = MapScheduleDtoToModel(request.OperatingSchedule);

            var filter = Builders<SolarStation>.Filter.Eq(s => s.Id, id);
            var update = Builders<SolarStation>.Update
                .Set(s => s.OperatingSchedule, scheduleModels)
                .Set(s => s.UpdatedByUserId, userId)
                .Set(s => s.UpdatedAtUtc, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<SolarStation> { ReturnDocument = ReturnDocument.After };
            var updatedStation = await _context.SolarStationInfo.FindOneAndUpdateAsync(filter, update, options);

            if (updatedStation == null)
            {
                throw new KeyNotFoundException($"Solar station with ID '{id}' was not found.");
            }

            var response = MapToStationResponse(updatedStation);
            await EnrichStationMetricsAsync(response);
            return response;
        }

        /// <summary>
        /// Performs soft deactivation by setting status to Inactive if no Pending/Approved reservations exist.
        /// </summary>
        public async Task<StationResponse> DeactivateStationAsync(string id, string userId)
        {
            ValidateObjectId(id);

            var station = await _context.SolarStationInfo.Find(s => s.Id == id).FirstOrDefaultAsync();

            if (station == null)
            {
                throw new KeyNotFoundException($"Solar station with ID '{id}' was not found.");
            }

            if (station.Status == StationStatus.Inactive)
            {
                throw new InvalidOperationException("Solar station is already inactive.");
            }

            // Check for active reservations
            var hasReservations = await HasActiveReservationsForStationAsync(id);
            if (hasReservations)
            {
                throw new ConflictException("Cannot deactivate station. Active reservations exist for this station.");
            }

            var filter = Builders<SolarStation>.Filter.Eq(s => s.Id, id);
            var update = Builders<SolarStation>.Update
                .Set(s => s.Status, StationStatus.Inactive)
                .Set(s => s.DeactivatedAtUtc, DateTime.UtcNow)
                .Set(s => s.UpdatedByUserId, userId)
                .Set(s => s.UpdatedAtUtc, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<SolarStation> { ReturnDocument = ReturnDocument.After };
            var updatedStation = await _context.SolarStationInfo.FindOneAndUpdateAsync(filter, update, options);

            var response = MapToStationResponse(updatedStation!);
            await EnrichStationMetricsAsync(response);
            return response;
        }

        /// <summary>
        /// Restores an Inactive station to Active status.
        /// </summary>
        public async Task<StationResponse> ReactivateStationAsync(string id, string userId)
        {
            ValidateObjectId(id);

            var station = await _context.SolarStationInfo.Find(s => s.Id == id).FirstOrDefaultAsync();

            if (station == null)
            {
                throw new KeyNotFoundException($"Solar station with ID '{id}' was not found.");
            }

            if (station.Status != StationStatus.Inactive)
            {
                throw new InvalidOperationException("Only Inactive solar stations can be reactivated.");
            }

            var filter = Builders<SolarStation>.Filter.Eq(s => s.Id, id);
            var update = Builders<SolarStation>.Update
                .Set(s => s.Status, StationStatus.Active)
                .Unset(s => s.DeactivatedAtUtc)
                .Set(s => s.UpdatedByUserId, userId)
                .Set(s => s.UpdatedAtUtc, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<SolarStation> { ReturnDocument = ReturnDocument.After };
            var updatedStation = await _context.SolarStationInfo.FindOneAndUpdateAsync(filter, update, options);

            var response = MapToStationResponse(updatedStation!);
            await EnrichStationMetricsAsync(response);
            return response;
        }

        /// <summary>
        /// Queries the EnergyReservations collection to check if any Pending or Approved reservations reference this station.
        /// </summary>
        public async Task<bool> HasActiveReservationsForStationAsync(string stationId)
        {
            var filter = Builders<EnergyReservation>.Filter.And(
                Builders<EnergyReservation>.Filter.Eq(r => r.StationId, stationId),
                Builders<EnergyReservation>.Filter.In(r => r.Status, new[] { ReservationStatus.Pending, ReservationStatus.Approved })
            );

            var count = await _context.EnergyReservations.CountDocumentsAsync(filter);
            return count > 0;
        }

        /// <summary>
        /// Calculates the great-circle distance between two GPS coordinates using the Haversine formula.
        /// Formula: a = sin²(Δφ/2) + cos φ1 ⋅ cos φ2 ⋅ sin²(Δλ/2); c = 2 ⋅ atan2( √a, √(1−a) ); d = R ⋅ c
        /// where R is Earth radius (6371 km), φ is latitude in radians, and λ is longitude in radians.
        /// </summary>
        private static double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double EarthRadiusKm = 6371.0;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadiusKm * c;
        }

        /// <summary>
        /// Converts degrees to radians.
        /// </summary>
        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

        /// <summary>
        /// Validates that days in operating schedule are distinct and that ClosingTime is later than OpeningTime.
        /// </summary>
        private static void ValidateOperatingSchedule(List<DayOperatingScheduleDto>? schedule)
        {
            if (schedule == null || !schedule.Any()) return;

            var distinctDays = schedule.Select(s => s.DayOfWeek).Distinct().Count();
            if (distinctDays != schedule.Count)
            {
                throw new ArgumentException("Duplicate days found in operating schedule. Each day of the week may appear only once.");
            }

            foreach (var item in schedule)
            {
                if (!item.IsClosed)
                {
                    if (TimeSpan.TryParse(item.OpeningTime, out var openTime) && TimeSpan.TryParse(item.ClosingTime, out var closeTime))
                    {
                        if (closeTime <= openTime)
                        {
                            throw new ArgumentException($"For {item.DayOfWeek}, ClosingTime ({item.ClosingTime}) must be later than OpeningTime ({item.OpeningTime}).");
                        }
                    }
                }
            }
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
        /// Maps schedule DTO list to domain model list.
        /// </summary>
        private static List<DayOperatingSchedule> MapScheduleDtoToModel(List<DayOperatingScheduleDto> dtos)
        {
            return dtos.Select(d => new DayOperatingSchedule
            {
                DayOfWeek = d.DayOfWeek,
                OpeningTime = d.OpeningTime,
                ClosingTime = d.ClosingTime,
                IsClosed = d.IsClosed
            }).ToList();
        }

        /// <summary>
        /// Maps domain model schedule list to DTO list.
        /// </summary>
        private static List<DayOperatingScheduleDto> MapScheduleModelToDto(List<DayOperatingSchedule> models)
        {
            return models.Select(m => new DayOperatingScheduleDto
            {
                DayOfWeek = m.DayOfWeek,
                OpeningTime = m.OpeningTime,
                ClosingTime = m.ClosingTime,
                IsClosed = m.IsClosed
            }).ToList();
        }

        /// <summary>
        /// Computes live received/dispatched energy telemetry and slot storage occupancy for a station.
        /// </summary>
        private async Task EnrichStationMetricsAsync(StationResponse response)
        {
            if (string.IsNullOrWhiteSpace(response.Id)) return;

            var totalSlots = response.TotalBatteryStorageSlots > 0 ? response.TotalBatteryStorageSlots : 6;

            var completedForStation = await _context.EnergyReservations
                .Find(r => r.StationId == response.Id && r.Status == ReservationStatus.Completed)
                .ToListAsync();

            var receivedKwh = completedForStation
                .Where(r => r.TransferType == EnergyTransferType.EnergyDropOff)
                .Sum(r => r.EnergyAmountKwh);

            var dispatchedKwh = completedForStation
                .Where(r => r.TransferType == EnergyTransferType.Charging)
                .Sum(r => r.EnergyAmountKwh);

            var capacity = response.CapacityKwh > 0 ? response.CapacityKwh : 600.0;

            // Physical State of Charge calculation:
            // Net stored energy cannot exceed total physical battery capacity, nor fall below 0
            var netStored = Math.Max(0, receivedKwh - dispatchedKwh);
            var currentStored = Math.Min(capacity, netStored);

            // Active / pending intake reservations awaiting drop-off
            var pendingDropOffList = await _context.EnergyReservations
                .Find(r => r.StationId == response.Id && 
                    (r.Status == ReservationStatus.Approved || r.Status == ReservationStatus.Pending) &&
                    r.TransferType == EnergyTransferType.EnergyDropOff)
                .ToListAsync();

            var pendingIntakeKwh = pendingDropOffList.Sum(r => r.EnergyAmountKwh);
            var availableIntakeKwh = Math.Max(0, capacity - currentStored - pendingIntakeKwh);

            var activeOccupyingCount = await _context.EnergyReservations
                .CountDocumentsAsync(r => r.StationId == response.Id && 
                    (r.Status == ReservationStatus.Approved || r.Status == ReservationStatus.Pending));

            int reservedSlots = (int)Math.Min(totalSlots, activeOccupyingCount);
            int availableSlots = Math.Max(0, totalSlots - reservedSlots);

            // Out of storage if either: no available bays left OR available intake kWh <= 0
            bool isOutOfStorage = availableSlots <= 0 || availableIntakeKwh <= 0;

            response.TotalBatteryStorageSlots = totalSlots;
            response.AvailableSlots = isOutOfStorage ? 0 : availableSlots;
            response.ReservedSlots = isOutOfStorage ? totalSlots : reservedSlots;
            response.ReceivedEnergyKwh = Math.Round(receivedKwh, 2);
            response.DispatchedEnergyKwh = Math.Round(dispatchedKwh, 2);
            response.CurrentStoredEnergyKwh = Math.Round(currentStored, 2);
            response.PendingIntakeKwh = Math.Round(pendingIntakeKwh, 2);
            response.AvailableIntakeKwh = Math.Round(availableIntakeKwh, 2);
            response.IsOutOfStorage = isOutOfStorage;
        }

        /// <summary>
        /// Maps SolarStation domain model to StationResponse DTO.
        /// </summary>
        private static StationResponse MapToStationResponse(SolarStation station)
        {
            return new StationResponse
            {
                Id = station.Id,
                StationCode = station.StationCode,
                Name = station.Name,
                Address = station.Address,
                Latitude = station.Latitude,
                Longitude = station.Longitude,
                CapacityKwh = station.CapacityKwh,
                TotalBatteryStorageSlots = station.TotalBatteryStorageSlots,
                OperatingSchedule = MapScheduleModelToDto(station.OperatingSchedule),
                Status = station.Status.ToString(),
                CreatedByUserId = station.CreatedByUserId,
                UpdatedByUserId = station.UpdatedByUserId,
                CreatedAtUtc = station.CreatedAtUtc,
                UpdatedAtUtc = station.UpdatedAtUtc,
                DeactivatedAtUtc = station.DeactivatedAtUtc
            };
        }

        /// <summary>
        /// Maps SolarStation domain model to NearbyStationResponse DTO including calculated distance.
        /// </summary>
        private static NearbyStationResponse MapToNearbyStationResponse(SolarStation station, double distanceKm)
        {
            var response = MapToStationResponse(station);
            return new NearbyStationResponse
            {
                Id = response.Id,
                StationCode = response.StationCode,
                Name = response.Name,
                Address = response.Address,
                Latitude = response.Latitude,
                Longitude = response.Longitude,
                CapacityKwh = response.CapacityKwh,
                TotalBatteryStorageSlots = response.TotalBatteryStorageSlots,
                OperatingSchedule = response.OperatingSchedule,
                Status = response.Status,
                CreatedByUserId = response.CreatedByUserId,
                UpdatedByUserId = response.UpdatedByUserId,
                CreatedAtUtc = response.CreatedAtUtc,
                UpdatedAtUtc = response.UpdatedAtUtc,
                DeactivatedAtUtc = response.DeactivatedAtUtc,
                DistanceKm = Math.Round(distanceKm, 2)
            };
        }
    }
}
