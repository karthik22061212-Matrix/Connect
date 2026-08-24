using Connect.Api.Middleware;
using Connect.Application;
using Connect.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Serilog logging to console
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// 2. Add services to the container
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Connect API", Version = "v1" });
});

// Configure CORS for Flutter Web local development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// 3. Configure HTTP request pipeline (Strict Middleware Pipeline Order per docs/06-backend-architecture.md)
// Step 1: Global Exception Handler Middleware (must be first)
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// Step 2: Serilog request logging middleware
app.UseSerilogRequestLogging();

// Step 3: HTTPS redirection
app.UseHttpsRedirection();

// Step 4: CORS
app.UseCors("AllowAll");

// Step 5: Authentication
app.UseAuthentication();

// Step 6: Authorization
app.UseAuthorization();

// Swagger UI (enabled for local dev)
if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Connect API v1"));
}

app.MapControllers();

try
{
    Log.Information("Starting Connect API server...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Connect API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
