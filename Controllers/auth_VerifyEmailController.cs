using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
namespace Kreator_API.Controllers;

[ApiController]
[Route("api/auth")]
public class auth_VerifyEmailController
    : ControllerBase
{
    private readonly IConfiguration _config;

    public auth_VerifyEmailController(
        IConfiguration config
    )
    {
        _config = config;
    }

    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmail(
        [FromQuery] string token
    )
    {
        try
        {
            if (
                string.IsNullOrWhiteSpace(token)
            )
            {
                return BadRequest(new
                {
                    message =
                        "Brak tokena"
                });
            }

            await using var conn =
                new NpgsqlConnection(
                    _config.GetConnectionString(
                        "Postgres"
                    )
                );

            await conn.OpenAsync();

            // =========================
            // GET TOKEN
            // =========================

            var cmd = new NpgsqlCommand(
                @"
                SELECT
                    user_id,
                    expires_at,
                    used
                FROM email_verification_tokens
                WHERE token = @token
                LIMIT 1
                ",
                conn
            );

            cmd.Parameters.AddWithValue(
                "token",
                token
            );

            Console.WriteLine(
            $"TOKEN FROM URL: {token}"
            );

            await using var reader =
                await cmd.ExecuteReaderAsync();



            if (!await reader.ReadAsync())
            {
                return BadRequest(new
                {
                    message =
                        "Niepoprawny token"
                });
            }

            string userId =
                reader["user_id"].ToString()!;

            DateTime expiresAt =
                Convert.ToDateTime(
                    reader["expires_at"]
                );

            bool used =
                Convert.ToBoolean(
                    reader["used"]
                );

            await reader.CloseAsync();

            // =========================
            // VALIDATION
            // =========================

            if (used)
            {
                return BadRequest(new
                {
                    message =
                        "Token został już użyty"
                });
            }

            if (
                expiresAt <
                DateTime.UtcNow
            )
            {
                return BadRequest(new
                {
                    message =
                        "Token wygasł"
                });
            }

            // =========================
            // VERIFY USER
            // =========================

            var verifyUserCmd =
                new NpgsqlCommand(
                    @"
                    UPDATE users
                    SET is_email_verified = true
                    WHERE id = @id
                    ",
                    conn
                );

            verifyUserCmd
                .Parameters
                .AddWithValue(
                    "id",
                    Guid.Parse(userId)
                );

            await verifyUserCmd
                .ExecuteNonQueryAsync();

            // =========================
            // MARK TOKEN USED
            // =========================

            var usedCmd =
                new NpgsqlCommand(
                    @"
                    UPDATE
                        email_verification_tokens
                    SET
                        used = true
                    WHERE
                        token = @token
                    ",
                    conn
                );

            usedCmd.Parameters
                .AddWithValue(
                    "token",
                    token
                );

            await usedCmd
                .ExecuteNonQueryAsync();

            var userCmd = new NpgsqlCommand(
                @"
                SELECT
                    email,
                    username,
                    role
                FROM users
                WHERE id = @id
                ",
                conn
            );

            userCmd.Parameters.AddWithValue(
                "id",
                Guid.Parse(userId)
            );

            await using var userReader =
                await userCmd.ExecuteReaderAsync();

            await userReader.ReadAsync();

            string email =
                userReader["email"].ToString()!;

            string username =
                userReader["username"].ToString()!;

            string role =
                userReader["role"].ToString()!;

            await userReader.CloseAsync();

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
        "username",
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
            // ACCESS TOKEN
            // =========================

            var accessToken =
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
                    .WriteToken(accessToken);

            // =========================
            // ACCESS COOKIE
            // =========================

            Response.Cookies.Append(
                "accessToken",
                jwt,
                new CookieOptions
                {
                    HttpOnly = true,

                    Secure = true,

                    SameSite =
                        SameSiteMode.None,

                    Path = "/",

                    Expires =
                        DateTime.UtcNow
                            .AddMinutes(15)
                }
            );

            // =========================
            // REFRESH TOKEN
            // =========================

            string refreshToken =
                GenerateRefreshToken();

            // =========================
            // SAVE REFRESH TOKEN
            // =========================

            var refreshCmd =
                new NpgsqlCommand(
                    @"
        INSERT INTO refresh_tokens
        (
            user_id,
            token,
            expires_at
        )
        VALUES
        (
            @user_id,
            @token,
            @expires_at
        )
        ",
                    conn
                );

            refreshCmd.Parameters.AddWithValue(
                "user_id",
                Guid.Parse(userId)
            );

            refreshCmd.Parameters.AddWithValue(
                "token",
                refreshToken
            );

            refreshCmd.Parameters.AddWithValue(
                "expires_at",
                DateTime.UtcNow.AddDays(30)
            );

            await refreshCmd.ExecuteNonQueryAsync();

            // =========================
            // REFRESH COOKIE
            // =========================

            Response.Cookies.Append(
                "refreshToken",
                refreshToken,
                new CookieOptions
                {
                    HttpOnly = true,

                    Secure = true,

                    SameSite =
                        SameSiteMode.None,

                    Path = "/",

                    Expires =
                        DateTime.UtcNow
                            .AddDays(30)
                }
            );

            return Ok(new
            {
                message =
                    "Email zweryfikowany",

                redirect =
                    "/dashboard"
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