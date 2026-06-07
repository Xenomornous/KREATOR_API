using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Kreator_API.Models;

namespace Kreator_API.Controllers;

[ApiController]
[Route("api/auth")]
public class auth_MeController : ControllerBase
{
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        foreach (var claim in User.Claims)
        {
            Console.WriteLine(
                $"{claim.Type} = {claim.Value}"
            );
        }
        try
        {
            // =========================
            // JWT CLAIMS
            // =========================

            string? userId =
                User.FindFirst(
                    ClaimTypes.NameIdentifier
                )?.Value;

            string? email =
                User.FindFirst(
                    ClaimTypes.Email
                )?.Value;

            string? username =
                User.FindFirst(
                    "username"
                )?.Value;

            string? role =
                User.FindFirst(
                    ClaimTypes.Role
                )?.Value;

            // =========================
            // SAFETY CHECK
            // =========================

            if (
                string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(email)
            )
            {
                return Unauthorized(new
                {
                    message =
                        "Niepoprawny token"
                });
            }

            // =========================
            // RESPONSE MODEL
            // =========================

            var response = new auth_Me
            {
                Id = userId,
                Email = email,
                Username = username ?? "",
                Role = role ?? "user"
            };

            return Ok(response);
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