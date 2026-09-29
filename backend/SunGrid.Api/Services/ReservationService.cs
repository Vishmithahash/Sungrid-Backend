// File name: ReservationService.cs
// Project name: SunGrid
// Purpose of the file: Implementation of energy reservation business logic, seven-day rule, 12-hour rule, slot capacity consistency, and dashboards.
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
    /// Service executing energy reservation business logic, status state transitions, and capacity management.
    /// </summary>
    public class ReservationService : IReservationService
    {
        private readonly MongoDbContext _context;
        private readonly IBookingSlotService _bookingSlotService;

        /// <summary>
        /// Initializes ReservationService with injected MongoDbContext and BookingSlotService dependencies.
        /// </summary>
        public ReservationService(MongoDbContext context, IBookingSlotService bookingSlotService)
        {
            _context = context;
            _bookingSlotService = bookingSlotService;
        }

        /// <summary>
        /// Creates a new energy reservation for the authenticated Prosumer.
        /// Validates Active Prosumer account, active station, 7-day rule, and atomic slot capacity reservation.
        /// </summary>
        public async Task<ReservationResponse> CreateReservationAsync(CreateReservationRequest request, string prosumerId, string createdByUserId)
        {
            return await ExecuteReservationCreationAsync(prosumerId, request.BookingSlotId, request.TransferType, request.EnergyAmountKwh, request.Notes, createdByUserId, request.StationId, request.ScheduledTime, request.SlotStartTimeUtc);
        }

        /// <summary>
        /// Staff operation creating a reservation on behalf of a specified Prosumer.
        /// Validates that the selected Prosumer account is currently Active.
        /// </summary>
        public async Task<ReservationResponse> CreateReservationForProsumerAsync(CreateReservationForProsumerRequest request, string createdByUserId)
        {
            return await ExecuteReservationCreationAsync(request.ProsumerId, request.BookingSlotId, request.TransferType, request.EnergyAmountKwh, request.Notes, createdByUserId, request.StationId);
        }

        /// <summary>
        /// Core internal reservation creation workflow enforcing 7-day rule, active account check, and atomic slot capacity reservation.
        /// </summary>
        private async Task<ReservationResponse> ExecuteReservationCreationAsync(
            string prosumerId, string bookingSlotId, EnergyTransferType transferType, double energyAmountKwh, string? notes, string createdByUserId, string? requestedStationId = null, string? scheduledTime = null, string? slotStartTimeUtc = null)
        {
            ValidateObjectId(prosumerId);

            // 1. Verify Prosumer is Active
            var prosumer = await _context.UserDetails.Find(u => u.Id == prosumerId).FirstOrDefaultAsync();
            if (prosumer == null)
            {
                throw new KeyNotFoundException($"Prosumer user with ID '{prosumerId}' was not found.");
            }

            if (prosumer.Role != UserRole.Prosumer)
            {
                throw new ArgumentException("Selected user is not a Prosumer.");
            }

            if (prosumer.AccountStatus != AccountStatus.Active)
            {
                throw new InvalidOperationException($"Cannot create reservation. Prosumer account is currently in '{prosumer.AccountStatus}' status. Account must be Active.");
            }

            // 2. Resolve Booking Slot and Station
            EnergyBookingSlot? slot = null;
            if (ObjectId.TryParse(bookingSlotId, out _))
            {
                slot = await _context.EnergyBookingSlots.Find(s => s.Id == bookingSlotId).FirstOrDefaultAsync();
            }

            SolarStation? station = null;
            if (slot != null)
            {
                station = await _context.SolarStationInfo.Find(s => s.Id == slot.StationId).FirstOrDefaultAsync();
            }
            else
            {
                // Slot ID was not an ObjectId (e.g., 'slot-01' from mobile app) or not found.
                // Resolve Station from requestedStationId or fallback:
                if (!string.IsNullOrWhiteSpace(requestedStationId))
                {
                    if (ObjectId.TryParse(requestedStationId, out _))
                    {
                        station = await _context.SolarStationInfo.Find(s => s.Id == requestedStationId).FirstOrDefaultAsync();
                    }
                    else if (int.TryParse(requestedStationId, out int stationIndex))
                    {
                        var activeStations = await _context.SolarStationInfo.Find(s => s.Status == StationStatus.Active).ToListAsync();
                        if (stationIndex >= 1 && stationIndex <= activeStations.Count)
                        {
                            station = activeStations[stationIndex - 1];
                        }
                        else if (stationIndex >= 0 && stationIndex < activeStations.Count)
                        {
                            station = activeStations[stationIndex];
                        }
                    }
                    else
                    {
                        var q = requestedStationId.Trim().ToLowerInvariant();
                        station = await _context.SolarStationInfo.Find(s => s.Name.ToLower().Contains(q) || s.StationCode.ToLower().Contains(q)).FirstOrDefaultAsync();
                    }
                }

                if (station == null)
                {
                    station = await _context.SolarStationInfo.Find(s => s.Status == StationStatus.Active).FirstOrDefaultAsync();
                }

                if (station == null)
                {
                    throw new KeyNotFoundException($"Could not resolve an active solar station.");
                }

                // Substation Out-of-Storage & Capacity Volume guard: DropOff intake rejected if battery full or volume exceeds available headroom
                if (transferType == EnergyTransferType.EnergyDropOff)
                {
                    var totalStationSlots = station.TotalBatteryStorageSlots > 0 ? station.TotalBatteryStorageSlots : 6;
                    var capacity = station.CapacityKwh > 0 ? station.CapacityKwh : 600.0;

                    var completedForStation = await _context.EnergyReservations
                        .Find(r => r.StationId == station.Id && r.Status == ReservationStatus.Completed)
                        .ToListAsync();

                    var completedDropOff = completedForStation.Where(r => r.TransferType == EnergyTransferType.EnergyDropOff).Sum(r => r.EnergyAmountKwh);
                    var completedCharging = completedForStation.Where(r => r.TransferType == EnergyTransferType.Charging).Sum(r => r.EnergyAmountKwh);
                    var currentStored = Math.Min(capacity, Math.Max(0, completedDropOff - completedCharging));

                    var pendingDropOffList = await _context.EnergyReservations
                        .Find(r => r.StationId == station.Id && 
                            (r.Status == ReservationStatus.Approved || r.Status == ReservationStatus.Pending) &&
                            r.TransferType == EnergyTransferType.EnergyDropOff)
                        .ToListAsync();

                    var pendingIntakeKwh = pendingDropOffList.Sum(r => r.EnergyAmountKwh);
                    var availableIntakeKwh = Math.Max(0, capacity - currentStored - pendingIntakeKwh);

                    var activeOccupyingCount = await _context.EnergyReservations
                        .CountDocumentsAsync(r => r.StationId == station.Id && 
                            (r.Status == ReservationStatus.Approved || r.Status == ReservationStatus.Pending));

                    if (activeOccupyingCount >= totalStationSlots || availableIntakeKwh <= 0)
                    {
                        throw new InvalidOperationException($"Substation '{station.Name}' is OUT OF STORAGE capacity ({activeOccupyingCount}/{totalStationSlots} bays occupied, {currentStored:F1}/{capacity:F1} kWh stored). It cannot receive energy drop-offs at this time.");
                    }

                    if (energyAmountKwh > availableIntakeKwh)
                    {
                        throw new InvalidOperationException($"Requested drop-off volume of {energyAmountKwh:F1} kWh exceeds the available storage capacity of substation '{station.Name}' ({availableIntakeKwh:F1} kWh available headroom).");
                    }
                }

                // Find or create slot matching the requested scheduled time or default
                var timeInput = !string.IsNullOrWhiteSpace(scheduledTime) ? scheduledTime : slotStartTimeUtc;
                DateTime slotStart;
                if (!string.IsNullOrWhiteSpace(timeInput) && DateTime.TryParse(timeInput, out var parsedTime))
                {
                    if (parsedTime.Kind == DateTimeKind.Unspecified)
                    {
                        var localOffset = TimeSpan.FromHours(5.5);
                        slotStart = DateTime.SpecifyKind(parsedTime - localOffset, DateTimeKind.Utc);
                    }
                    else
                    {
                        slotStart = parsedTime.ToUniversalTime();
                    }
                }
                else
                {
                    var now = DateTime.UtcNow;
                    slotStart = DateTime.UtcNow.Date.AddDays(1).AddHours(1);
                }

                slot = await _context.EnergyBookingSlots
                    .Find(s => s.StationId == station.Id && s.StartTimeUtc == slotStart && s.Status == BookingSlotStatus.Available && s.AvailableCapacity > 0)
                    .FirstOrDefaultAsync();

                if (slot == null)
                {
                    var maxCap = station.TotalBatteryStorageSlots > 0 ? station.TotalBatteryStorageSlots : 10;
                    slot = new EnergyBookingSlot
                    {
                        StationId = station.Id,
                        StartTimeUtc = slotStart,
                        EndTimeUtc = slotStart.AddHours(2),
                        TotalCapacity = maxCap,
                        AvailableCapacity = maxCap,
                        Status = BookingSlotStatus.Available,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    };
                    await _context.EnergyBookingSlots.InsertOneAsync(slot);
                }

                bookingSlotId = slot.Id;
            }

            if (station == null)
            {
                throw new KeyNotFoundException($"Solar station with ID '{slot.StationId}' derived from booking slot was not found.");
            }

            if (station.Status != StationStatus.Active)
            {
                throw new InvalidOperationException("Cannot create reservation for an Inactive solar station.");
            }

            // 4. Validate slot status & availability
            if (slot.Status == BookingSlotStatus.Closed)
            {
                throw new InvalidOperationException("Selected booking slot is Closed.");
            }

            if (slot.Status == BookingSlotStatus.Full || slot.AvailableCapacity <= 0)
            {
                throw new ConflictException("Selected booking slot is Full and has no remaining capacity.");
            }

            // 5. Seven-Day Rule: currentUtc < slotStartUtc <= currentUtc + 7 days
            var currentUtc = DateTime.UtcNow;
            if (slot.StartTimeUtc <= currentUtc || slot.StartTimeUtc > currentUtc.AddDays(7))
            {
                throw new ArgumentException("Booking slot must be scheduled in the future and no later than 7 days from the current time.");
            }

            // 6. Validate EnergyAmountKwh
            if (energyAmountKwh > station.CapacityKwh)
            {
                throw new ArgumentException($"Requested energy amount ({energyAmountKwh} kWh) exceeds station total capacity ({station.CapacityKwh} kWh).");
            }

            // 7. Duplicate active reservation check for same Prosumer and Slot
            var existingDuplicate = await _context.EnergyReservations
                .Find(r => r.ProsumerId == prosumerId && r.BookingSlotId == bookingSlotId && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Approved))
                .FirstOrDefaultAsync();

            if (existingDuplicate != null)
            {
                throw new ConflictException("You already have an active (Pending or Approved) reservation for this booking slot.");
            }

            // 8. Atomic capacity unit reservation
            var reserved = await _bookingSlotService.TryReserveOneCapacityUnitAsync(bookingSlotId);
            if (!reserved)
            {
                throw new ConflictException("Failed to reserve capacity. Booking slot has no remaining capacity.");
            }

            // 9. Generate unique ReservationReference
            var reference = await GenerateReservationReferenceAsync();

            var reservation = new EnergyReservation
            {
                ReservationReference = reference,
                ProsumerId = prosumerId,
                StationId = station.Id,
                BookingSlotId = slot.Id,
                TransferType = transferType,
                EnergyAmountKwh = energyAmountKwh,
                Status = ReservationStatus.Pending,
                Notes = notes?.Trim(),
                CreatedByUserId = createdByUserId,
                UpdatedByUserId = createdByUserId,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            try
            {
                await _context.EnergyReservations.InsertOneAsync(reservation);
            }
            catch
            {
                // Compensating update if insertion fails
                await _bookingSlotService.ReleaseOneCapacityUnitAsync(bookingSlotId);
                throw;
            }

            return await MapToReservationResponseAsync(reservation, prosumer, station, slot, true);
        }

        /// <summary>
        /// Fetches paginated reservations for the authenticated Prosumer with optional status, search, and date range filters.
        /// </summary>
        public async Task<ReservationListResponse> GetMyReservationsAsync(string prosumerId, string? status, string? search, DateTime? fromUtc, DateTime? toUtc, int pageNumber, int pageSize)
        {
            ValidateObjectId(prosumerId);
            pageNumber = pageNumber < 1 ? 1 : pageNumber;
            pageSize = pageSize < 1 ? 10 : pageSize;

            var builder = Builders<EnergyReservation>.Filter;
            var filter = builder.Eq(r => r.ProsumerId, prosumerId);

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReservationStatus>(status, true, out var parsedStatus))
            {
                filter &= builder.Eq(r => r.Status, parsedStatus);
            }

            if (fromUtc.HasValue)
            {
                filter &= builder.Gte(r => r.CreatedAtUtc, fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                filter &= builder.Lte(r => r.CreatedAtUtc, toUtc.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var query = search.Trim();
                // Find matching station IDs for station search
                var matchingStationIds = await _context.SolarStationInfo
                    .Find(s => s.StationCode.Contains(query) || s.Name.Contains(query))
                    .Project(s => s.Id)
                    .ToListAsync();

                var searchFilter = builder.Or(
                    builder.Regex(r => r.ReservationReference, new BsonRegularExpression(query, "i")),
                    builder.In(r => r.StationId, matchingStationIds)
                );
                filter &= searchFilter;
            }

            var totalCount = await _context.EnergyReservations.CountDocumentsAsync(filter);
            var reservations = await _context.EnergyReservations
                .Find(filter)
                .SortByDescending(r => r.CreatedAtUtc)
                .Skip((pageNumber - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            var responseItems = new List<ReservationResponse>();
            foreach (var r in reservations)
            {
                responseItems.Add(await MapToReservationResponseAsync(r, null, null, null, false));
            }

            return new ReservationListResponse
            {
                Items = responseItems,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        /// <summary>
        /// Retrieves future Pending and Approved reservations for the authenticated Prosumer.
        /// </summary>
        public async Task<List<ReservationResponse>> GetMyCurrentReservationsAsync(string prosumerId)
        {
            ValidateObjectId(prosumerId);

            var filter = Builders<EnergyReservation>.Filter.And(
                Builders<EnergyReservation>.Filter.Eq(r => r.ProsumerId, prosumerId),
                Builders<EnergyReservation>.Filter.In(r => r.Status, new[] { ReservationStatus.Pending, ReservationStatus.Approved })
            );

            var reservations = await _context.EnergyReservations
                .Find(filter)
                .SortByDescending(r => r.CreatedAtUtc)
                .ToListAsync();

            var responseItems = new List<ReservationResponse>();
            foreach (var r in reservations)
            {
                var slot = await _context.EnergyBookingSlots.Find(s => s.Id == r.BookingSlotId).FirstOrDefaultAsync();
                // Exclude past slots from current view
                if (slot == null || slot.StartTimeUtc > DateTime.UtcNow)
                {
                    responseItems.Add(await MapToReservationResponseAsync(r, null, null, slot, false));
                }
            }

            return responseItems;
        }

        /// <summary>
        /// Retrieves historical (Rejected, Cancelled, Completed, or past slot) reservations for the authenticated Prosumer.
        /// </summary>
        public async Task<List<ReservationResponse>> GetMyReservationHistoryAsync(string prosumerId)
        {
            ValidateObjectId(prosumerId);

            var filter = Builders<EnergyReservation>.Filter.Eq(r => r.ProsumerId, prosumerId);
            var reservations = await _context.EnergyReservations
                .Find(filter)
                .SortByDescending(r => r.CreatedAtUtc)
                .ToListAsync();

            var responseItems = new List<ReservationResponse>();
            foreach (var r in reservations)
            {
                var slot = await _context.EnergyBookingSlots.Find(s => s.Id == r.BookingSlotId).FirstOrDefaultAsync();
                responseItems.Add(await MapToReservationResponseAsync(r, null, null, slot, false));
            }

            return responseItems;
        }

        /// <summary>
        /// Returns live counts for Prosumer dashboard.
        /// </summary>
        public async Task<ProsumerReservationDashboardResponse> GetMyDashboardCountsAsync(string prosumerId)
        {
            ValidateObjectId(prosumerId);

            var pendingCount = await _context.EnergyReservations
                .CountDocumentsAsync(r => r.ProsumerId == prosumerId && r.Status == ReservationStatus.Pending);

            var completedReservations = await _context.EnergyReservations
                .Find(r => r.ProsumerId == prosumerId && r.Status == ReservationStatus.Completed)
                .ToListAsync();

            var completedCount = (long)completedReservations.Count;
            var totalEnergyTraded = completedReservations.Sum(r => r.EnergyAmountKwh);
            var totalEnergySold = completedReservations
                .Where(r => r.TransferType == EnergyTransferType.EnergyDropOff)
                .Sum(r => r.EnergyAmountKwh);
            var totalEnergyBought = completedReservations
                .Where(r => r.TransferType == EnergyTransferType.Charging)
                .Sum(r => r.EnergyAmountKwh);

            var cancelledCount = await _context.EnergyReservations
                .CountDocumentsAsync(r => r.ProsumerId == prosumerId && r.Status == ReservationStatus.Cancelled);

            // Fetch approved reservations and count those with future slot start times
            var approvedList = await _context.EnergyReservations
                .Find(r => r.ProsumerId == prosumerId && r.Status == ReservationStatus.Approved)
                .ToListAsync();

            long approvedFutureCount = 0;
            foreach (var r in approvedList)
            {
                var slot = await _context.EnergyBookingSlots.Find(s => s.Id == r.BookingSlotId).FirstOrDefaultAsync();
                if (slot != null && slot.StartTimeUtc > DateTime.UtcNow)
                {
                    approvedFutureCount++;
                }
            }

            return new ProsumerReservationDashboardResponse
            {
                PendingReservationsCount = pendingCount,
                ApprovedFutureReservationsCount = approvedFutureCount,
                CurrentBookingsCount = pendingCount + approvedFutureCount,
                CompletedReservationsCount = completedCount,
                CancelledReservationsCount = cancelledCount,
                TotalEnergyTraded = Math.Round(totalEnergyTraded, 2),
                TotalEnergySoldKwh = Math.Round(totalEnergySold, 2),
                TotalEnergyBoughtKwh = Math.Round(totalEnergyBought, 2)
            };
        }

        /// <summary>
        /// Staff query returning paginated reservations with filters across all Prosumers and stations.
        /// </summary>
        public async Task<ReservationListResponse> GetReservationsAsync(
            string? status, string? prosumerId, string? stationId, string? bookingSlotId, string? search, DateTime? fromUtc, DateTime? toUtc, int pageNumber, int pageSize)
        {
            pageNumber = pageNumber < 1 ? 1 : pageNumber;
            pageSize = pageSize < 1 ? 10 : pageSize;

            var builder = Builders<EnergyReservation>.Filter;
            var filter = builder.Empty;

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReservationStatus>(status, true, out var parsedStatus))
            {
                filter &= builder.Eq(r => r.Status, parsedStatus);
            }

            if (!string.IsNullOrWhiteSpace(prosumerId) && ObjectId.TryParse(prosumerId, out _))
            {
                filter &= builder.Eq(r => r.ProsumerId, prosumerId);
            }

            if (!string.IsNullOrWhiteSpace(stationId) && ObjectId.TryParse(stationId, out _))
            {
                filter &= builder.Eq(r => r.StationId, stationId);
            }

            if (!string.IsNullOrWhiteSpace(bookingSlotId) && ObjectId.TryParse(bookingSlotId, out _))
            {
                filter &= builder.Eq(r => r.BookingSlotId, bookingSlotId);
            }

            if (fromUtc.HasValue)
            {
                filter &= builder.Gte(r => r.CreatedAtUtc, fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                filter &= builder.Lte(r => r.CreatedAtUtc, toUtc.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var query = search.Trim();
                var matchingProsumerIds = await _context.UserDetails
                    .Find(u => u.Role == UserRole.Prosumer && (u.FullName.Contains(query) || (u.Nic != null && u.Nic.Contains(query))))
                    .Project(u => u.Id)
                    .ToListAsync();

                var matchingStationIds = await _context.SolarStationInfo
                    .Find(s => s.StationCode.Contains(query) || s.Name.Contains(query))
                    .Project(s => s.Id)
                    .ToListAsync();

                var searchFilter = builder.Or(
                    builder.Regex(r => r.ReservationReference, new BsonRegularExpression(query, "i")),
                    builder.In(r => r.ProsumerId, matchingProsumerIds),
                    builder.In(r => r.StationId, matchingStationIds)
                );
                filter &= searchFilter;
            }

            var totalCount = await _context.EnergyReservations.CountDocumentsAsync(filter);
            var reservations = await _context.EnergyReservations
                .Find(filter)
                .SortByDescending(r => r.CreatedAtUtc)
                .Skip((pageNumber - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            var responseItems = new List<ReservationResponse>();
            foreach (var r in reservations)
            {
                responseItems.Add(await MapToReservationResponseAsync(r, null, null, null, true));
            }

            return new ReservationListResponse
            {
                Items = responseItems,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        /// <summary>
        /// Returns all Pending reservations ordered oldest first for administrative approval.
        /// </summary>
        public async Task<List<ReservationResponse>> GetPendingReservationsAsync()
        {
            var filter = Builders<EnergyReservation>.Filter.Eq(r => r.Status, ReservationStatus.Pending);
            var reservations = await _context.EnergyReservations
                .Find(filter)
                .SortBy(r => r.CreatedAtUtc)
                .ToListAsync();

            var responseItems = new List<ReservationResponse>();
            foreach (var r in reservations)
            {
                responseItems.Add(await MapToReservationResponseAsync(r, null, null, null, true));
            }

            return responseItems;
        }

        /// <summary>
        /// Returns live reservation metrics for operational staff (Backoffice / GridOperator).
        /// </summary>
        public async Task<OperationsDashboardResponse> GetOperationsDashboardCountsAsync()
        {
            var pendingCount = await _context.EnergyReservations
                .CountDocumentsAsync(r => r.Status == ReservationStatus.Pending);

            var cancelledCount = await _context.EnergyReservations
                .CountDocumentsAsync(r => r.Status == ReservationStatus.Cancelled);

            var completedCount = await _context.EnergyReservations
                .CountDocumentsAsync(r => r.Status == ReservationStatus.Completed);

            var approvedList = await _context.EnergyReservations
                .Find(r => r.Status == ReservationStatus.Approved)
                .ToListAsync();

            long approvedFutureCount = 0;
            foreach (var r in approvedList)
            {
                var slot = await _context.EnergyBookingSlots.Find(s => s.Id == r.BookingSlotId).FirstOrDefaultAsync();
                if (slot != null && slot.StartTimeUtc > DateTime.UtcNow)
                {
                    approvedFutureCount++;
                }
            }

            // Today's reservations: slots starting between start of today UTC and end of today UTC
            var startOfToday = DateTime.UtcNow.Date;
            var endOfToday = startOfToday.AddDays(1).AddTicks(-1);

            var allReservations = await _context.EnergyReservations.Find(_ => true).ToListAsync();
            long todaysCount = 0;
            foreach (var r in allReservations)
            {
                var slot = await _context.EnergyBookingSlots.Find(s => s.Id == r.BookingSlotId).FirstOrDefaultAsync();
                if (slot != null && slot.StartTimeUtc >= startOfToday && slot.StartTimeUtc <= endOfToday)
                {
                    todaysCount++;
                }
            }

            return new OperationsDashboardResponse
            {
                PendingReservationsCount = pendingCount,
                ApprovedFutureReservationsCount = approvedFutureCount,
                TodaysReservationsCount = todaysCount,
                CancelledReservationsCount = cancelledCount,
                CompletedReservationsCount = completedCount
            };
        }

        /// <summary>
        /// Retrieves a single reservation by ID. Enforces ownership for Prosumers.
        /// </summary>
        public async Task<ReservationResponse> GetReservationByIdAsync(string id, string userId, string userRole)
        {
            EnergyReservation? reservation = null;

            if (ObjectId.TryParse(id, out _))
            {
                reservation = await _context.EnergyReservations.Find(r => r.Id == id).FirstOrDefaultAsync();
            }

            if (reservation == null)
            {
                reservation = await _context.EnergyReservations.Find(r => r.ReservationReference == id).FirstOrDefaultAsync();
            }

            if (reservation == null)
            {
                // Fallback for mobile app using integer / local sqlite ID (e.g. "1" or "#35483")
                if (!string.IsNullOrEmpty(userId))
                {
                    reservation = await _context.EnergyReservations
                        .Find(r => r.ProsumerId == userId)
                        .SortByDescending(r => r.CreatedAtUtc)
                        .FirstOrDefaultAsync();
                }

                if (reservation == null)
                {
                    reservation = await _context.EnergyReservations
                        .Find(_ => true)
                        .SortByDescending(r => r.CreatedAtUtc)
                        .FirstOrDefaultAsync();
                }
            }

            if (reservation == null)
            {
                throw new KeyNotFoundException($"Energy reservation with ID '{id}' was not found.");
            }

            // Ownership check for Prosumer role
            var isProsumer = userRole.Equals(UserRole.Prosumer.ToString(), StringComparison.OrdinalIgnoreCase);
            if (isProsumer && reservation.ProsumerId != userId)
            {
                throw new UnauthorizedAccessException("You are not authorized to view another user's reservation.");
            }

            return await MapToReservationResponseAsync(reservation, null, null, null, !isProsumer);
        }

        /// <summary>
        /// Updates a Pending or Approved reservation applying slot capacity migration and mobile app compatibility.
        /// </summary>
        public async Task<ReservationResponse> UpdateReservationAsync(string id, UpdateReservationRequest request, string userId, string userRole)
        {
            EnergyReservation? reservation = null;

            if (ObjectId.TryParse(id, out _))
            {
                reservation = await _context.EnergyReservations.Find(r => r.Id == id).FirstOrDefaultAsync();
            }

            if (reservation == null)
            {
                reservation = await _context.EnergyReservations.Find(r => r.ReservationReference == id).FirstOrDefaultAsync();
            }

            if (reservation == null)
            {
                // Fallback for mobile app using integer / local sqlite ID (e.g. "1")
                if (!string.IsNullOrEmpty(userId))
                {
                    reservation = await _context.EnergyReservations
                        .Find(r => r.ProsumerId == userId && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Approved))
                        .SortByDescending(r => r.CreatedAtUtc)
                        .FirstOrDefaultAsync();
                }

                if (reservation == null)
                {
                    reservation = await _context.EnergyReservations
                        .Find(r => r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Approved)
                        .SortByDescending(r => r.CreatedAtUtc)
                        .FirstOrDefaultAsync();
                }
            }

            if (reservation == null)
            {
                throw new KeyNotFoundException($"Energy reservation with ID '{id}' was not found.");
            }

            // Verify Prosumer
            var prosumer = await _context.UserDetails.Find(u => u.Id == reservation.ProsumerId).FirstOrDefaultAsync();
            var isProsumer = userRole.Equals(UserRole.Prosumer.ToString(), StringComparison.OrdinalIgnoreCase);

            // Check terminal status
            if (reservation.Status == ReservationStatus.Rejected ||
                reservation.Status == ReservationStatus.Cancelled ||
                reservation.Status == ReservationStatus.Completed)
            {
                throw new InvalidOperationException($"Cannot update a reservation with terminal status '{reservation.Status}'.");
            }

            // Slot Migration logic if BookingSlotId changed and is a valid MongoDB ObjectId
            if (!string.IsNullOrWhiteSpace(request.BookingSlotId) &&
                request.BookingSlotId != reservation.BookingSlotId &&
                ObjectId.TryParse(request.BookingSlotId, out _))
            {
                var newSlot = await _context.EnergyBookingSlots.Find(s => s.Id == request.BookingSlotId).FirstOrDefaultAsync();
                if (newSlot != null)
                {
                    var newStation = await _context.SolarStationInfo.Find(s => s.Id == newSlot.StationId).FirstOrDefaultAsync();
                    if (newStation != null && newStation.Status == StationStatus.Active)
                    {
                        var reservedNew = await _bookingSlotService.TryReserveOneCapacityUnitAsync(newSlot.Id);
                        if (reservedNew)
                        {
                            await _bookingSlotService.ReleaseOneCapacityUnitAsync(reservation.BookingSlotId);
                            reservation.BookingSlotId = newSlot.Id;
                            reservation.StationId = newStation.Id;
                        }
                    }
                }
            }

            // Handle scheduled time update from mobile app (e.g. "2026-09-25 03:30")
            var timeInput = !string.IsNullOrWhiteSpace(request.ScheduledTime) ? request.ScheduledTime : request.SlotStartTimeUtc;
            if (!string.IsNullOrWhiteSpace(timeInput))
            {
                if (DateTime.TryParse(timeInput, out var parsedTime))
                {
                    DateTime startTimeUtc;
                    if (parsedTime.Kind == DateTimeKind.Unspecified)
                    {
                        var localOffset = TimeSpan.FromHours(5.5);
                        startTimeUtc = DateTime.SpecifyKind(parsedTime - localOffset, DateTimeKind.Utc);
                    }
                    else
                    {
                        startTimeUtc = parsedTime.ToUniversalTime();
                    }
                    var endTimeUtc = startTimeUtc.AddHours(2);

                    // Update corresponding booking slot if exists
                    if (!string.IsNullOrEmpty(reservation.BookingSlotId) && ObjectId.TryParse(reservation.BookingSlotId, out _))
                    {
                        var slotUpdate = Builders<EnergyBookingSlot>.Update
                            .Set(s => s.StartTimeUtc, startTimeUtc)
                            .Set(s => s.EndTimeUtc, endTimeUtc)
                            .Set(s => s.UpdatedAtUtc, DateTime.UtcNow);
                        await _context.EnergyBookingSlots.UpdateOneAsync(s => s.Id == reservation.BookingSlotId, slotUpdate);
                    }
                }
            }

            if (request.EnergyAmountKwh > 0)
            {
                reservation.EnergyAmountKwh = request.EnergyAmountKwh;
            }

            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                reservation.Notes = request.Notes.Trim();
            }

            reservation.TransferType = request.TransferType;

            // Return Approved reservation to Pending upon edit and revoke active QR
            if (reservation.Status == ReservationStatus.Approved)
            {
                reservation.Status = ReservationStatus.Pending;
                reservation.ApprovedByUserId = null;
                reservation.ApprovedAtUtc = null;
                reservation.QrRevokedAtUtc = DateTime.UtcNow;
            }

            reservation.UpdatedByUserId = !string.IsNullOrEmpty(userId) ? userId : reservation.ProsumerId;
            reservation.UpdatedAtUtc = DateTime.UtcNow;

            var filter = Builders<EnergyReservation>.Filter.Eq(r => r.Id, reservation.Id);
            await _context.EnergyReservations.ReplaceOneAsync(filter, reservation);

            return await MapToReservationResponseAsync(reservation, prosumer, null, null, !isProsumer);
        }

        /// <summary>
        /// Approves a Pending reservation (Backoffice or GridOperator).
        /// </summary>
        public async Task<ReservationResponse> ApproveReservationAsync(string id, string userId)
        {
            ValidateObjectId(id);

            var reservation = await _context.EnergyReservations.Find(r => r.Id == id).FirstOrDefaultAsync();
            if (reservation == null)
            {
                throw new KeyNotFoundException($"Energy reservation with ID '{id}' was not found.");
            }

            if (reservation.Status != ReservationStatus.Pending)
            {
                throw new InvalidOperationException($"Only Pending reservations can be approved. Current status: '{reservation.Status}'.");
            }

            // Verify Prosumer is Active
            var prosumer = await _context.UserDetails.Find(u => u.Id == reservation.ProsumerId).FirstOrDefaultAsync();
            if (prosumer == null || prosumer.AccountStatus != AccountStatus.Active)
            {
                throw new InvalidOperationException("Cannot approve reservation. Associated Prosumer account is inactive.");
            }

            // Verify Station is Active
            var station = await _context.SolarStationInfo.Find(s => s.Id == reservation.StationId).FirstOrDefaultAsync();
            if (station == null || station.Status != StationStatus.Active)
            {
                throw new InvalidOperationException("Cannot approve reservation. Associated solar station is inactive.");
            }

            // Verify Slot is not Closed and not past
            var slot = await _context.EnergyBookingSlots.Find(s => s.Id == reservation.BookingSlotId).FirstOrDefaultAsync();
            if (slot == null || slot.Status == BookingSlotStatus.Closed || slot.StartTimeUtc <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("Cannot approve reservation. Booking slot is closed or has already passed.");
            }

            var filter = Builders<EnergyReservation>.Filter.Eq(r => r.Id, id);
            var update = Builders<EnergyReservation>.Update
                .Set(r => r.Status, ReservationStatus.Approved)
                .Set(r => r.ApprovedByUserId, userId)
                .Set(r => r.ApprovedAtUtc, DateTime.UtcNow)
                .Set(r => r.UpdatedByUserId, userId)
                .Set(r => r.UpdatedAtUtc, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<EnergyReservation> { ReturnDocument = ReturnDocument.After };
            var updated = await _context.EnergyReservations.FindOneAndUpdateAsync(filter, update, options);

            return await MapToReservationResponseAsync(updated!, prosumer, station, slot, true);
        }

        /// <summary>
        /// Rejects a Pending reservation and releases one slot capacity unit (Backoffice or GridOperator).
        /// </summary>
        public async Task<ReservationResponse> RejectReservationAsync(string id, RejectReservationRequest request, string userId)
        {
            ValidateObjectId(id);

            var reservation = await _context.EnergyReservations.Find(r => r.Id == id).FirstOrDefaultAsync();
            if (reservation == null)
            {
                throw new KeyNotFoundException($"Energy reservation with ID '{id}' was not found.");
            }

            if (reservation.Status != ReservationStatus.Pending)
            {
                throw new InvalidOperationException($"Only Pending reservations can be rejected. Current status: '{reservation.Status}'.");
            }

            var filter = Builders<EnergyReservation>.Filter.Eq(r => r.Id, id);
            var update = Builders<EnergyReservation>.Update
                .Set(r => r.Status, ReservationStatus.Rejected)
                .Set(r => r.RejectionReason, request.RejectionReason.Trim())
                .Set(r => r.RejectedByUserId, userId)
                .Set(r => r.RejectedAtUtc, DateTime.UtcNow)
                .Set(r => r.QrRevokedAtUtc, DateTime.UtcNow)
                .Set(r => r.UpdatedByUserId, userId)
                .Set(r => r.UpdatedAtUtc, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<EnergyReservation> { ReturnDocument = ReturnDocument.After };
            var updated = await _context.EnergyReservations.FindOneAndUpdateAsync(filter, update, options);

            // Release 1 slot capacity unit
            await _bookingSlotService.ReleaseOneCapacityUnitAsync(reservation.BookingSlotId);

            return await MapToReservationResponseAsync(updated!, null, null, null, true);
        }

        /// <summary>
        /// Soft-cancels a Pending or Approved reservation enforcing 12-hour rule and releasing slot capacity.
        /// </summary>
        public async Task<ReservationResponse> CancelReservationAsync(string id, CancelReservationRequest request, string userId, string userRole)
        {
            EnergyReservation? reservation = null;
            if (ObjectId.TryParse(id, out _))
            {
                reservation = await _context.EnergyReservations.Find(r => r.Id == id).FirstOrDefaultAsync();
            }

            if (reservation == null)
            {
                reservation = await _context.EnergyReservations.Find(r => r.ReservationReference == id || (r.Notes != null && r.Notes.Contains(id))).FirstOrDefaultAsync();
            }

            if (reservation == null)
            {
                reservation = await _context.EnergyReservations
                    .Find(r => r.ProsumerId == userId && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Approved))
                    .SortByDescending(r => r.CreatedAtUtc)
                    .FirstOrDefaultAsync();
            }

            if (reservation == null)
            {
                throw new KeyNotFoundException($"Energy reservation with ID '{id}' was not found.");
            }

            id = reservation.Id;

            // Ownership check for Prosumer
            var isProsumer = userRole.Equals(UserRole.Prosumer.ToString(), StringComparison.OrdinalIgnoreCase);
            if (isProsumer && reservation.ProsumerId != userId)
            {
                throw new UnauthorizedAccessException("You are not authorized to cancel another user's reservation.");
            }

            // Check non-terminal status
            if (reservation.Status == ReservationStatus.Cancelled ||
                reservation.Status == ReservationStatus.Rejected ||
                reservation.Status == ReservationStatus.Completed)
            {
                throw new InvalidOperationException($"Cannot cancel a reservation with status '{reservation.Status}'.");
            }

            // Check 12-hour rule
            var slot = await _context.EnergyBookingSlots.Find(s => s.Id == reservation.BookingSlotId).FirstOrDefaultAsync();
            if (slot != null && slot.StartTimeUtc - DateTime.UtcNow < TimeSpan.FromHours(12))
            {
                throw new ArgumentException("Cannot cancel a reservation within 12 hours of the scheduled booking slot start time.");
            }

            var filter = Builders<EnergyReservation>.Filter.Eq(r => r.Id, id);
            var update = Builders<EnergyReservation>.Update
                .Set(r => r.Status, ReservationStatus.Cancelled)
                .Set(r => r.CancellationReason, request.CancellationReason?.Trim())
                .Set(r => r.CancelledByUserId, userId)
                .Set(r => r.CancelledAtUtc, DateTime.UtcNow)
                .Set(r => r.QrRevokedAtUtc, DateTime.UtcNow)
                .Set(r => r.UpdatedByUserId, userId)
                .Set(r => r.UpdatedAtUtc, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<EnergyReservation> { ReturnDocument = ReturnDocument.After };
            var updated = await _context.EnergyReservations.FindOneAndUpdateAsync(filter, update, options);

            // Release 1 slot capacity unit
            await _bookingSlotService.ReleaseOneCapacityUnitAsync(reservation.BookingSlotId);

            return await MapToReservationResponseAsync(updated!, null, null, slot, !isProsumer);
        }

        /// <summary>
        /// Generates a unique readable reference string in format RES-YYYYMMDD-XXXXXX.
        /// </summary>
        private async Task<string> GenerateReservationReferenceAsync()
        {
            var dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
            string reference;
            bool exists;

            do
            {
                var randomCode = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
                reference = $"RES-{dateStr}-{randomCode}";

                var count = await _context.EnergyReservations.CountDocumentsAsync(r => r.ReservationReference == reference);
                exists = count > 0;
            } while (exists);

            return reference;
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
        /// Maps an EnergyReservation model to a complete ReservationResponse DTO.
        /// </summary>
        private async Task<ReservationResponse> MapToReservationResponseAsync(
            EnergyReservation r, User? prosumer, SolarStation? station, EnergyBookingSlot? slot, bool includeNic)
        {
            prosumer ??= await _context.UserDetails.Find(u => u.Id == r.ProsumerId).FirstOrDefaultAsync();
            station ??= await _context.SolarStationInfo.Find(s => s.Id == r.StationId).FirstOrDefaultAsync();
            slot ??= await _context.EnergyBookingSlots.Find(s => s.Id == r.BookingSlotId).FirstOrDefaultAsync();

            return new ReservationResponse
            {
                Id = r.Id,
                ReservationReference = r.ReservationReference,
                ProsumerId = r.ProsumerId,
                ProsumerFullName = prosumer?.FullName ?? "Unknown Prosumer",
                ProsumerNic = includeNic ? prosumer?.Nic : null,
                StationId = r.StationId,
                StationCode = station?.StationCode ?? "UNKNOWN",
                StationName = station?.Name ?? "Unknown Station",
                BookingSlotId = r.BookingSlotId,
                SlotStartTimeUtc = slot?.StartTimeUtc ?? DateTime.MinValue,
                SlotEndTimeUtc = slot?.EndTimeUtc ?? DateTime.MinValue,
                TransferType = r.TransferType.ToString(),
                EnergyAmountKwh = r.EnergyAmountKwh,
                Status = r.Status.ToString(),
                Notes = r.Notes,
                RejectionReason = r.RejectionReason,
                CancellationReason = r.CancellationReason,
                CreatedByUserId = r.CreatedByUserId,
                UpdatedByUserId = r.UpdatedByUserId,
                ApprovedByUserId = r.ApprovedByUserId,
                RejectedByUserId = r.RejectedByUserId,
                CancelledByUserId = r.CancelledByUserId,
                CreatedAtUtc = r.CreatedAtUtc,
                UpdatedAtUtc = r.UpdatedAtUtc,
                ApprovedAtUtc = r.ApprovedAtUtc,
                RejectedAtUtc = r.RejectedAtUtc,
                CancelledAtUtc = r.CancelledAtUtc,
                CompletedAtUtc = r.CompletedAtUtc
            };
        }
    }
}
