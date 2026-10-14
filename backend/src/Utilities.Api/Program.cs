using Utilities.Api.Extensions;
using Utilities.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApi(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseExceptionHandler();
app.UseForwardedHeaders();
app.UseMiddleware<Utilities.Api.Middleware.SecurityHeadersMiddleware>();
app.UseCors("Frontend");

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateDatabaseAsync();
}

app.MapControllers();

app.Run();

public partial class Program;
