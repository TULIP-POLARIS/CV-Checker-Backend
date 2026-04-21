using BusinessLogic.Interface;
using BusinessLogic.Services;
using CVApi;
using DAL.Api;
using DAL.Interface;
using DAL.Repository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CV Checker API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Database
builder.Services.AddDbContext<ApiContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CvMatchDb")));

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
builder.Services.AddScoped<CVExtractionRunner>();
builder.Services.AddScoped<JobOfferReadinessService>();
builder.Services.AddScoped<CvGenerationService>();

builder.Services.AddScoped<
    Microsoft.AspNetCore.Identity.IPasswordHasher<Domain.Entities.User>,
    Microsoft.AspNetCore.Identity.PasswordHasher<Domain.Entities.User>>();

builder.Services.Configure<CVApi.Controllers.JwtOptions>(
    builder.Configuration.GetSection("Jwt"));

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// JWT validation
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

// SQL ConnectionFactory
var cvMatchDbConnectionString = builder.Configuration.GetConnectionString("CvMatchDb");
if (string.IsNullOrWhiteSpace(cvMatchDbConnectionString))
{
    throw new InvalidOperationException(
        "Missing connection string 'ConnectionStrings:CvMatchDb'. " +
        "Set it via appsettings.json / appsettings.Development.json, " +
        "environment variable 'ConnectionStrings__CvMatchDb', or user-secrets.");
}

builder.Services.AddSingleton<DAL.ISqlConnectionFactory>(_ =>
    new DAL.SqlConnectionFactory(cvMatchDbConnectionString));

var app = builder.Build();

app.UseCors("AllowAll");

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();