// File name: AccountStatus.cs
// Project name: SunGrid
// Purpose of the file: Defines the possible states of a user account.
// Author placeholder: SunGrid Development Team

namespace SunGrid.Api.Enums
{
    /// <summary>
    /// Account lifecycle statuses for SunGrid users.
    /// </summary>
    public enum AccountStatus
    {
        Pending,
        Active,
        DeactivationRequested,
        Deactivated,
        Rejected
    }
}
