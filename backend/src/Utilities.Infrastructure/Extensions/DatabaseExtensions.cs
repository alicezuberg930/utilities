using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Utilities.Infrastructure.Data.Context;

namespace Utilities.Infrastructure.Extensions;

public static class DatabaseExtensions
{
    public static async Task MigrateDatabaseAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
