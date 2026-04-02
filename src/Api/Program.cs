using Aidly.src.Shared.Infrastructure.Modules;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options => 
{
    options.Filters.Add<Aidly.src.Shared.Infrastructure.Filters.GlobalResponseWrapperFilter>();
});

// Register all modules dynamically
builder.Services.AddModules(builder.Configuration);

// Configure CORS to allow any origin, method, and header for local development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Add global exception handling interceptor
app.UseMiddleware<Aidly.src.Shared.Infrastructure.Middlewares.GlobalExceptionMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseModules();

app.UseAuthorization();

app.MapControllers();
app.MapModuleEndpoints();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<Aidly.src.Modules.Core.Infrastructure.Persistence.CoreDbContext>();
        await db.Database.OpenConnectionAsync();
        Console.WriteLine($"\n--> DATABASE CONNECTION TEST: SUCCESS! Connected to Aiven DB.\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n--> DATABASE CONNECTION TEST FAILED: {ex.Message}\n");
    }
}

app.Run();
