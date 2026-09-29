// File name: IQrTransactionService.cs
// Project name: SunGrid
// Purpose of the file: Interface defining Phase 4 QR code token generation, status queries, verification, and completion operations.
// Author placeholder: SunGrid Development Team

using SunGrid.Api.DTOs;

namespace SunGrid.Api.Services
{
    /// <summary>
    /// Service contract for handling secure QR token generation, status tracking, verification, and idempotent energy transfer completion.
    /// </summary>
    public interface IQrTransactionService
    {
        /// <summary>
        /// Generates a secure random QR token payload for an Approved reservation owned by the Prosumer.
        /// </summary>
        Task<GenerateQrResponse> GenerateQrAsync(string reservationId, string prosumerUserId);

        /// <summary>
        /// Retrieves safe status metadata about a reservation's QR token without exposing raw payload or hash.
        /// </summary>
        Task<QrStatusResponse> GetQrStatusAsync(string reservationId, string prosumerUserId);

        /// <summary>
        /// Verifies a scanned QR payload against current MongoDB state and calculates transfer window eligibility.
        /// </summary>
        Task<VerifyQrResponse> VerifyQrAsync(VerifyQrRequest request);

        /// <summary>
        /// Idempotently completes an energy transfer for an Approved reservation using scanned QR payload.
        /// </summary>
        Task<CompleteEnergyTransferResponse> CompleteEnergyTransferAsync(CompleteEnergyTransferRequest request, string gridOperatorUserId);
    }
}
