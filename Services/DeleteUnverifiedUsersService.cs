using Npgsql;

namespace Kreator_API.Services;

public class DeleteUnverifiedUsersService
    : BackgroundService
{
    private readonly IServiceScopeFactory
        _scopeFactory;

    public DeleteUnverifiedUsersService(
        IServiceScopeFactory scopeFactory
    )
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken
    )
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope =
                    _scopeFactory.CreateScope();

                var config =
                    scope.ServiceProvider
                        .GetRequiredService<IConfiguration>();

                await using var conn =
                    new NpgsqlConnection(
                        config.GetConnectionString(
                            "Postgres"
                        )
                    );

                await conn.OpenAsync(
                    stoppingToken
                );

                var cmd =
                    new NpgsqlCommand(
                        @"
                        DELETE FROM users
                        WHERE
                            is_email_verified = false
                            AND created_at <
                                NOW() - INTERVAL '7 days'
                        ",
                        conn
                    );

                int deleted =
                    await cmd.ExecuteNonQueryAsync(
                        stoppingToken
                    );

                Console.WriteLine(
                    $"Deleted {deleted} unverified users"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    ex.ToString()
                );
            }

            await Task.Delay(
                //TimeSpan.FromSeconds(30),
                TimeSpan.FromHours(24),
                stoppingToken
            );
        }
    }
}