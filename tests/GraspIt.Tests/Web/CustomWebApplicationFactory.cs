using GraspIt.Infrastructure.Data;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GraspIt.Tests.Web;

  
/// Custom WebApplicationFactory that replaces PostgreSQL with an in-memory database
/// and stubs out external infrastructure (Hangfire) so integration tests can run
/// without Docker or a live database.
  
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove ALL EF Core and database provider registrations.
            // AddDbContext<GraspItDbContext>(UseNpgsql) registers many internal services.
            // We must remove every EF Core related descriptor to avoid the dual-provider error.
            var efDescriptors = services
                .Where(d =>
                    d.ServiceType.FullName?.Contains("EntityFramework") == true ||
                    d.ServiceType.FullName?.Contains("Microsoft.EntityFrameworkCore") == true ||
                    d.ImplementationType?.FullName?.Contains("Npgsql") == true ||
                    d.ServiceType.FullName?.Contains("Npgsql") == true ||
                    d.ServiceType == typeof(DbContextOptions<GraspItDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(GraspItDbContext))
                .ToList();
            foreach (var d in efDescriptors) services.Remove(d);

            // Remove all Hangfire registrations
            var hangfireDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("Hangfire") == true ||
                            d.ImplementationType?.FullName?.Contains("Hangfire") == true ||
                            d.ServiceType.FullName?.Contains("JobStorage") == true)
                .ToList();
            foreach (var descriptor in hangfireDescriptors)
                services.Remove(descriptor);

            // Add in-memory database
            services.AddDbContext<GraspItDbContext>(options =>
                options.UseInMemoryDatabase("GraspItTestDb_" + Guid.NewGuid()));

            // Add Hangfire with in-memory storage
            services.AddHangfire(config => config.UseMemoryStorage());
            services.AddHangfireServer();
        });
    }
}
