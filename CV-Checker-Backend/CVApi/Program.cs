using BusinessLogic;
using BusinessLogic.Interface;
using BusinessLogic.Services;
using CVApi;
using DAL.Api;
using DAL.Interface;
using DAL.Repository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Services
builder.Services.AddScoped<ICVService, CVService>();
builder.Services.AddScoped<IJobOfferService, JobOfferService>();
builder.Services.AddScoped<ICVComparisonService, CVComparisonService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<Domain.Entities.User>, Microsoft.AspNetCore.Identity.PasswordHasher<Domain.Entities.User>>();
builder.Services.Configure<CVApi.Controllers.JwtOptions>(builder.Configuration.GetSection("Jwt"));

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException("Missing/invalid Jwt:Key (must be at least 32 characters).");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
