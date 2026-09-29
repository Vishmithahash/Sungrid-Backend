// File name: GlobalExceptionMiddleware.cs
// Project name: SunGrid
// Purpose of the file: Global ASP.NET Core exception handling middleware converting uncaught exceptions into standardized HTTP JSON responses.
// Author placeholder: SunGrid Development Team

using System.Net;
using System.Text.Json;

namespace SunGrid.Api.Middleware
{
    /// <summary>
    /// Custom exception class representing a 409 Conflict response.
    /// </summary>
    public class ConflictException : Exception
    {
        public ConflictException(string message) : base(message) { }
    }

    /// <summary>
    /// Middleware to handle exceptions globally across all API requests.
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IHostEnvironment _environment;

        /// <summary>
        /// Initializes the exception middleware with next delegate, logger, and host environment.
        /// </summary>
        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        /// <summary>
        /// Invokes the request context and catches unhandled exceptions.
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred during request processing.");
                await HandleExceptionAsync(context, ex, _environment.IsDevelopment());
            }
        }

        /// <summary>
        /// Maps exceptions to appropriate HTTP status codes and writes standardized JSON error response.
        /// </summary>
        private static Task HandleExceptionAsync(HttpContext context, Exception exception, bool isDevelopment)
        {
            context.Response.ContentType = "application/json";

            var statusCode = exception switch
            {
                ConflictException => HttpStatusCode.Conflict,                  // 409 Conflict
                UnauthorizedAccessException => HttpStatusCode.Unauthorized,    // 401 Unauthorized
                KeyNotFoundException => HttpStatusCode.NotFound,               // 404 Not Found
                ArgumentException => HttpStatusCode.BadRequest,               // 400 Bad Request
                InvalidOperationException => HttpStatusCode.BadRequest,       // 400 Bad Request
                BadHttpRequestException => HttpStatusCode.BadRequest,         // 400 Bad Request
                _ => HttpStatusCode.InternalServerError                       // 500 Internal Server Error
            };

            context.Response.StatusCode = (int)statusCode;

            string safeMessage;
            if (statusCode == HttpStatusCode.InternalServerError && !isDevelopment)
            {
                safeMessage = "An unexpected server error occurred. Please try again later.";
            }
            else
            {
                safeMessage = exception.Message;
            }

            var response = new
            {
                statusCode = (int)statusCode,
                message = safeMessage,
                timestampUtc = DateTime.UtcNow
            };

            var jsonResponse = JsonSerializer.Serialize(response);
            return context.Response.WriteAsync(jsonResponse);
        }
    }
}
