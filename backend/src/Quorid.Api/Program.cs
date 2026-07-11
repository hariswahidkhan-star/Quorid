using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Quorid.Api.Endpoints;
using Quorid.Api.Identity;
using Quorid.Api.Middleware;
using Quorid.Application;
using Quorid.Application.Common.Interfaces;
using Quorid.Infrastructure;
using Quorid.Infrastructure.Identity;
using Quorid.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Honor the platform-assigned port (Render/Heroku/Cloud Run set PORT).
var listenPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(listenPort))
    builder.WebHost.UseUrls($"http://0.0.0.0:{listenPort}");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// --- Upload limits (spec §10: 50 MB max file) ---
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 60L * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 60L * 1024 * 1024;
});

// --- Authentication (JWT bearer) ---
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub"
        };
    });

// --- Authorization: one policy per permission in the catalog (spec §Screen 9) ---
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Quorid.Application.Common.Security.Permissions.All)
    {
        options.AddPolicy(
            Quorid.Api.Authorization.Policies.For(permission),
            policy => policy.Requirements.Add(new Quorid.Api.Authorization.PermissionRequirement(permission)));
    }
});
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
    Quorid.Api.Authorization.PermissionAuthorizationHandler>();

// --- JSON: serialize enums as strings, ignore navigation cycles ---
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// --- CORS for the React dev server ---
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
    options.AddPolicy("frontend", policy =>
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

// --- Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Quorid API", Version = "v1" });
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    options.AddSecurityDefinition("Bearer", scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --- Database bootstrap ---
// Production: apply EF Core migrations on startup (Database:MigrateOnStartup=true).
// Pure dev: EnsureCreated builds the schema from the model with no migration history.
// Retries a few times so a managed DB that is still booting (e.g. on Render) settles.
if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
    RunBootstrap(db => db.Database.Migrate(), rethrowOnFail: true);
else if (app.Configuration.GetValue("Database:EnsureCreatedOnStartup", false))
    RunBootstrap(db => db.Database.EnsureCreated(), rethrowOnFail: false);

void RunBootstrap(Action<ApplicationDbContext> action, bool rethrowOnFail)
{
    const int maxAttempts = 6;
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
            return;
        }
        catch (Exception ex)
        {
            if (attempt >= maxAttempts)
            {
                if (rethrowOnFail)
                {
                    app.Logger.LogError(ex, "Database bootstrap failed after {Attempts} attempts.", attempt);
                    throw;
                }
                app.Logger.LogWarning(ex, "Database bootstrap skipped after {Attempts} attempts.", attempt);
                return;
            }
            app.Logger.LogWarning("Database not ready (attempt {Attempt}/{Max}); retrying in 3s…", attempt, maxAttempts);
            Thread.Sleep(TimeSpan.FromSeconds(3));
        }
    }
}

// Serve the bundled React SPA from wwwroot when present (single-origin deploy).
// API endpoints and /health are matched first; unmatched routes fall back to the SPA.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("frontend");
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "quorid-api" }))
   .WithTags("System");

app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapDocumentEndpoints();
app.MapDocumentCaptureEndpoints();
app.MapDocumentManagementEndpoints();
app.MapDocumentStudioEndpoints();
app.MapSharingEndpoints();
app.MapVaultEndpoints();
app.MapComplianceEndpoints();
app.MapEngagementEndpoints();
app.MapProposalEndpoints();
app.MapAnalyticsEndpoints();
app.MapAdminEndpoints();
app.MapRoomEndpoints();
app.MapGuestPortalEndpoints();
app.MapPortalEndpoints();

// SPA client-side routing: serve index.html for any unmatched non-API GET.
app.MapFallbackToFile("index.html");

app.Run();
