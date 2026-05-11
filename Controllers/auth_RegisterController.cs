// Controllers/AuthController.cs

using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using YourProject.DTO;

namespace Kreator_API.Controllers

{ 

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest dto)
    {
        try
        {
            // BASIC VALIDATION
            if (
                string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.Username) ||
                string.IsNullOrWhiteSpace(dto.Password)
            )
            {
                return BadRequest("Brak wymaganych pól");
            }

            // PASSWORD MIN LENGTH
            if (dto.Password.Length < 8)
            {
                return BadRequest(
                    "Hasło musi mieć minimum 8 znaków"
                );
            }

            await using var conn = new NpgsqlConnection(
                _config.GetConnectionString("Postgres")
            );

            await conn.OpenAsync();

            // CHECK EMAIL EXISTS
            var checkCmd = new NpgsqlCommand(
                @"SELECT COUNT(*) 
                  FROM users 
                  WHERE email = @email 
                     OR username = @username",
                conn
            );

            checkCmd.Parameters.AddWithValue(
                "email",
                dto.Email.ToLower()
            );

            checkCmd.Parameters.AddWithValue(
                "username",
                dto.Username
            );

            var exists = Convert.ToInt32(
                await checkCmd.ExecuteScalarAsync()
            );

            if (exists > 0)
            {
                return Conflict(
                    "Email lub username już istnieje"
                );
            }

            // HASH PASSWORD
            string passwordHash =
                BCrypt.Net.BCrypt.HashPassword(dto.Password);

            // INSERT USER
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

            insertCmd.Parameters.AddWithValue(
                "email",
                dto.Email.ToLower()
            );

            insertCmd.Parameters.AddWithValue(
                "username",
                dto.Username
            );

            insertCmd.Parameters.AddWithValue(
                "password_hash",
                passwordHash
            );

            insertCmd.Parameters.AddWithValue(
                "role",
                "user"
            );

            var userId = await insertCmd.ExecuteScalarAsync();

            // JWT TOKEN
            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    userId!.ToString()!
                ),

                new Claim(
                    ClaimTypes.Email,
                    dto.Email
                ),

                new Claim(
                    ClaimTypes.Role,
                    "user"
                )
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _config["Jwt:Key"]!
                )
            );

            var creds = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: creds
            );

            string jwt = new JwtSecurityTokenHandler()
                .WriteToken(token);

            // COOKIE
            Response.Cookies.Append(
                "accessToken",
                jwt,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTime.UtcNow.AddMinutes(15)
                }
            );

            return Ok(new
            {
                message = "Rejestracja poprawna"
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);

            return StatusCode(
                500,
                "Internal server error"
            );
        }
    }
}
}