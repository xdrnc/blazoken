using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OAuthDemo.ResourceApi.Data;
using OAuthDemo.ResourceApi.Models;

namespace OAuthDemo.ResourceApi.Data;

public class DataSeeder : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public DataSeeder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ResourceDbContext>();

        // Ensure database is created
        await context.Database.EnsureCreatedAsync(cancellationToken);

        // Seed test data for each client
        if (!await context.ClientData.AnyAsync(cancellationToken))
        {
            var testData = new List<ClientData>
            {
                // Client A data
                new ClientData { ClientId = "client-a", Key = "config", Value = "Client A Configuration", CreatedAt = DateTime.UtcNow },
                new ClientData { ClientId = "client-a", Key = "setting1", Value = "Client A Setting 1", CreatedAt = DateTime.UtcNow },
                new ClientData { ClientId = "client-a", Key = "setting2", Value = "Client A Setting 2", CreatedAt = DateTime.UtcNow },

                // Client B data
                new ClientData { ClientId = "client-b", Key = "config", Value = "Client B Configuration", CreatedAt = DateTime.UtcNow },
                new ClientData { ClientId = "client-b", Key = "preference", Value = "Client B Preference", CreatedAt = DateTime.UtcNow },

                // Client C data
                new ClientData { ClientId = "client-c", Key = "config", Value = "Client C Configuration", CreatedAt = DateTime.UtcNow },
                new ClientData { ClientId = "client-c", Key = "admin_data", Value = "Client C Admin Data", CreatedAt = DateTime.UtcNow },
                new ClientData { ClientId = "client-c", Key = "secret", Value = "Client C Secret Info", CreatedAt = DateTime.UtcNow }
            };

            await context.ClientData.AddRangeAsync(testData, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}