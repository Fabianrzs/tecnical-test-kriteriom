using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kriteriom.Notifications.Infrastructure.Persistence;

public class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql("Host=localhost;Database=notifications_db;Username=postgres;Password=postgres")
            .Options;
        return new NotificationsDbContext(options);
    }
}
