// File name: HealthController.cs
// Project name: SunGrid
// Purpose of the file: Public API endpoints to verify application and MongoDB database connectivity health status.
// Author placeholder: SunGrid Development Team

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SunGrid.Api.Data;
using SunGrid.Api.DTOs;

namespace SunGrid.Api.Controllers
{
    /// <summary>
    /// Controller serving basic system and database connectivity health checks.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class HealthController : ControllerBase
    {
        private readonly MongoDbContext _context;

        /// <summary>
        /// Initializes HealthController with MongoDbContext dependency.
        /// </summary>
        public HealthController(MongoDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Returns general API health status, application name, and UTC timestamp.
        /// </summary>
        /// <response code="200">API is healthy and operational.</response>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
        public IActionResult GetHealth()
        {
            var response = new HealthResponse
            {
                Status = "Healthy",
                Message = "API is running",
                Application = "SunGrid",
                TimestampUtc = DateTime.UtcNow
            };

            return Ok(response);
        }

        /// <summary>
        /// Executes a MongoDB ping command to verify live database connectivity.
        /// </summary>
        /// <response code="200">MongoDB database is reachable and healthy.</response>
        /// <response code="503">MongoDB database connection is unreachable or offline.</response>
        [HttpGet("database")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(DatabaseHealthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(DatabaseHealthResponse), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetDatabaseHealth()
        {
            var isDbConnected = await _context.PingDatabaseAsync();

            var response = new DatabaseHealthResponse
            {
                Application = "SunGrid",
                ApiStatus = "Healthy",
                DatabaseStatus = isDbConnected ? "Healthy" : "Unhealthy",
                TimestampUtc = DateTime.UtcNow
            };

            if (!isDbConnected)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
            }

            return Ok(response);
        }
    }
}
