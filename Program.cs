using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Resend;
using Kreator_API.Services;
using AspNetCoreRateLimit;
using Kreator_API.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddHttpClient();

builder.Services.AddMemoryCache();

builder.Services.Configure<
    IpRateLimitOptions
>(
    builder.Configuration.GetSection(
        "IpRateLimiting"
    )
);

builder.Services
    .AddInMemoryRateLimiting();

builder.Services.AddSingleton<
    IRateLimitConfiguration,
    RateLimitConfiguration
>();

// email
builder.Services.Configure<ResendClientOptions>(
    options =>
    {
        options.ApiToken =
            builder.Configuration[
                "Resend:ApiKey"
            ]!;
    }
);

builder.Services.AddTransient<IResend, ResendClient>();

builder.Services.AddScoped<
    EmailService
>();

builder.Services.AddHostedService<
    DeleteUnverifiedUsersService
>();

builder.Services.AddOpenApi();

// JWT AUTH
builder.Services.AddAuthentication(
    JwtBearerDefaults.AuthenticationScheme
)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters =
        new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer =
                builder.Configuration["Jwt:Issuer"],

            ValidAudience =
                builder.Configuration["Jwt:Audience"],

            IssuerSigningKey =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(
                        builder.Configuration["Jwt:Key"]!
                    )
                )
        };

    // JWT z COOKIE
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            context.Token =
                context.Request.Cookies["accessToken"];

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// CORS dla Next.js
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .WithOrigins("https://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials(); // 🔥 WAŻNE DLA COOKIE
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseIpRateLimiting();

//  AUTH
app.UseMiddleware<
    CsrfMiddleware
>();
app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();