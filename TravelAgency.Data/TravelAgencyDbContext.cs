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

        // decimal precision ensured via [Column(TypeName=...)]
    }
}