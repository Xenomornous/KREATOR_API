using BCrypt.Net;
using Kreator_API.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text.RegularExpressions;

namespace Kreator_API.Controllers;

[ApiController]
[Route("api/auth")]
public class auth_ResetPasswordController
    : ControllerBase
{
    private readonly IConfiguration _config;

    public auth_ResetPasswordController(
        IConfiguration config
    )
    {
        _config = config;
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult>
        ResetPassword(
            [FromBody]
            auth_ResetPassword dto
        )
    {
        try
        {
            // =========================
            // VALIDATION
            // =========================

            if (
                string.IsNullOrWhiteSpace(
                    dto.Token
                ) ||
                string.IsNullOrWhiteSpace(
                    dto.Password
                )
            )
            {
                return BadRequest(new
                {
                    message =
                        "Token i hasło są wymagane"
                });
            }

            // =========================
            // PASSWORD POLICY
            // =========================

            List<string> errors = [];

            if (dto.Password.Length < 8)
            {
                errors.Add(
                    "minimum 8 znaków"
                );
            }

            if (
                !dto.Password.Any(
                    char.IsUpper
                )
            )
            {
                errors.Add(
                    "minimum 1 wielka litera"
                );
            }

            if (
                !dto.Password.Any(
                    char.IsDigit
                )
            )
            {
                errors.Add(
                    "minimum 1 cyfra"
                );
            }

            if (
                !Regex.IsMatch(
                    dto.Password,
                    @"[\W_]"
                )
            )
            {
                errors.Add(
                    "minimum 1 znak specjalny"
                );
            }

            if (errors.Count > 0)
            {
                return BadRequest(new
                {
                    message =
                        "Hasło nie spełnia wymagań",

                    errors
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
            // GET TOKEN
            // =========================

            var cmd =
                new NpgsqlCommand(
                    @"
                    SELECT
                        user_id,
                        expires_at,
                        used
                    FROM
                        password_reset_tokens
                    WHERE
                        token = @token
                    LIMIT 1
                    ",
                    conn
                );

            cmd.Parameters.AddWithValue(
                "token",
                dto.Token
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
                reader["user_id"]
                    .ToString()!;

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
            // TOKEN VALIDATION
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
            // HASH PASSWORD
            // =========================

            string passwordHash =
                BCrypt.Net.BCrypt
                    .HashPassword(
                        dto.Password
                    );

            // =========================
            // UPDATE USER
            // =========================

            var updateUserCmd =
                new NpgsqlCommand(
                    @"
                    UPDATE users
                    SET password_hash =
                        @password_hash
                    WHERE id = @id
                    ",
                    conn
                );

            updateUserCmd
                .Parameters
                .AddWithValue(
                    "password_hash",
                    passwordHash
                );

            updateUserCmd
                .Parameters
                .AddWithValue(
                    "id",
                    Guid.Parse(
                        userId
                    )
                );

            await updateUserCmd
                .ExecuteNonQueryAsync();

            // =========================
            // MARK TOKEN USED
            // =========================

            var usedCmd =
                new NpgsqlCommand(
                    @"
                    UPDATE
                        password_reset_tokens
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
                    dto.Token
                );

            await usedCmd
                .ExecuteNonQueryAsync();

            // =========================
            // SUCCESS
            // =========================

            return Ok(new
            {
                message =
                    "Hasło zostało zmienione"
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