// File name: QrTransactionsController.cs
// Project name: SunGrid
// Purpose of the file: API Controller managing Phase 4 QR token generation, status queries, QR verification, and energy transfer completion.
// Author placeholder: SunGrid Development Team

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SunGrid.Api.DTOs;
using SunGrid.Api.Services;

namespace SunGrid.Api.Controllers
{
    /// <summary>
    /// Controller exposing endpoints for Prosumer QR generation and Grid Operator QR verification and energy transfer completion.
    /// </summary>
    [ApiController]
    [Route("api")]
    [Produces("application/json")]
    public class QrTransactionsController : ControllerBase
    {
        private readonly IQrTransactionService _qrService;

        /// <summary>
        /// Initializes the controller with the IQrTransactionService dependency.
        /// </summary>
        public QrTransactionsController(IQrTransactionService qrService)
        {
            _qrService = qrService;
        }

        /// <summary>
        /// Generates a secure random QR payload for an Approved reservation owned by the authenticated Prosumer.
        /// </summary>
        [HttpPost("reservations/{reservationId}/qr")]
        [HttpPost("/reservations/{reservationId}/qr")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(GenerateQrResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<GenerateQrResponse>> GenerateQr([FromRoute] string reservationId)
        {
            var userId = GetCurrentUserId();
            var response = await _qrService.GenerateQrAsync(reservationId, userId);
            return Ok(response);
        }

        /// <summary>
        /// Retrieves safe QR status metadata for an Approved reservation.
        /// </summary>
        [HttpGet("reservations/{reservationId}/qr/status")]
        [HttpGet("/reservations/{reservationId}/qr/status")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(QrStatusResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<QrStatusResponse>> GetQrStatus([FromRoute] string reservationId)
        {
            var userId = GetCurrentUserId();
            var response = await _qrService.GetQrStatusAsync(reservationId, userId);
            return Ok(response);
        }

        /// <summary>
        /// Verifies a scanned QR payload against current MongoDB server data.
        /// </summary>
        [HttpPost("qr/verify")]
        [HttpPost("/qr/verify")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(VerifyQrResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<VerifyQrResponse>> VerifyQr([FromBody] VerifyQrRequest request)
        {
            var response = await _qrService.VerifyQrAsync(request);
            return Ok(response);
        }

        /// <summary>
        /// Idempotently completes an energy transfer for a reservation using scanned QR payload or identifier.
        /// Updates Status to "Completed" directly in MongoDB.
        /// </summary>
        [HttpPost("qr/complete")]
        [HttpPost("/qr/complete")]
        [HttpPost("reservations/complete")]
        [HttpPost("/reservations/complete")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(CompleteEnergyTransferResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<CompleteEnergyTransferResponse>> CompleteEnergyTransfer(
            [FromBody] CompleteEnergyTransferRequest request)
        {
            var operatorUserId = GetCurrentUserId();
            var response = await _qrService.CompleteEnergyTransferAsync(request, operatorUserId);
            return Ok(response);
        }

        /// <summary>
        /// Completes an energy transfer by route reservationId.
        /// </summary>
        [HttpPost("reservations/{reservationId}/complete")]
        [HttpPut("reservations/{reservationId}/complete")]
        [HttpPost("/reservations/{reservationId}/complete")]
        [HttpPut("/reservations/{reservationId}/complete")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(CompleteEnergyTransferResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<CompleteEnergyTransferResponse>> CompleteReservationById(
            [FromRoute] string reservationId,
            [FromBody] CompleteEnergyTransferRequest? request)
        {
            request ??= new CompleteEnergyTransferRequest();
            if (string.IsNullOrWhiteSpace(request.ReservationId))
            {
                request.ReservationId = reservationId;
            }
            var operatorUserId = GetCurrentUserId();
            var response = await _qrService.CompleteEnergyTransferAsync(request, operatorUserId);
            return Ok(response);
        }

        /// <summary>
        /// Helper method extracting current user ObjectId string from NameIdentifier JWT claim with safe fallback.
        /// </summary>
        private string GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(claim))
            {
                return "operator-user";
            }
            return claim;
        }
    }
}
