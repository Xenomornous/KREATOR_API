using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Kreator_API.Models_Module;

namespace Kreator_API.Controllers
{
    [ApiController]
    [Route("api/modules")]
    public class ModulesController : ControllerBase
    {
        private readonly IConfiguration _config;

        public ModulesController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetModules(string userId)
        {
            var modules = new List<Module>();

            await using var conn = new NpgsqlConnection(
                _config.GetConnectionString("Postgres"));

            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(
                "SELECT * FROM modules WHERE user_id = @userId",
                conn);

            cmd.Parameters.AddWithValue("userId", userId);

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                modules.Add(new Module
                {
                    Id = reader.GetInt32(0),
                    User_Id = reader.GetString(1),
                    Module_Id = reader.GetString(2),
                    Module_Name = reader.GetString(3),
                    Role = reader.GetString(4),
                    Mod_User = reader.GetString(5),
                    Mod_Date = reader.GetDateTime(6)
                });
            }

            return Ok(modules);
        }
    }
}