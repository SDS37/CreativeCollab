using CreativeCollab.Api.Data;
using CreativeCollab.Api.Endpoints;
using CreativeCollab.Api.Services;
using CreativeCollab.Api.Validation;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ICreativeRepository, InMemoryCreativeRepository>();
builder.Services.AddScoped<CreativeService>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateCreativeRequestValidator>();

var app = builder.Build();

app.MapCreativeEndpoints();

app.Run();

public partial class Program;
