using BusinessLogic;
using BusinessLogic.Interface;
using BusinessLogic.Services;
using CVApi;
using DAL.Api;
using DAL.Interface;
using DAL.Repository;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ApiContext>(options =>
    options.UseInMemoryDatabase("cvmatchdb"));
builder.Services.AddControllers();

builder.Services.AddOpenApi();

// Repositories
builder.Services.AddScoped<ICVRepository, CVRepository>();
builder.Services.AddScoped<IJobOfferRepository, JobOfferRepository>();
builder.Services.AddScoped<ICVComparisonRepository, CVComparisonRepository>();

// Services
builder.Services.AddScoped<ICVService, CVService>();
builder.Services.AddScoped<IJobOfferService, JobOfferService>();
builder.Services.AddScoped<ICVComparisonService, CVComparisonService>();

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

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
