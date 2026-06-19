using Kreator_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Kreator_API.Controllers;

[ApiController]
[Route("api/auth")]
public class LoginController : ControllerBase
{
    private readonly IConfiguration _config;

    public LoginController(IConfiguration config)
    {
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] auth_Login dto)
    {

        Console.WriteLine("LOGIN CONTROLLER HIT");
        try
        {
            // VALIDATION

            if (
                string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.Password)
            )
            {
                return BadRequest(new
                {
                    message = "Email i hasło są wymagane"
                });
            }

            string email =
                dto.Email.Trim().ToLower();

            string password =
                dto.Password.Trim();

            // EMAIL VALIDATION

            if (
                !Regex.IsMatch(
                    email,
                    @"^[^@\s]+@[^@\s]+\.[^@\s]+$"
                )
            )
            {
                return BadRequest(new
                {
                    message = "Niepoprawny email"
                });
            }

            // DATABASE

            await using var conn =
                new NpgsqlConnection(
                    _config.GetConnectionString(
                        "Postgres"
                    )
                );

            await conn.OpenAsync();

            // GET USER

            var cmd = new NpgsqlCommand(
                @"
                SELECT
                    id,
                    email,
                    username,
                    password_hash,
                    role,
                    is_active,
                    is_email_verified
                FROM users
                WHERE email = @email
                LIMIT 1
                ",
                conn
            );

            cmd.Parameters.AddWithValue(
                "email",
                email
            );

            await using var reader =
                await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return Unauthorized(new
                {
                    message =
                        "Niepoprawny email lub hasło"
                });
            }

            // USER DATA

            string userId =
                reader["id"].ToString()!;

            string dbEmail =
                reader["email"].ToString()!;

            string username =
                reader["username"].ToString()!;
            Console.WriteLine(
            $"USERNAME FROM DB: {username}"
            );

            string passwordHash =
                reader["password_hash"].ToString()!;

            string role =
                reader["role"].ToString()!;

            bool isActive =
                Convert.ToBoolean(
                    reader["is_active"]
                );

            bool isEmailVerified =
                Convert.ToBoolean(
                    reader["is_email_verified"]
                );

            // ACCOUNT ACTIVE

            if (!isActive)
            {
                return Unauthorized(new
                {
                    message =
                        "Konto zostało zablokowane"
                });
            }

            // Check email verification
            if (!isEmailVerified)
            {
                return Unauthorized(new
                {
                    message =
                        "Zweryfikuj adres email przed zalogowaniem"
                });
            }

            // VERIFY PASSWORD

            bool validPassword =
                BCrypt.Net.BCrypt.Verify(
                    password,
                    passwordHash
                );

            if (!validPassword)
            {
                return Unauthorized(new
                {
                    message =
                        "Niepoprawny email lub hasło"
                });
            }

            // JWT CLAIMS

            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    userId
                ),

                new Claim(
                    ClaimTypes.Email,
                    dbEmail
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

            // JWT KEY

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

            // TOKEN

            var token =
                new JwtSecurityToken(
                    issuer:
                        _config["Jwt:Issuer"],

                    audience:
                        _config["Jwt:Audience"],

                    claims: claims,

                    expires:
                        DateTime.UtcNow
                            .AddHours(12),

                    signingCredentials: creds
                );

            string jwt =
                new JwtSecurityTokenHandler()
                    .WriteToken(token);

            // REFRESH TOKEN

            string refreshToken =
                GenerateRefreshToken();

            // COOKIE

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

            // SAVE REFRESH TOKEN

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

            // CLOSE READER

            await reader.CloseAsync();

            Console.WriteLine("READER CLOSED");

            // SAVE REFRESH TOKEN
            await refreshCmd.ExecuteNonQueryAsync();

            Console.WriteLine("REFRESH SAVED");

            // REFRESH COOKIE

            Console.WriteLine("CREATING REFRESH COOKIE");
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

            // UPDATE LAST LOGIN

            var updateCmd =
                new NpgsqlCommand(
                    @"
                    UPDATE users
                    SET last_login_at = NOW()
                    WHERE id = @id
                    ",
                    conn
                );

            updateCmd.Parameters.AddWithValue(
                "id",
                Guid.Parse(userId)
            );

            await updateCmd.ExecuteNonQueryAsync();

            // SUCCESS

            return Ok(new
            {
                message = "Logowanie poprawne",

                user = new
                {
                    id = userId,
                    email = dbEmail,
                    username,
                    role
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "LOGIN ERROR:"
            );

            Console.WriteLine(ex.ToString());

            return StatusCode(
                500,
                new
                {
                    message =
                        ex.Message
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

