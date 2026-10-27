using CreativeCollab.Api.Data;
using CreativeCollab.Api.Endpoints;
using CreativeCollab.Api.Infrastructure;
using CreativeCollab.Api.Services;
using CreativeCollab.Api.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<CreativeDbContext>(options => options.UseSqlite(
    builder.Configuration.GetConnectionString("Creatives") ?? "Data Source=creativecollab.db"));
builder.Services.AddScoped<ICreativeRepository, EfCreativeRepository>();
builder.Services.AddScoped<CreativeService>();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials())); // SignalR needs credentials, so the origin list is explicit, never a wildcard
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateCreativeRequestValidator>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();

if (app.Environment.IsDevelopment())
    await app.InitializeDatabaseAsync();

app.MapCreativeEndpoints();

app.Run();

public partial class Program;
