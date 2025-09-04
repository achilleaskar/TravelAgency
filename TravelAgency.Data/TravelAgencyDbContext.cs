using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var e in ChangeTracker.Entries())
        {
            if (e.Entity is Allotment a)
            {
                if (e.State == EntityState.Added) { a.CreatedAt = a.UpdatedAt = now; }
                if (e.State == EntityState.Modified) a.UpdatedAt = now;
            }
            if (e.Entity is AllotmentRoomType art)
            {
                if (e.State == EntityState.Added) { art.CreatedAt = art.UpdatedAt = now; }
                if (e.State == EntityState.Modified) art.UpdatedAt = now;
            }
            if (e.Entity is AllotmentPayment pay)
            {
                if (e.State == EntityState.Added) { pay.CreatedAt = now; }
            }
        }

        // Audit μόνο για Allotment & AllotmentRoomType
        foreach (var entry in ChangeTracker.Entries()
                                           .Where(x => x.State == EntityState.Modified &&
                                                       (x.Entity is Allotment || x.Entity is AllotmentRoomType)))
        {
            WriteChangeLogs(entry, now);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    private void WriteChangeLogs(EntityEntry entry, DateTime whenUtc)
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
            case AllotmentRoomType art:
                entityName = nameof(AllotmentRoomType);
                entityId = art.Id;
                allotmentId = art.AllotmentId;
                break;
            default:
                return;
        }

        foreach (var prop in entry.Properties)
        {
            if (!prop.IsModified) continue;

            var name = prop.Metadata.Name;
            if (name is nameof(Allotment.UpdatedAt) or nameof(AllotmentRoomType.UpdatedAt))
                continue;

            var oldVal = prop.OriginalValue?.ToString();
            var newVal = prop.CurrentValue?.ToString();
            if (oldVal == newVal) continue;

            UpdateLogs.Add(new UpdateLog
            {
                EntityName = entityName,
                EntityId = entityId,
                AllotmentId = allotmentId,
                PropertyName = name,
                OldValue = oldVal,
                NewValue = newVal,
                ChangedAt = whenUtc
            });
        }
    }

}