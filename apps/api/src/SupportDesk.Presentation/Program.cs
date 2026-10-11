using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SupportDesk.Application;
using SupportDesk.Application.Abstractions;
using SupportDesk.Infrastructure;
using SupportDesk.Presentation.Extensions;
using SupportDesk.Presentation.Filters;
using SupportDesk.Presentation.Health;
using SupportDesk.Presentation.Middleware;
using SupportDesk.Presentation.Security;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "frontend";

builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options =>
    {
        // Enums travel as their names, both ways, so the API reads well from the browser.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddCors(options => options.AddPolicy(
    FrontendCorsPolicy,
    policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Adds an Authorize button: paste the token from POST /api/auth/login to try protected endpoints.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "The token returned by POST /api/auth/login."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

// Sign-in. The key comes from configuration or the environment, never from code, and a missing or
// weak key stops the application here.
var jwt = JwtSettings.FromConfiguration(builder.Configuration);

builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep the claim names as issued ("sub", "name"), instead of mapping them to long URIs.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwt.CreateKey(),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name"
        };
    });

// Everything needs a signed-in agent unless it says [AllowAnonymous]. Set once, here, so a new
// endpoint is closed by default and nobody has to remember to protect it.
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Keeps a developer machine one command away from a working database.
    await app.MigrateAndSeedAsync();
}

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Open on purpose: Docker and load balancers call it with no token. It is the one endpoint, apart
// from login, that opts out of the fallback policy, and it is declared here so it can be seen.
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

// Lets the integration tests start the application in memory.
public partial class Program;
