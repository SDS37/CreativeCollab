using CreativeCollab.Api.Data;
using CreativeCollab.Api.Endpoints;
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
builder.Services.AddValidatorsFromAssemblyContaining<CreateCreativeRequestValidator>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    await app.InitializeDatabaseAsync();

app.MapCreativeEndpoints();

app.Run();

public partial class Program;
