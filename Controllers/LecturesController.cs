using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Kreator_API.Models;

namespace Kreator_API.Controllers
{
    [ApiController]
    [Route("api/lectures")]
    public class LecturesController : ControllerBase
    {
        private readonly IConfiguration _config;

        public LecturesController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet("{userId}/{moduleId}/{lessonId}")]
        public async Task<IActionResult> GetLectures(string userId, string moduleId, string lessonId)
        {
            Console.WriteLine("========== LECTURES DEBUG ==========");
            Console.WriteLine($"userId = {userId}");
            Console.WriteLine($"moduleId = {moduleId}");
            Console.WriteLine($"lessonId = {lessonId}");

            var lectures = new List<Lecture>();

            try
            {
                await using var conn = new NpgsqlConnection(
                    _config.GetConnectionString("Postgres"));

                await conn.OpenAsync();
                Console.WriteLine("DB CONNECTED OK");

                var cmd = new NpgsqlCommand(@"
                    SELECT 
                        id,
                        user_id,
                        module_id,
                        module_name,
                        lesson_id,
                        lesson_name,
                        lecture_id,
                        lecture_name,
                        role,
                        mod_user,
                        mod_date
                    FROM lectures
                    WHERE user_id = @userId
                    AND module_id = @moduleId
                    AND lesson_id = @lessonId
                ", conn);

                cmd.Parameters.AddWithValue("userId", userId);
                cmd.Parameters.AddWithValue("moduleId", moduleId);
                cmd.Parameters.AddWithValue("lessonId", lessonId);

                Console.WriteLine("QUERY EXECUTED");

                await using var reader = await cmd.ExecuteReaderAsync();

                if (!reader.HasRows)
                {
                    Console.WriteLine("⚠️ NO ROWS FOUND");
                }

                while (await reader.ReadAsync())
                {
                    var lecture = new Lecture
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        User_Id = reader["user_id"].ToString(),
                        Module_Id = reader["module_id"].ToString(),
                        Module_Name = reader["module_name"].ToString(),
                        Lesson_Id = reader["lesson_id"].ToString(),
                        Lesson_Name = reader["lesson_name"].ToString(),

                        Lecture_Id = reader["lecture_id"].ToString(),
                        Lecture_Name = reader["lecture_name"].ToString(),

                        Role = reader["role"].ToString(),
                        Mod_User = reader["mod_user"].ToString(),
                        Mod_Date = Convert.ToDateTime(reader["mod_date"])
                    };

                    Console.WriteLine($"Lecture: {lecture.Lecture_Name}");

                    lectures.Add(lecture);
                }

                Console.WriteLine($"TOTAL LECTURES: {lectures.Count}");
                Console.WriteLine("===================================");

                return Ok(lectures);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ ERROR:");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);

                return StatusCode(500, ex.Message);
            }
        }
    }
}