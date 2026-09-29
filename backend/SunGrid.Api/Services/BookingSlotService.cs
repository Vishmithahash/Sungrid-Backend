// File name: BookingSlotService.cs
// Project name: SunGrid
// Purpose of the file: Service handling energy booking slot creation, overlap prevention, capacity adjustments, and soft closure.
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
    /// Service implementing energy booking slot business rules and MongoDB operations.
    /// </summary>
    public class BookingSlotService : IBookingSlotService
    {
        private readonly MongoDbContext _context;

        /// <summary>
        /// Initializes BookingSlotService with injected MongoDbContext dependency.
        /// </summary>
        public BookingSlotService(MongoDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Creates a new energy booking slot after validating station status, future start time, capacity, and slot overlap.
        /// </summary>
        public async Task<BookingSlotResponse> CreateSlotAsync(string stationId, CreateBookingSlotRequest request, string userId)
        {
            ValidateObjectId(stationId);

            var station = await _context.SolarStationInfo.Find(s => s.Id == stationId).FirstOrDefaultAsync();
            if (station == null)
            {
                throw new KeyNotFoundException($"Solar station with ID '{stationId}' was not found.");
            }

            if (station.Status != StationStatus.Active)
            {
                throw new InvalidOperationException("Cannot create booking slots for an Inactive station.");
            }

            // Validate dates
            if (request.StartTimeUtc >= request.EndTimeUtc)
            {
                throw new ArgumentException("StartTimeUtc must be earlier than EndTimeUtc.");
            }

            if (request.StartTimeUtc <= DateTime.UtcNow)
            {
                throw new ArgumentException("Slot start time must be in the future.");
            }

            // Validate total capacity against station storage slots
            if (request.TotalCapacity > station.TotalBatteryStorageSlots)
            {
                throw new ArgumentException($"TotalCapacity ({request.TotalCapacity}) cannot exceed the station's total battery storage slots ({station.TotalBatteryStorageSlots}).");
            }

            // Check overlapping slots for the same station
            var hasOverlap = await CheckSlotOverlapAsync(stationId, request.StartTimeUtc, request.EndTimeUtc, null);
            if (hasOverlap)
            {
                throw new ConflictException("The proposed slot time range overlaps with an existing slot for this station.");
            }

            var slot = new EnergyBookingSlot
            {
                StationId = stationId,
                StartTimeUtc = request.StartTimeUtc,
                EndTimeUtc = request.EndTimeUtc,
                TotalCapacity = request.TotalCapacity,
                AvailableCapacity = request.TotalCapacity,
                Status = BookingSlotStatus.Available,
                CreatedByUserId = userId,
                UpdatedByUserId = userId,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _context.EnergyBookingSlots.InsertOneAsync(slot);
            return MapToBookingSlotResponse(slot);
        }

        /// <summary>
        /// Fetches energy booking slots for a specified station applying role-based visibility and date filters.
        /// Prosumers see only future Available slots; Grid Operators see operational slots; Backoffice sees all.
        /// </summary>
        public async Task<List<BookingSlotResponse>> GetSlotsForStationAsync(string stationId, DateTime? fromUtc, DateTime? toUtc, string? status, bool includePast, string userRole)
        {
            SolarStation? station;
            string resolvedStationId = stationId;
            if (ObjectId.TryParse(stationId, out _))
            {
                station = await _context.SolarStationInfo.Find(s => s.Id == stationId).FirstOrDefaultAsync();
            }
            else
            {
                station = await _context.SolarStationInfo.Find(s => s.StationCode == stationId).FirstOrDefaultAsync();
                if (station != null)
                {
                    resolvedStationId = station.Id;
                }
            }

            if (station == null)
            {
                return new List<BookingSlotResponse>();
            }

            var builder = Builders<EnergyBookingSlot>.Filter;
            var filter = builder.Eq(s => s.StationId, resolvedStationId);

            // Role-based visibility enforcement
            if (string.IsNullOrEmpty(userRole) || userRole.Equals(UserRole.Prosumer.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                if (station.Status != StationStatus.Active)
                {
                    throw new KeyNotFoundException($"Solar station with ID '{stationId}' was not found.");
                }
                filter &= builder.Gt(s => s.StartTimeUtc, DateTime.UtcNow);
                filter &= builder.Eq(s => s.Status, BookingSlotStatus.Available);
            }
            else if (userRole.Equals(UserRole.GridOperator.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                if (station.Status != StationStatus.Active)
                {
                    throw new KeyNotFoundException($"Solar station with ID '{stationId}' was not found.");
                }
                filter &= builder.Ne(s => s.Status, BookingSlotStatus.Closed);
                if (!includePast)
                {
                    filter &= builder.Gt(s => s.StartTimeUtc, DateTime.UtcNow);
                }
            }
            else
            {
                // Backoffice role filter
                if (!includePast)
                {
                    filter &= builder.Gt(s => s.StartTimeUtc, DateTime.UtcNow);
                }

                if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookingSlotStatus>(status, true, out var parsedStatus))
                {
                    filter &= builder.Eq(s => s.Status, parsedStatus);
                }
            }

            // Apply optional date range filters
            if (fromUtc.HasValue)
            {
                filter &= builder.Gte(s => s.StartTimeUtc, fromUtc.Value);
            }
            if (toUtc.HasValue)
            {
                filter &= builder.Lte(s => s.EndTimeUtc, toUtc.Value);
            }

            var slots = await _context.EnergyBookingSlots
                .Find(filter)
                .SortBy(s => s.StartTimeUtc)
                .ToListAsync();

            return slots.Select(MapToBookingSlotResponse).ToList();
        }

        /// <summary>
        /// Retrieves a single energy booking slot by ID subject to role-based visibility.
        /// </summary>
        public async Task<BookingSlotResponse> GetSlotByIdAsync(string id, string userRole)
        {
            ValidateObjectId(id);

            var slot = await _context.EnergyBookingSlots.Find(s => s.Id == id).FirstOrDefaultAsync();
            if (slot == null)
            {
                throw new KeyNotFoundException($"Energy booking slot with ID '{id}' was not found.");
            }

            // Prosumers can only view future Available slots
            if (userRole.Equals(UserRole.Prosumer.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                if (slot.StartTimeUtc <= DateTime.UtcNow || slot.Status != BookingSlotStatus.Available)
                {
                    throw new KeyNotFoundException($"Energy booking slot with ID '{id}' was not found.");
                }
            }

            return MapToBookingSlotResponse(slot);
        }

        /// <summary>
        /// Updates timing and total capacity for a future booking slot after validating overlap and reserved capacity bounds.
        /// </summary>
        public async Task<BookingSlotResponse> UpdateSlotAsync(string id, UpdateBookingSlotRequest request, string userId)
        {
            ValidateObjectId(id);

            var slot = await _context.EnergyBookingSlots.Find(s => s.Id == id).FirstOrDefaultAsync();
            if (slot == null)
            {
                throw new KeyNotFoundException($"Energy booking slot with ID '{id}' was not found.");
            }

            if (slot.StartTimeUtc <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("Cannot modify a past or ongoing booking slot.");
            }

            // Check if slot timing is changing while active reservations exist
            var isTimingChanged = request.StartTimeUtc != slot.StartTimeUtc || request.EndTimeUtc != slot.EndTimeUtc;
            var activeReservationsCount = await GetActiveReservationCountForSlotAsync(id);
            if (isTimingChanged && activeReservationsCount > 0)
            {
                throw new ConflictException("Slot start/end times cannot be changed while Pending or Approved reservations exist.");
            }

            var station = await _context.SolarStationInfo.Find(s => s.Id == slot.StationId).FirstOrDefaultAsync();
            if (station != null && request.TotalCapacity > station.TotalBatteryStorageSlots)
            {
                throw new ArgumentException($"TotalCapacity ({request.TotalCapacity}) cannot exceed the station's total battery storage slots ({station.TotalBatteryStorageSlots}).");
            }

            // Ensure total capacity is not reduced below active reservation count
            if (request.TotalCapacity < activeReservationsCount)
            {
                throw new ArgumentException($"TotalCapacity ({request.TotalCapacity}) cannot be reduced below the number of active reservations ({activeReservationsCount}).");
            }

            var reservedCapacity = slot.TotalCapacity - slot.AvailableCapacity;
            if (request.TotalCapacity < reservedCapacity)
            {
                throw new ArgumentException($"TotalCapacity ({request.TotalCapacity}) cannot be less than currently reserved capacity ({reservedCapacity}).");
            }

            if (request.StartTimeUtc >= request.EndTimeUtc)
            {
                throw new ArgumentException("StartTimeUtc must be earlier than EndTimeUtc.");
            }

            if (request.StartTimeUtc <= DateTime.UtcNow)
            {
                throw new ArgumentException("Slot start time must be in the future.");
            }

            // Re-check slot overlap
            var hasOverlap = await CheckSlotOverlapAsync(slot.StationId, request.StartTimeUtc, request.EndTimeUtc, id);
            if (hasOverlap)
            {
                throw new ConflictException("Updated slot timing overlaps with another existing slot.");
            }

            var newAvailable = request.TotalCapacity - reservedCapacity;
            var newStatus = slot.Status == BookingSlotStatus.Closed
                ? BookingSlotStatus.Closed
                : (newAvailable == 0 ? BookingSlotStatus.Full : BookingSlotStatus.Available);

            var filter = Builders<EnergyBookingSlot>.Filter.Eq(s => s.Id, id);
            var update = Builders<EnergyBookingSlot>.Update
                .Set(s => s.StartTimeUtc, request.StartTimeUtc)
                .Set(s => s.EndTimeUtc, request.EndTimeUtc)
                .Set(s => s.TotalCapacity, request.TotalCapacity)
                .Set(s => s.AvailableCapacity, newAvailable)
                .Set(s => s.Status, newStatus)
                .Set(s => s.UpdatedByUserId, userId)
                .Set(s => s.UpdatedAtUtc, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<EnergyBookingSlot> { ReturnDocument = ReturnDocument.After };
            var updatedSlot = await _context.EnergyBookingSlots.FindOneAndUpdateAsync(filter, update, options);

            return MapToBookingSlotResponse(updatedSlot!);
        }

        /// <summary>
        /// Adjusts available capacity and updates status to Full or Available automatically.
        /// Cannot set AvailableCapacity above TotalCapacity minus active reservation count.
        /// </summary>
        public async Task<BookingSlotResponse> UpdateSlotAvailabilityAsync(string id, UpdateSlotAvailabilityRequest request, string userId)
        {
            ValidateObjectId(id);

            var slot = await _context.EnergyBookingSlots.Find(s => s.Id == id).FirstOrDefaultAsync();
            if (slot == null)
            {
                throw new KeyNotFoundException($"Energy booking slot with ID '{id}' was not found.");
            }

            if (slot.StartTimeUtc <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("Cannot update availability for a past booking slot.");
            }

            var activeReservationsCount = await GetActiveReservationCountForSlotAsync(id);
            var maxAllowedAvailable = slot.TotalCapacity - (int)activeReservationsCount;

            if (request.AvailableCapacity < 0 || request.AvailableCapacity > slot.TotalCapacity)
            {
                throw new ArgumentException($"AvailableCapacity must be between 0 and TotalCapacity ({slot.TotalCapacity}).");
            }

            if (request.AvailableCapacity > maxAllowedAvailable)
            {
                throw new ArgumentException($"AvailableCapacity ({request.AvailableCapacity}) cannot exceed maximum allowed capacity ({maxAllowedAvailable}) based on active reservations.");
            }

            var newStatus = slot.Status == BookingSlotStatus.Closed
                ? BookingSlotStatus.Closed
                : (request.AvailableCapacity == 0 ? BookingSlotStatus.Full : BookingSlotStatus.Available);

            var filter = Builders<EnergyBookingSlot>.Filter.Eq(s => s.Id, id);
            var update = Builders<EnergyBookingSlot>.Update
                .Set(s => s.AvailableCapacity, request.AvailableCapacity)
                .Set(s => s.Status, newStatus)
                .Set(s => s.UpdatedByUserId, userId)
                .Set(s => s.UpdatedAtUtc, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<EnergyBookingSlot> { ReturnDocument = ReturnDocument.After };
            var updatedSlot = await _context.EnergyBookingSlots.FindOneAndUpdateAsync(filter, update, options);

            return MapToBookingSlotResponse(updatedSlot!);
        }

        /// <summary>
        /// Performs soft closure of a slot by setting status to Closed if no active reservations exist.
        /// </summary>
        public async Task<BookingSlotResponse> CloseSlotAsync(string id, string userId)
        {
            ValidateObjectId(id);

            var slot = await _context.EnergyBookingSlots.Find(s => s.Id == id).FirstOrDefaultAsync();
            if (slot == null)
            {
                throw new KeyNotFoundException($"Energy booking slot with ID '{id}' was not found.");
            }

            if (slot.Status == BookingSlotStatus.Closed)
            {
                throw new InvalidOperationException("Booking slot is already closed.");
            }

            // Check active reservations for this slot
            var hasReservations = await HasActiveReservationsForSlotAsync(id);
            if (hasReservations)
            {
                throw new ConflictException("Cannot close slot. Active reservations exist for this slot.");
            }

            var filter = Builders<EnergyBookingSlot>.Filter.Eq(s => s.Id, id);
            var update = Builders<EnergyBookingSlot>.Update
                .Set(s => s.Status, BookingSlotStatus.Closed)
                .Set(s => s.ClosedAtUtc, DateTime.UtcNow)
                .Set(s => s.UpdatedByUserId, userId)
                .Set(s => s.UpdatedAtUtc, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<EnergyBookingSlot> { ReturnDocument = ReturnDocument.After };
            var updatedSlot = await _context.EnergyBookingSlots.FindOneAndUpdateAsync(filter, update, options);

            return MapToBookingSlotResponse(updatedSlot!);
        }

        /// <summary>
        /// Reopens a future Closed booking slot and sets status to Available or Full based on AvailableCapacity.
        /// </summary>
        public async Task<BookingSlotResponse> ReopenSlotAsync(string id, string userId)
        {
            ValidateObjectId(id);

            var slot = await _context.EnergyBookingSlots.Find(s => s.Id == id).FirstOrDefaultAsync();
            if (slot == null)
            {
                throw new KeyNotFoundException($"Energy booking slot with ID '{id}' was not found.");
            }

            if (slot.Status != BookingSlotStatus.Closed)
            {
                throw new InvalidOperationException("Only Closed booking slots can be reopened.");
            }

            if (slot.StartTimeUtc <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("Cannot reopen a past booking slot.");
            }

            var newStatus = slot.AvailableCapacity == 0 ? BookingSlotStatus.Full : BookingSlotStatus.Available;

            var filter = Builders<EnergyBookingSlot>.Filter.Eq(s => s.Id, id);
            var update = Builders<EnergyBookingSlot>.Update
                .Set(s => s.Status, newStatus)
                .Unset(s => s.ClosedAtUtc)
                .Set(s => s.UpdatedByUserId, userId)
                .Set(s => s.UpdatedAtUtc, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<EnergyBookingSlot> { ReturnDocument = ReturnDocument.After };
            var updatedSlot = await _context.EnergyBookingSlots.FindOneAndUpdateAsync(filter, update, options);

            return MapToBookingSlotResponse(updatedSlot!);
        }

        /// <summary>
        /// Queries the EnergyReservations collection to check if any Pending or Approved reservations reference this slot.
        /// </summary>
        public async Task<bool> HasActiveReservationsForSlotAsync(string slotId)
        {
            var count = await GetActiveReservationCountForSlotAsync(slotId);
            return count > 0;
        }

        /// <summary>
        /// Gets the count of Pending or Approved reservations referencing a slot.
        /// </summary>
        private async Task<long> GetActiveReservationCountForSlotAsync(string slotId)
        {
            var filter = Builders<EnergyReservation>.Filter.And(
                Builders<EnergyReservation>.Filter.Eq(r => r.BookingSlotId, slotId),
                Builders<EnergyReservation>.Filter.In(r => r.Status, new[] { ReservationStatus.Pending, ReservationStatus.Approved })
            );

            return await _context.EnergyReservations.CountDocumentsAsync(filter);
        }

        /// <summary>
        /// Atomically decrements slot available capacity by 1 if capacity is available. Sets status to Full when 0.
        /// </summary>
        public async Task<bool> TryReserveOneCapacityUnitAsync(string slotId)
        {
            ValidateObjectId(slotId);

            var filter = Builders<EnergyBookingSlot>.Filter.And(
                Builders<EnergyBookingSlot>.Filter.Eq(s => s.Id, slotId),
                Builders<EnergyBookingSlot>.Filter.Gt(s => s.AvailableCapacity, 0),
                Builders<EnergyBookingSlot>.Filter.Ne(s => s.Status, BookingSlotStatus.Closed)
            );

            var slot = await _context.EnergyBookingSlots.Find(filter).FirstOrDefaultAsync();
            if (slot == null) return false;

            var newAvailable = slot.AvailableCapacity - 1;
            var newStatus = newAvailable == 0 ? BookingSlotStatus.Full : BookingSlotStatus.Available;

            var updateFilter = Builders<EnergyBookingSlot>.Filter.And(
                Builders<EnergyBookingSlot>.Filter.Eq(s => s.Id, slotId),
                Builders<EnergyBookingSlot>.Filter.Eq(s => s.AvailableCapacity, slot.AvailableCapacity)
            );

            var update = Builders<EnergyBookingSlot>.Update
                .Set(s => s.AvailableCapacity, newAvailable)
                .Set(s => s.Status, newStatus)
                .Set(s => s.UpdatedAtUtc, DateTime.UtcNow);

            var result = await _context.EnergyBookingSlots.UpdateOneAsync(updateFilter, update);
            return result.ModifiedCount > 0;
        }

        /// <summary>
        /// Atomically increments slot available capacity by 1 if below TotalCapacity. Sets non-closed slot status to Available.
        /// </summary>
        public async Task<bool> ReleaseOneCapacityUnitAsync(string slotId)
        {
            ValidateObjectId(slotId);

            var slot = await _context.EnergyBookingSlots.Find(s => s.Id == slotId).FirstOrDefaultAsync();
            if (slot == null || slot.AvailableCapacity >= slot.TotalCapacity) return false;

            var newAvailable = slot.AvailableCapacity + 1;
            var newStatus = slot.Status == BookingSlotStatus.Closed
                ? BookingSlotStatus.Closed
                : BookingSlotStatus.Available;

            var filter = Builders<EnergyBookingSlot>.Filter.And(
                Builders<EnergyBookingSlot>.Filter.Eq(s => s.Id, slotId),
                Builders<EnergyBookingSlot>.Filter.Eq(s => s.AvailableCapacity, slot.AvailableCapacity)
            );

            var update = Builders<EnergyBookingSlot>.Update
                .Set(s => s.AvailableCapacity, newAvailable)
                .Set(s => s.Status, newStatus)
                .Set(s => s.UpdatedAtUtc, DateTime.UtcNow);

            var result = await _context.EnergyBookingSlots.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        /// <summary>
        /// Checks if a proposed start and end time range overlaps with any existing non-closed slot for a station.
        /// Overlap condition: (StartA < EndB) AND (EndA > StartB).
        /// </summary>
        private async Task<bool> CheckSlotOverlapAsync(string stationId, DateTime startTimeUtc, DateTime endTimeUtc, string? excludeSlotId)
        {
            var builder = Builders<EnergyBookingSlot>.Filter;
            var filter = builder.And(
                builder.Eq(s => s.StationId, stationId),
                builder.Ne(s => s.Status, BookingSlotStatus.Closed),
                builder.Lt(s => s.StartTimeUtc, endTimeUtc),
                builder.Gt(s => s.EndTimeUtc, startTimeUtc)
            );

            if (!string.IsNullOrEmpty(excludeSlotId))
            {
                filter &= builder.Ne(s => s.Id, excludeSlotId);
            }

            var count = await _context.EnergyBookingSlots.CountDocumentsAsync(filter);
            return count > 0;
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
        /// Maps EnergyBookingSlot domain model to BookingSlotResponse DTO.
        /// </summary>
        private static BookingSlotResponse MapToBookingSlotResponse(EnergyBookingSlot slot)
        {
            return new BookingSlotResponse
            {
                Id = slot.Id,
                StationId = slot.StationId,
                StartTimeUtc = slot.StartTimeUtc,
                EndTimeUtc = slot.EndTimeUtc,
                TotalCapacity = slot.TotalCapacity,
                AvailableCapacity = slot.AvailableCapacity,
                Status = slot.Status.ToString(),
                CreatedByUserId = slot.CreatedByUserId,
                UpdatedByUserId = slot.UpdatedByUserId,
                CreatedAtUtc = slot.CreatedAtUtc,
                UpdatedAtUtc = slot.UpdatedAtUtc,
                ClosedAtUtc = slot.ClosedAtUtc
            };
        }
    }
}
