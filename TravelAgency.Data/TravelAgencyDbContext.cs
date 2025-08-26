using Microsoft.EntityFrameworkCore;
using TravelAgency.Domain.Entities;

namespace TravelAgency.Data;

public class TravelAgencyDbContext : DbContext
{
    public DbSet<City> Cities => Set<City>();
    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<Allotment> Allotments => Set<Allotment>();
    public DbSet<AllotmentRoomType> AllotmentRoomTypes => Set<AllotmentRoomType>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationItem> ReservationItems => Set<ReservationItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<UpdateLog> UpdateLogs => Set<UpdateLog>();


    public TravelAgencyDbContext(DbContextOptions<TravelAgencyDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<City>().HasIndex(x => new { x.Name, x.Country }).IsUnique();
        b.Entity<Hotel>().HasIndex(x => new { x.Name, x.CityId });
        b.Entity<RoomType>().HasIndex(x => x.Code).IsUnique();

        b.Entity<Allotment>().HasIndex(x => x.OptionDueDate);
        b.Entity<Allotment>().HasMany(x => x.RoomTypes).WithOne(x => x.Allotment).HasForeignKey(x => x.AllotmentId);

        b.Entity<AllotmentRoomType>().HasIndex(x => new { x.AllotmentId, x.RoomTypeId }).IsUnique();

        b.Entity<Customer>().HasIndex(x => x.Name);

        b.Entity<Reservation>().HasIndex(x => x.DepositDueDate);
        b.Entity<Reservation>().HasIndex(x => x.BalanceDueDate);
        b.Entity<Reservation>().HasMany(x => x.Items).WithOne(x => x.Reservation).HasForeignKey(x => x.ReservationId);

        b.Entity<ReservationItem>().HasIndex(x => x.AllotmentRoomTypeId);

        // Map AuditableEntity properties for each derived type
        b.Entity<Customer>().Property(x => x.Notes).HasMaxLength(2000);
        b.Entity<Hotel>().Property(x => x.Notes).HasMaxLength(2000);
        b.Entity<Allotment>().Property(x => x.Notes).HasMaxLength(2000);
        b.Entity<Reservation>().Property(x => x.Notes).HasMaxLength(2000);

        b.Entity<UpdateLog>().HasIndex(x => new { x.EntityType, x.EntityId, x.ChangedAt });

    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        // set CreatedAt/UpdatedAt
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = utcNow;
                entry.Entity.UpdatedAt = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
            }
        }

        // gather change logs
        var logs = new List<UpdateLog>();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Modified) continue;
            if (entry.Entity is not AuditableEntity auditable) continue;

            var entityType = entry.Entity.GetType().Name;
            // Try to get an integer id property named "Id"
            var idProp = entry.Property("Id");
            var entityId = idProp?.CurrentValue is int i ? i : 0;

            foreach (var prop in entry.Properties)
            {
                if (!prop.IsModified) continue;
                if (prop.Metadata.Name is "UpdatedAt" or "CreatedAt") continue; // skip auto fields

                var oldVal = prop.OriginalValue?.ToString();
                var newVal = prop.CurrentValue?.ToString();
                if (oldVal == newVal) continue;

                logs.Add(new UpdateLog
                {
                    EntityType = entityType,
                    EntityId = entityId,
                    ChangedAt = utcNow,
                    ChangedBy = null, // plug user later
                    Field = prop.Metadata.Name,
                    OldValue = oldVal,
                    NewValue = newVal
                });
            }
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        if (logs.Count > 0)
        {
            UpdateLogs.AddRange(logs);
            await base.SaveChangesAsync(cancellationToken);
        }
        return result;
    }

}