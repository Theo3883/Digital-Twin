using System.Text;
using DigitalTwin.Composition;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

// ── Load .env.local before the host builder reads configuration ───────────────
var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env.local");
if (File.Exists(envFile))
    DotNetEnv.Env.Load(envFile);

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

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
