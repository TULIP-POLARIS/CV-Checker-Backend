var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var cvMatchDbConnectionString = builder.Configuration.GetConnectionString("CvMatchDb");
if (string.IsNullOrWhiteSpace(cvMatchDbConnectionString))
{
    throw new InvalidOperationException(
        "Missing connection string 'ConnectionStrings:CvMatchDb'. " +
        "Set it via appsettings.json / appsettings.Development.json, " +
        "environment variable 'ConnectionStrings__CvMatchDb', or user-secrets.");
}

builder.Services.AddSingleton<DAL.ISqlConnectionFactory>(_ => new DAL.SqlConnectionFactory(cvMatchDbConnectionString));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
