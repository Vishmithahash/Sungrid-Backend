// File name: ReservationStatus.cs
// Project name: SunGrid
// Purpose of the file: Defines the lifecycle states of an energy booking reservation.
// Author placeholder: SunGrid Development Team

namespace SunGrid.Api.Enums
{
    /// <summary>
    /// Lifecycle states of an energy booking reservation.
    /// </summary>
    public enum ReservationStatus
    {
        Pending,
        Approved,
        Rejected,
        Cancelled,
        Completed
    }
}
