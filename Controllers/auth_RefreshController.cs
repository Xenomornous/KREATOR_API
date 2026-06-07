using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Kreator_API.Controllers;

[ApiController]
[Route("api/auth")]
public class auth_RefreshController : ControllerBase
{
    private readonly IConfiguration _config;

    public auth_RefreshController(
        IConfiguration config
    )
    {
        _config = config;
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        try
        {
            // =========================
            // GET REFRESH TOKEN COOKIE
            // =========================

            var refreshToken =
                Request.Cookies["refreshToken"];

            if (
                string.IsNullOrWhiteSpace(
                    refreshToken
                )
            )
            {
                return Unauthorized(new
                {
                    message =
                        "Brak refresh token"
                });
            }

            // =========================
            // DATABASE
            // =========================

            await using var conn =
                new NpgsqlConnection(
                    _config.GetConnectionString(
                        "Postgres"
                    )
                );

            await conn.OpenAsync();

            // =========================
            // GET TOKEN FROM DB
            // =========================

            var cmd = new NpgsqlCommand(
                @"
                SELECT
                    rt.user_id,
                    rt.expires_at,
                    rt.revoked,
                    u.email,
                    u.username,
                    u.role
                FROM refresh_tokens rt
                JOIN users u
                    ON u.id = rt.user_id
                WHERE rt.token = @token
                LIMIT 1
                ",
                conn
            );

            cmd.Parameters.AddWithValue(
                "token",
                refreshToken
            );

            await using var reader =
                await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return Unauthorized(new
                {
                    message =
                        "Niepoprawny refresh token"
                });
            }

            // =========================
            // GET DATA
            // =========================

            string userId =
                reader["user_id"].ToString()!;

            DateTime expiresAt =
                Convert.ToDateTime(
                    reader["expires_at"]
                );

            bool revoked =
                Convert.ToBoolean(
                    reader["revoked"]
                );

            string email =
                reader["email"].ToString()!;

            string username =
                reader["username"].ToString()!;

            string role =
                reader["role"].ToString()!;

            // =========================
            // VALIDATION
            // =========================

            if (revoked)
            {
                return Unauthorized(new
                {
                    message =
                        "Refresh token revoked"
                });
            }

            if (expiresAt < DateTime.UtcNow)
            {
                return Unauthorized(new
                {
                    message =
                        "Refresh token expired"
                });
            }

            // =========================
            // JWT CLAIMS
            // =========================

            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    userId
                ),

                new Claim(
                    ClaimTypes.Email,
                    email
                ),

                new Claim(
                    ClaimTypes.Name,
                    username
                ),

                new Claim(
                    ClaimTypes.Role,
                    role
                )
            };

            // =========================
            // JWT KEY
            // =========================

            var key =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(
                        _config["Jwt:Key"]!
                    )
                );

            var creds =
                new SigningCredentials(
                    key,
                    SecurityAlgorithms.HmacSha256
                );

            // =========================
            // NEW ACCESS TOKEN
            // =========================

            var token =
                new JwtSecurityToken(
                    issuer:
                        _config["Jwt:Issuer"],

                    audience:
                        _config["Jwt:Audience"],

                    claims: claims,

                    expires:
                        DateTime.UtcNow
                            .AddMinutes(15),

                    signingCredentials: creds
                );

            string jwt =
                new JwtSecurityTokenHandler()
                    .WriteToken(token);

            // =========================
            // NEW ACCESS COOKIE
            // =========================

            Response.Cookies.Append(
                "accessToken",
                jwt,
                new CookieOptions
                {
                    HttpOnly = true,

                    // PROD => true
                    Secure = true,

                    SameSite =
                        SameSiteMode.None,

                    Path = "/",

                    Expires =
                        DateTime.UtcNow
                            .AddMinutes(15)
                }
            );

            return Ok(new
            {
                message =
                    "Access token odświeżony"
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);

            return StatusCode(
                500,
                new
                {
                    message =
                        "Internal server error"
                }
            );
        }
    }

    // =========================
    // GENERATE REFRESH TOKEN
    // =========================

    private string GenerateRefreshToken()
    {
        var randomBytes =
            new byte[64];

        using var rng =
            RandomNumberGenerator.Create();

        rng.GetBytes(randomBytes);

        return Convert.ToBase64String(
            randomBytes
        );
    }
}