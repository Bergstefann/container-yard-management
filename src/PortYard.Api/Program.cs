using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PortYard.Api.Data;
using PortYard.Api.Middleware;
using PortYard.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<YardDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("YardDb")));

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddScoped<ContainerService>();
builder.Services.AddScoped<YardSlotService>();
builder.Services.AddScoped<CustomsHoldService>();
builder.Services.AddScoped<ReportService>();

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

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<YardDbContext>();
    db.Database.Migrate();
    YardSeeder.Seed(db);
}

app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "PortYard API v1"));

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

public partial class Program
{
}
