using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Kreator_API.Models;

namespace Kreator_API.Controllers
{
    [ApiController]
    [Route("api/lessonStructure")]
    public class LessonsStructureController : ControllerBase
    {
        private readonly IConfiguration _config;

        public LessonsStructureController(IConfiguration config)
        {
            _config = config;
        }


        [HttpGet("{userId}/{moduleId}")]
        public async Task<IActionResult> GetLessons(string userId, string moduleId)
        {
            Console.WriteLine("========== LESSONS STRUCTURE DEBUG ==========");
            Console.WriteLine($"userId = {userId}");
            Console.WriteLine($"moduleId = {moduleId}");

            var lessons = new List<Lesson_structure>();

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
                role,
                mod_user,
                mod_date
            FROM lessons
            WHERE user_id = @userId
            AND module_id = @moduleId
        ", conn);

                cmd.Parameters.AddWithValue("userId", userId);
                cmd.Parameters.AddWithValue("moduleId", moduleId);

                Console.WriteLine("QUERY EXECUTED");

                await using var reader = await cmd.ExecuteReaderAsync();

                if (!reader.HasRows)
                {
                    Console.WriteLine("⚠️ NO ROWS FOUND");
                }

                while (await reader.ReadAsync())
                {
                    Console.WriteLine("ROW FOUND");

                    var lesson = new Lesson_structure
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        User_Id = reader["user_id"].ToString(),
                        Module_Id = reader["module_id"].ToString(),
                        Module_Name = reader["module_name"].ToString(),
                        Lesson_Id = reader["lesson_id"].ToString(),
                        Lesson_Name = reader["lesson_name"].ToString(),
                        Role = reader["role"].ToString(),
                        Mod_User = reader["mod_user"].ToString(),
                        Mod_Date = Convert.ToDateTime(reader["mod_date"])
                    };

                    Console.WriteLine($"Lesson: {lesson.Lesson_Name}");

                    lessons.Add(lesson);
                }

                Console.WriteLine($"TOTAL LESSONS: {lessons.Count}");
                Console.WriteLine("===================================");

                return Ok(lessons);
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