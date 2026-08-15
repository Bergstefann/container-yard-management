using Microsoft.EntityFrameworkCore;
using PortYard.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<YardDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("YardDb")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<YardDbContext>();
    db.Database.Migrate();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.Run();

public partial class Program
{
}
