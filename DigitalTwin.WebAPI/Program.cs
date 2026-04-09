using System.Text;
using DigitalTwin.Application.Configuration;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Services;
using DigitalTwin.Composition;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Interfaces.Services;
using DigitalTwin.Integrations.AI;
using DigitalTwin.Integrations.Medication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using DigitalTwin.WebAPI.Middleware;

// ── Load .env.local before the host builder reads configuration ───────────────
var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env.local");
if (File.Exists(envFile))
    DotNetEnv.Env.Load(envFile);

// ── Application config (same source as MAUI — reads env vars set above) ──────
var appConfig = EnvConfig.FromEnvironment();

var builder = WebApplication.CreateBuilder(args);

// ── JWT Authentication ───────────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// ── CORS ─────────────────────────────────────────────────────────────────────
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:3000"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(corsOrigins)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

// ── Digital Twin (cloud-only: Domain, Application, Infrastructure) ───────────
var cloudConn = builder.Configuration.GetConnectionString("CloudDb")
    ?? throw new InvalidOperationException("ConnectionStrings:CloudDb is required.");
builder.Services.AddDigitalTwinForWebApi(cloudConn);

// ── Medication providers ──────────────────────────────────────────────────────
builder.Services.AddSingleton(new MedicationApiOptions());
builder.Services.AddHttpClient<IDrugSearchProvider, RxNavDrugSearchProvider>();
builder.Services.AddHttpClient<IMedicationInteractionProvider, OpenFdaMedicationInteractionProvider>();
builder.Services.AddHttpClient<RxNavRxCuiResolver>();

// ── RxCUI lookup: RxNav-first (authoritative) with optional Gemini fallback ───
builder.Services.Configure<GeminiPromptOptions>(_ => { });
builder.Services.AddHttpClient("GeminiApi", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
    c.DefaultRequestHeaders.Add("Accept", "application/json");
});

if (appConfig.UseGeminiAi)
{
    builder.Services.AddSingleton<IGeminiApiClient>(sp => new GeminiApiClient(
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<IOptions<GeminiPromptOptions>>(),
        sp.GetRequiredService<ILogger<GeminiApiClient>>(),
        appConfig.GeminiApiKey!));
    builder.Services.AddScoped<GeminiRxCuiLookupProvider>();
    builder.Services.AddScoped<IRxCuiLookupProvider>(sp => new ChainedRxCuiLookupProvider(
        primary:  sp.GetRequiredService<RxNavRxCuiResolver>(),
        fallback: sp.GetRequiredService<GeminiRxCuiLookupProvider>()));
}
else
{
    builder.Services.AddScoped<IRxCuiLookupProvider>(sp => new ChainedRxCuiLookupProvider(
        primary:  sp.GetRequiredService<RxNavRxCuiResolver>(),
        fallback: new NullRxCuiLookupProvider()));
}

builder.Services.AddScoped<IMedicationApplicationService>(sp => new MedicationApplicationService(
    sp.GetRequiredService<IMedicationInteractionProvider>(),
    sp.GetRequiredService<IDrugSearchProvider>(),
    sp.GetRequiredService<IRxCuiLookupProvider>(),
    sp.GetRequiredService<IMedicationInteractionService>(),
    sp.GetRequiredService<IMedicationService>(),
    sp.GetRequiredService<IMedicationManagementService>(),
    sp.GetRequiredService<IDomainEventDispatcher>()));

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// Global error boundary (keeps responses safe/consistent)
app.UseMiddleware<GlobalExceptionMiddleware>();

// ── Transport security ───────────────────────────────────────────────────────
// In production, enforce HTTPS and HSTS so browsers pin HTTPS for this origin.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// ── Security headers (baseline) ──────────────────────────────────────────────
app.Use(async (ctx, next) =>
{
    // Avoid MIME sniffing
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    // Clickjacking protection
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    // Reduce referrer leakage
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    // Lock down powerful browser features by default (portal should allow explicitly if needed)
    ctx.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
