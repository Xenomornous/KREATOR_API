// Controllers/AuthController.cs

using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Kreator_API.Models;

namespace Kreator_API.Controllers
{ 
[ApiController]
[Route("api/auth")]
public class RegisterController : ControllerBase
{
    private readonly IConfiguration _config;

    public RegisterController(IConfiguration config)
    {
        _config = config;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] auth_Register dto)
    {
        try
        {
            // =========================
            // BASIC VALIDATION
            // =========================

            if (
                string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.Username) ||
                string.IsNullOrWhiteSpace(dto.Password)
            )
            {
                return BadRequest(new
                {
                    message = "Wszystkie pola są wymagane"
                });
            }

            // =========================
            // SANITIZATION
            // =========================

            string email =
                dto.Email.Trim().ToLower();

            string username =
                dto.Username.Trim().ToLower();

            string password =
                dto.Password.Trim();

            // =========================
            // EMAIL VALIDATION
            // =========================

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

            // =========================
            // USERNAME VALIDATION
            // =========================
            // tylko:
            // a-z
            // 0-9
            // _
            // -
            // minimum 3 znaki

            if (
                !Regex.IsMatch(
                    username,
                    @"^[a-zA-Z0-9_-]{3,20}$"
                )
            )
            {
                return BadRequest(new
                {
                    message =
                        "Username może zawierać tylko litery, cyfry, _ i -"
                });
            }

            // =========================
            // PASSWORD POLICY
            // =========================

            List<string> passwordErrors = [];

            if (password.Length < 8)
            {
                passwordErrors.Add(
                    "minimum 8 znaków"
                );
            }

            if (!password.Any(char.IsUpper))
            {
                passwordErrors.Add(
                    "minimum 1 wielka litera"
                );
            }

            if (!password.Any(char.IsDigit))
            {
                passwordErrors.Add(
                    "minimum 1 cyfra"
                );
            }

            if (
                !Regex.IsMatch(
                    password,
                    @"[\W_]"
                )
            )
            {
                passwordErrors.Add(
                    "minimum 1 znak specjalny"
                );
            }

            if (passwordErrors.Count > 0)
            {
                return BadRequest(new
                {
                    message =
                        "Hasło nie spełnia wymagań",

                    errors = passwordErrors
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
            // CHECK USER EXISTS
            // =========================

            var checkCmd = new NpgsqlCommand(
                @"
                SELECT COUNT(*)
                FROM users
                WHERE email = @email
                   OR username = @username
                ",
                conn
            );

            // 🔥 SQL INJECTION SAFE
            checkCmd.Parameters.AddWithValue(
                "email",
                email
            );

            checkCmd.Parameters.AddWithValue(
                "username",
                username
            );

            var exists = Convert.ToInt32(
                await checkCmd.ExecuteScalarAsync()
            );

            if (exists > 0)
            {
                return Conflict(new
                {
                    message =
                        "Email lub username już istnieje"
                });
            }

            // =========================
            // HASH PASSWORD
            // =========================

            string passwordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    password
                );

            // =========================
            // INSERT USER
            // =========================

            var insertCmd = new NpgsqlCommand(
                @"
                INSERT INTO users
                (
                    email,
                    username,
                    password_hash,
                    role
                )
                VALUES
                (
                    @email,
                    @username,
                    @password_hash,
                    @role
                )
                RETURNING id;
                ",
                conn
            );

            // 🔥 SQL INJECTION SAFE
            insertCmd.Parameters.AddWithValue(
                "email",
                email
            );

            insertCmd.Parameters.AddWithValue(
                "username",
                username
            );

            insertCmd.Parameters.AddWithValue(
                "password_hash",
                passwordHash
            );

            insertCmd.Parameters.AddWithValue(
                "role",
                "user"
            );

            var userId =
                await insertCmd.ExecuteScalarAsync();

            // =========================
            // JWT CLAIMS
            // =========================

            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    userId!.ToString()!
                ),

                new Claim(
                    ClaimTypes.Email,
                    email
                ),

                new Claim(
                    ClaimTypes.Role,
                    "user"
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
            // JWT TOKEN
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
            // COOKIE
            // =========================

            Response.Cookies.Append(
                "accessToken",
                jwt,
                new CookieOptions
                {
                    HttpOnly = true,

                    // DEV:
                    Secure = false,

                    SameSite =
                        SameSiteMode.Strict,

                    Path = "/",

                    Expires =
                        DateTime.UtcNow
                            .AddMinutes(15)
                }
            );

            // =========================
            // SUCCESS
            // =========================

            return Ok(new
            {
                message =
                    "Rejestracja poprawna"
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
}
}