using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;

namespace Kreator_API.Controllers;

[ApiController]
[Route("api/auth")]
public class auth_CsrfController
    : ControllerBase
{
    [HttpGet("csrf")]
    public IActionResult GetCsrfToken()
    {
        var randomBytes =
            new byte[32];

        using var rng =
            RandomNumberGenerator.Create();

        rng.GetBytes(randomBytes);

        string csrfToken =
            Convert.ToBase64String(
                randomBytes
            );

        Response.Cookies.Append(
            "csrfToken",
            csrfToken,
            new CookieOptions
            {
                HttpOnly = false,

                Secure = true,

                SameSite =
                    SameSiteMode.None,

                Path = "/"
            }
        );

        return Ok(new
        {
            csrfToken
        });
    }
}