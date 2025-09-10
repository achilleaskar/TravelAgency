using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TravelAgency.Domain.Entities;
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

    public DbSet<AllotmentPayment> AllotmentPayments => Set<AllotmentPayment>();
    public DbSet<UpdateLog> UpdateLogs => Set<UpdateLog>();


    public TravelAgencyDbContext(DbContextOptions<TravelAgencyDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<City>().HasIndex(x => new { x.Name, x.Country }).IsUnique();
        b.Entity<Hotel>().HasIndex(x => new { x.Name, x.CityId });
        b.Entity<RoomType>().HasIndex(x => x.Code).IsUnique();

        b.Entity<AllotmentRoomType>().Property(p => p.PricePerNight).HasPrecision(18, 2);
        b.Entity<AllotmentPayment>().Property(p => p.Amount).HasPrecision(18, 2);

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
        b.Entity<AllotmentPayment>().Property(p => p.UpdatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // ενημέρωση AuditableEntity timestamps
        foreach (var e in ChangeTracker.Entries<AuditableEntity>())
        {
            if (e.State == EntityState.Added)
            {
                e.Entity.CreatedAt = now;
                e.Entity.UpdatedAt = now;
            }
            else if (e.State == EntityState.Modified)
            {
                e.Entity.UpdatedAt = now;
            }
            else if (e.State == EntityState.Deleted)
            {
                e.Entity.UpdatedAt = now;
            }
        }

        // Audit μόνο για Allotment & AllotmentRoomType
        foreach (var entry in ChangeTracker.Entries()
                                           .Where(x => x.State == EntityState.Modified &&
                                                       (x.Entity is Allotment || x.Entity is AllotmentRoomType)))
        {
            WriteUpdateLogs(entry, now);
        }

        return await base.SaveChangesAsync(ct);
    }

    private void WriteUpdateLogs(EntityEntry entry, DateTime whenUtc)
    {
        string entityName;
        int entityId;
        int? allotmentId = null;

        switch (entry.Entity)
        {
            case Allotment a:
                entityName = nameof(Allotment);
                entityId = a.Id;
                allotmentId = a.Id;
                break;

            case AllotmentRoomType l:
                entityName = nameof(AllotmentRoomType);
                entityId = l.Id;
                allotmentId = l.AllotmentId;
                break;

            default: return;
        }

        foreach (var p in entry.Properties)
        {
            if (!p.IsModified) continue;
            var propName = p.Metadata.Name;

            // αγνόησε UpdatedAt (θόρυβος)
            if (propName is nameof(AuditableEntity.UpdatedAt)) continue;

            var oldVal = p.OriginalValue?.ToString();
            var newVal = p.CurrentValue?.ToString();
            if (oldVal == newVal) continue;

            UpdateLogs.Add(new UpdateLog
            {
                EntityName = entityName,
                EntityId = entityId,
                AllotmentId = allotmentId,
                PropertyName = propName,            // προσαρμόζω στο δικό σου schema (Field/OldValue/NewValue)
                OldValue = oldVal,
                NewValue = newVal,
                ChangedAtUtc = whenUtc
            });
        }
    }
}