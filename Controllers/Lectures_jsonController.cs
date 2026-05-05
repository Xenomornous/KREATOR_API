using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text.Json;

namespace Kreator_API.Controllers
{
    [ApiController]
    [Route("api/lectures_json")]
    public class LecturesJsonController : ControllerBase
    {
        private readonly IConfiguration _config;

        public LecturesJsonController(IConfiguration config)
        {
            _config = config;
        }

        // =========================
        // GET lecture_json
        // =========================
        [HttpGet("{lectureId}")]
        public async Task<IActionResult> GetLectureJson(string lectureId)
        {
            try
            {
                await using var conn = new NpgsqlConnection(
                    _config.GetConnectionString("Postgres"));

                await conn.OpenAsync();

                var cmd = new NpgsqlCommand(@"
                    SELECT lecture_id, lecture_name, lecture_json
                    FROM lectures_json
                    WHERE lecture_id = @lectureId
                    LIMIT 1
                ", conn);

                cmd.Parameters.AddWithValue("lectureId", lectureId);

                await using var reader = await cmd.ExecuteReaderAsync();

                if (!reader.Read())
                    return NotFound("Lecture not found");

                var result = new
                {
                    lecture_id = reader["lecture_id"].ToString(),
                    lecture_name = reader["lecture_name"].ToString(),
                    lecture_json = reader["lecture_json"].ToString()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500, ex.Message);
            }
        }

        // =========================
        // POST / SAVE lecture_json
        // =========================
        [HttpPost]
        public async Task<IActionResult> SaveLectureJson([FromBody] LectureJsonDto dto)
        {
            try
            {
                await using var conn = new NpgsqlConnection(
                    _config.GetConnectionString("Postgres"));

                await conn.OpenAsync();

                var cmd = new NpgsqlCommand(@"
                    INSERT INTO lectures_json
                    (lecture_id, lecture_name, lecture_json, mod_user, mod_date)
                    VALUES
                    (@lecture_id, @lecture_name, @lecture_json::jsonb, @mod_user, now())
                    ON CONFLICT (lecture_id)
                    DO UPDATE SET
                        lecture_name = @lecture_name,
                        lecture_json = @lecture_json::jsonb,
                        mod_user = @mod_user,
                        mod_date = now();
                ", conn);

                cmd.Parameters.AddWithValue("lecture_id", dto.lecture_id);
                cmd.Parameters.AddWithValue("lecture_name", dto.lecture_name);
                cmd.Parameters.AddWithValue("lecture_json", dto.lecture_json);
                cmd.Parameters.AddWithValue("mod_user", dto.mod_user);


                await cmd.ExecuteNonQueryAsync();

                return Ok("Saved");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500, ex.Message);
            }
        }
    }

    // =========================
    // DTO
    // =========================
    public class LectureJsonDto
    {
        public string lecture_id { get; set; }
        public string lecture_name { get; set; }
        public string mod_user { get; set; }
        public string lecture_json { get; set; }
    }
}