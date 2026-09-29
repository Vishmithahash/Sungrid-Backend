// File name: Program.cs
// Project name: SunGrid
// Purpose of the file: Application entry point configuring dependency injection, startup validation, middleware pipeline, JWT auth, Swagger, and database seeding.
// Author placeholder: SunGrid Development Team

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SunGrid.Api.Data;
using SunGrid.Api.Middleware;
using SunGrid.Api.Services;
using SunGrid.Api.Settings;

var builder = WebApplication.CreateBuilder(args);

// Perform startup configuration validation
ValidateConfiguration(builder.Configuration, builder.Environment);

// Configure Settings Sections
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<SeedAdminSettings>(builder.Configuration.GetSection("SeedAdminSettings"));
builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection("CorsSettings"));

// Register Singleton Database Context
builder.Services.AddSingleton<MongoDbContext>();

// Register Application Services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
// Member 1: builder.Services.AddScoped<IStationService, StationService>();
// Member 2: builder.Services.AddScoped<IBookingSlotService, BookingSlotService>();
// Member 3: builder.Services.AddScoped<IReservationService, ReservationService>();
// Member 4: builder.Services.AddScoped<IQrTransactionService, QrTransactionService>();
builder.Services.AddScoped<DataSeeder>();

// Configure JWT Authentication
var jwtSettingsSection = builder.Configuration.GetSection("JwtSettings");
var configKey = jwtSettingsSection["SecretKey"];
var secretKey = string.IsNullOrWhiteSpace(configKey) ? "SunGrid_Default_Development_Secret_Key_2026_Minimum_32_Bytes!" : configKey;
var issuer = string.IsNullOrWhiteSpace(jwtSettingsSection["Issuer"]) ? "SunGridApi" : jwtSettingsSection["Issuer"];
var audience = string.IsNullOrWhiteSpace(jwtSettingsSection["Audience"]) ? "SunGridClients" : jwtSettingsSection["Audience"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(5)
    };
});

builder.Services.AddAuthorization();

// Configure Controllers with camelCase and String Enum Serialization
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Configure CORS Policy
var corsOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>();
if (corsOrigins == null || corsOrigins.Length == 0)
{
    if (builder.Environment.IsDevelopment())
    {
        corsOrigins = new[] { "http://localhost:3000", "http://localhost:5173" };
    }
    else
    {
        corsOrigins = Array.Empty<string>();
    }
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("SunGridCorsPolicy", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure Swagger / OpenAPI with Bearer Authorization
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SunGrid API - Smart Solar Microgrid Trading System",
        Version = "v1",
        Description = "RESTful Web API for SunGrid microgrid management and authentication."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter JWT Bearer token format: Bearer {your_token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Seed initial Backoffice admin if enabled in configuration
using (var scope = app.Services.CreateScope())
{
    try
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedInitialAdminAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the initial Backoffice account.");
    }
}

// Global Exception Handler Middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

// Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SunGrid API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("SunGridCorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { message = "SunGrid API is running!", status = "Healthy" }));

app.MapControllers();

app.Run();

/// <summary>
/// Startup helper function validating required configuration settings without exposing secrets.
/// </summary>
static void ValidateConfiguration(IConfiguration config, IHostEnvironment env)
{
    var mongoConn = config["MongoDbSettings:ConnectionString"];
    var mongoDb = config["MongoDbSettings:DatabaseName"];
    var secretKey = config["JwtSettings:SecretKey"];
    var issuer = config["JwtSettings:Issuer"];
    var audience = config["JwtSettings:Audience"];

    if (string.IsNullOrWhiteSpace(mongoConn))
    {
        throw new InvalidOperationException("Startup Error: 'MongoDbSettings:ConnectionString' is missing or empty.");
    }

    if (string.IsNullOrWhiteSpace(mongoDb))
    {
        throw new InvalidOperationException("Startup Error: 'MongoDbSettings:DatabaseName' is missing or empty.");
    }

    if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
    {
        throw new InvalidOperationException("Startup Error: 'JwtSettings:SecretKey' must be at least 32 characters long.");
    }

    if (string.IsNullOrWhiteSpace(issuer))
    {
        throw new InvalidOperationException("Startup Error: 'JwtSettings:Issuer' is missing or empty.");
    }

    if (string.IsNullOrWhiteSpace(audience))
    {
        throw new InvalidOperationException("Startup Error: 'JwtSettings:Audience' is missing or empty.");
    }

    var seedEnabled = config.GetValue<bool>("SeedAdminSettings:Enabled", false);
    if (seedEnabled)
    {
        var seedEmail = config["SeedAdminSettings:Email"];
        var seedPass = config["SeedAdminSettings:Password"];
        if (string.IsNullOrWhiteSpace(seedEmail) || string.IsNullOrWhiteSpace(seedPass))
        {
            throw new InvalidOperationException("Startup Error: Seed admin is enabled but 'SeedAdminSettings:Email' or 'Password' is missing.");
        }
    }
}

