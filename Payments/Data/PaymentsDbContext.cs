using Microsoft.EntityFrameworkCore;
using Payments.Models;

namespace Payments.Data;

public class PaymentsDbContext : DbContext
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) 
        : base(options)
    {
    }

    // Таблицы (DbSets)
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Настройка сущности Account
        modelBuilder.Entity<Account>(entity =>
        {
            // Уникальный индекс на UserId. 
            // Это железная гарантия, что у юзера не будет двух счетов.
            entity.HasIndex(a => a.UserId).IsUnique();

            // Явно указываем, что Version используется для контроля конкуренции (CAS)
            entity.Property(a => a.Version).IsConcurrencyToken();
        });

        // Настройка InboxMessage
        modelBuilder.Entity<InboxMessage>(entity =>
        {
            // Здесь ID — это GUID сообщения (OrderId), пришедший из RabbitMQ
            entity.HasKey(i => i.Id);
        });

        // Настройка OutboxMessage
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.IsProcessed).HasDefaultValue(false);
        });
    }
}