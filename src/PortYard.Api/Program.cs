using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PortYard.Api.Data;
using PortYard.Api.Middleware;
using PortYard.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<YardDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("YardDb")));

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddScoped<ContainerService>();
builder.Services.AddScoped<YardSlotService>();
builder.Services.AddScoped<CustomsHoldService>();
builder.Services.AddScoped<ReportService>();

// Pings the database, not just "is the process running" — an App Service instance whose SQL
// connection has died should fail its probe and get taken out of rotation, not keep serving
// requests that are all going to 500 anyway.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<YardDbContext>();

// No real frontend exists yet, so there's nothing to hardcode an origin for — allowed origins
// are configuration-driven (Cors:AllowedOrigins) and empty by default, which means CORS stays
// off until a real caller's origin is actually known. Configure it via an App Service
// Application Setting (Cors__AllowedOrigins__0=https://...) once a frontend exists.
const string CorsPolicyName = "Configured";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PortYard API",
        Version = "v1",
        Description = "Container yard management for a port terminal: container lifecycle tracking, " +
                       "yard slot capacity, customs holds, and operational reporting."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Migrations are a deployment concern, not a startup concern, past local
// development: running them opportunistically at app startup in a scaled-out
// or multi-instance environment risks two instances racing to migrate the
// same database, and it means a schema problem surfaces as a confusing 500
// on someone's first request instead of a clear failure in the deploy
// pipeline. Development is the one exception, kept for the zero-setup
// "clone and dotnet run" experience the README promises. Everywhere else,
// the schema is expected to already be current — applied by
// `dotnet ef database update` as an explicit deploy step before the app
// ever starts (see the README's deployment section).
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<YardDbContext>();
    db.Database.Migrate();
    YardSeeder.Seed(db);
}
else if (app.Environment.IsStaging())
{
    // Schema is already current (deploy step); demo data is still useful
    // here so a reviewer gets the same "browse a populated yard" experience
    // as Development, without ever seeding a real production database.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<YardDbContext>();
    YardSeeder.Seed(db);
}

app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "PortYard API v1"));

app.UseHttpsRedirection();

app.UseCors(CorsPolicyName);

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program
{
}
