using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Kreator_API.Models;

namespace Kreator_API.Controllers;

[ApiController]
[Route("api/auth")]
public class auth_LogoutController : ControllerBase
{
    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        try
        {
            // =========================
            // DELETE COOKIE
            // =========================

            Response.Cookies.Delete(
                "accessToken",
                new CookieOptions
                {
                    HttpOnly = true,

                    // PROD => true
                    Secure = false,

                    SameSite =
                        SameSiteMode.Strict,

                    Path = "/"
                }
            );

            // =========================
            // RESPONSE
            // =========================

            var response = new auth_Logout
            {
                Message =
                    "Wylogowano pomyślnie"
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