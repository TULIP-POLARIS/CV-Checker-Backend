using BusinessLogic.Services;
using DAL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CVApi.Controllers;

[ApiController]
[Route("api/health")]
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    private readonly CVExtractionRunner _runner;

    public HealthController(CVExtractionRunner runner)
    {
        _runner = runner;
    }

    [HttpGet("python")]
    public async Task<IActionResult> Python()
    {
        var result = await _runner.GetPythonHealthAsync();

        if (!result.ScriptFound || !result.AnyHealthyPython3)
        {
            return StatusCode(503, new
            {
                message = "Python extraction runtime is not healthy.",
                result
            });
        }

        return Ok(new
        {
            message = "Python extraction runtime is healthy.",
            result
        });
    }

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
