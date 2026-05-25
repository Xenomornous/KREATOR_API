using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Kreator_API.Models;

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
                    is_active
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

            string passwordHash =
                reader["password_hash"].ToString()!;

            string role =
                reader["role"].ToString()!;

            bool isActive =
                Convert.ToBoolean(
                    reader["is_active"]
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
                    ClaimTypes.Name,
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
                            .AddMinutes(15),

                    signingCredentials: creds
                );

            string jwt =
                new JwtSecurityTokenHandler()
                    .WriteToken(token);

            // COOKIE

            Response.Cookies.Append(
                "accessToken",
                jwt,
                new CookieOptions
                {
                    HttpOnly = true,

                    // PROD => true
                    Secure = false,

                    SameSite =
                        SameSiteMode.Strict,

                    Path = "/",

                    Expires =
                        DateTime.UtcNow
                            .AddMinutes(15)
                }
            );

            // CLOSE READER

            await reader.CloseAsync();

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