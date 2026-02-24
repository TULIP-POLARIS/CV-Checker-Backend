using DAL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace BusinessLogic.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("db")]
    public async Task<IActionResult> GetDatabaseHealth([FromServices] ISqlConnectionFactory connectionFactory, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand("SELECT 1", connection);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);

        return Ok(new
        {
            ok = true,
            result,
            dataSource = connection.DataSource,
            database = connection.Database
        });
    }
}


