using Kreator_API.Models;
using Kreator_API.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Kreator_API.Controllers;

[ApiController]
[Route("api/auth")]
public class auth_ResendVerificationController
    : ControllerBase
{
    private readonly IConfiguration _config;

    private readonly EmailService
        _emailService;

    public auth_ResendVerificationController(
        IConfiguration config,
        EmailService emailService
    )
    {
        _config = config;
        _emailService = emailService;
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult>
        ResendVerification(
            [FromBody]
            auth_ResendVerification dto
        )
    {
        try
        {
            if (
                string.IsNullOrWhiteSpace(
                    dto.Email
                )
            )
            {
                return BadRequest(new
                {
                    message =
                        "Email jest wymagany"
                });
            }

            string email =
                dto.Email
                    .Trim()
                    .ToLower();

            if (
                !Regex.IsMatch(
                    email,
                    @"^[^@\s]+@[^@\s]+\.[^@\s]+$"
                )
            )
            {
                return BadRequest(new
                {
                    message =
                        "Niepoprawny email"
                });
            }

            await using var conn =
                new NpgsqlConnection(
                    _config.GetConnectionString(
                        "Postgres"
                    )
                );

            await conn.OpenAsync();

            var userCmd =
                new NpgsqlCommand(
                    @"
                    SELECT
                        id,
                        is_email_verified
                    FROM users
                    WHERE email = @email
                    LIMIT 1
                    ",
                    conn
                );

            userCmd.Parameters.AddWithValue(
                "email",
                email
            );

            await using var reader =
                await userCmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return Ok(new
                {
                    message =
                        "Jeżeli konto istnieje, wysłaliśmy wiadomość email."
                });
            }

            string userId =
                reader["id"].ToString()!;

            bool verified =
                Convert.ToBoolean(
                    reader["is_email_verified"]
                );

            await reader.CloseAsync();

            if (verified)
            {
                return Ok(new
                {
                    message =
                        "Konto jest już zweryfikowane."
                });
            }

            string verificationToken =
                GenerateVerificationToken();

            var insertCmd =
                new NpgsqlCommand(
                    @"
                    INSERT INTO
                    email_verification_tokens
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

            insertCmd.Parameters.AddWithValue(
                "user_id",
                Guid.Parse(userId)
            );

            insertCmd.Parameters.AddWithValue(
                "token",
                verificationToken
            );

            insertCmd.Parameters.AddWithValue(
                "expires_at",
                DateTime.UtcNow.AddDays(1)
            );

            await insertCmd.ExecuteNonQueryAsync();

            await _emailService
                .SendVerificationEmail(
                    email,
                    verificationToken
                );

            return Ok(new
            {
                message =
                    "Wysłaliśmy nową wiadomość weryfikacyjną."
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

    private string GenerateVerificationToken()
    {
        var randomBytes =
            new byte[64];

        using var rng =
            RandomNumberGenerator.Create();

        rng.GetBytes(randomBytes);

        return Convert.ToHexString(
            randomBytes
        );
    }
}