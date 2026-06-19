using Kreator_API.Models;
using Kreator_API.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Kreator_API.Controllers;

[ApiController]
[Route("api/auth")]
public class auth_ForgotPasswordController
    : ControllerBase
{
    private readonly IConfiguration _config;

    private readonly EmailService
        _emailService;

    public auth_ForgotPasswordController(
        IConfiguration config,
        EmailService emailService
    )
    {
        _config = config;
        _emailService = emailService;
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult>
        ForgotPassword(
            [FromBody]
            auth_ForgotPassword dto
        )
    {
        try
        {
            // =========================
            // VALIDATION
            // =========================

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
            // FIND USER
            // =========================

            var cmd =
                new NpgsqlCommand(
                    @"
                    SELECT id
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

            var userId =
                await cmd.ExecuteScalarAsync();

            // =========================
            // SECURITY
            // =========================

            if (userId == null)
            {
                return Ok(new
                {
                    message =
                        "Jeżeli konto istnieje, wysłaliśmy wiadomość email."
                });
            }

            // =========================
            // TOKEN
            // =========================

            string resetToken =
                GenerateResetToken();

            // =========================
            // SAVE TOKEN
            // =========================

            var insertCmd =
                new NpgsqlCommand(
                    @"
                    INSERT INTO
                    password_reset_tokens
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
                Guid.Parse(
                    userId.ToString()!
                )
            );

            insertCmd.Parameters.AddWithValue(
                "token",
                resetToken
            );

            insertCmd.Parameters.AddWithValue(
                "expires_at",
                DateTime.UtcNow
                    .AddHours(1)
            );

            await insertCmd
                .ExecuteNonQueryAsync();

            // =========================
            // SEND EMAIL
            // =========================

            await _emailService
                .SendPasswordResetEmail(
                    email,
                    resetToken
                );

            // =========================
            // SUCCESS
            // =========================

            return Ok(new
            {
                message =
                    "Jeżeli konto istnieje, wysłaliśmy wiadomość email."
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

    private string GenerateResetToken()
    {
        var randomBytes =
            new byte[64];

        using var rng =
            RandomNumberGenerator.Create();

        rng.GetBytes(
            randomBytes
        );

        return Convert.ToHexString(
            randomBytes
        );
    }


}