// File name: AuthController.cs
// Project name: SunGrid
// Purpose of the file: API endpoints handling Prosumer self-registration and user login.
// Author placeholder: SunGrid Development Team

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SunGrid.Api.DTOs;
using SunGrid.Api.Services;

namespace SunGrid.Api.Controllers
{
    /// <summary>
    /// Controller handling public authentication operations.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        /// <summary>
        /// Initializes AuthController with injected AuthService dependency.
        /// </summary>
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Registers a new Prosumer account with Pending status.
        /// </summary>
        [HttpPost("register/prosumer")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RegisterProsumer([FromBody] RegisterProsumerRequest request)
        {
            var user = await _authService.RegisterProsumerAsync(request);
            return CreatedAtAction("GetUserById", "Users", new { id = user.Id }, user);
        }

        /// <summary>
        /// Authenticates user credentials and returns JWT token for Active accounts.
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }
    }
}
