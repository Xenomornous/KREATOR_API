using Kreator_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Claims;

[ApiController]
[Route("api/sidebar")]
[Authorize]
public class SidebarController : ControllerBase
{
    private readonly IConfiguration _config;

    public SidebarController(
        IConfiguration config
    )
    {
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> GetSidebar()
    {
        try
        {
            string? userId =
                User.FindFirst(
                    ClaimTypes.NameIdentifier
                )?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var modules =
                new Dictionary<
                    string,
                    SidebarModule
                >();

            await using var conn =
                new NpgsqlConnection(
                    _config.GetConnectionString(
                        "Postgres"
                    )
                );

            await conn.OpenAsync();

            // MODULES
            var modulesCmd =
    new NpgsqlCommand(
        @"
        SELECT
            module_id,
            module_name
        FROM modules
        WHERE user_id = @userId
        ",
        conn
    );

            modulesCmd.Parameters.AddWithValue(
                "userId",
                userId
            );

            await using var modulesReader =
                await modulesCmd.ExecuteReaderAsync();

            while (
                await modulesReader.ReadAsync()
            )
            {
                string moduleId =
                    modulesReader["module_id"]
                        .ToString()!;

                modules[moduleId] =
                    new SidebarModule
                    {
                        Id = moduleId,

                        Name =
                            modulesReader["module_name"]
                                .ToString()!
                    };
            }

            await modulesReader.CloseAsync();

            var lessonsCmd =
    new NpgsqlCommand(
        @"
        SELECT
            module_id,
            lesson_id,
            lesson_name
        FROM lessons
        WHERE user_id = @userId
        ",
        conn
    );

            lessonsCmd.Parameters.AddWithValue(
                "userId",
                userId
            );

            await using var lessonsReader =
                await lessonsCmd.ExecuteReaderAsync();

            while (
                await lessonsReader.ReadAsync()
            )
            {
                string moduleId =
                    lessonsReader["module_id"]
                        .ToString()!;

                if (
                    !modules.ContainsKey(
                        moduleId
                    )
                )
                {
                    continue;
                }

                modules[moduleId]
                    .Lessons
                    .Add(
                        new SidebarLesson
                        {
                            Id =
                                lessonsReader["lesson_id"]
                                    .ToString()!,

                            Name =
                                lessonsReader["lesson_name"]
                                    .ToString()!
                        }
                    );
            }

            await lessonsReader.CloseAsync();

            var lecturesCmd =
    new NpgsqlCommand(
        @"
        SELECT
            module_id,
            lesson_id,
            lecture_id,
            lecture_name
        FROM lectures
        WHERE user_id = @userId
        ",
        conn
    );

            lecturesCmd.Parameters.AddWithValue(
                "userId",
                userId
            );

            await using var lecturesReader =
                await lecturesCmd.ExecuteReaderAsync();

            while (
                await lecturesReader.ReadAsync()
            )
            {
                string moduleId =
                    lecturesReader["module_id"]
                        .ToString()!;

                string lessonId =
                    lecturesReader["lesson_id"]
                        .ToString()!;

                if (
                    !modules.ContainsKey(
                        moduleId
                    )
                )
                {
                    continue;
                }

                var lesson =
                    modules[moduleId]
                        .Lessons
                        .FirstOrDefault(
                            x => x.Id == lessonId
                        );

                if (lesson == null)
                {
                    continue;
                }

                lesson.Lectures.Add(
                    new SidebarLecture
                    {
                        Id =
                            lecturesReader["lecture_id"]
                                .ToString()!,

                        Name =
                            lecturesReader["lecture_name"]
                                .ToString()!
                    }
                );
            }

            await lecturesReader.CloseAsync();

            return Ok(
            modules.Values.ToList()
        );
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);

            return StatusCode(
                500,
                ex.Message
            );
        }
    }
}
