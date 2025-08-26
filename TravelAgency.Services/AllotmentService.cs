using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Data;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Services;

public class AllotmentService
{
    private readonly TravelAgencyDbContext _db;
    public AllotmentService(TravelAgencyDbContext db) => _db = db;

    public Task<List<Allotment>> GetUpcomingAsync(DateTime from, DateTime to, int? cityId = null)
    {
        var q = _db.Allotments
            .Include(a => a.Hotel)!.ThenInclude(h => h.City)
            .Include(a => a.RoomTypes)!.ThenInclude(rt => rt.RoomType)
            .Where(a => a.StartDate < to && a.EndDate > from);
        if (cityId != null) q = q.Where(a => a.Hotel!.CityId == cityId);
        return q.AsNoTracking().ToListAsync();
    }

    public async Task<bool> ReserveRoomsAsync(int reservationId, int allotmentRoomTypeId, int qty)
    {
        var art = await _db.AllotmentRoomTypes.Include(x => x.Allotment).FirstAsync(x => x.Id == allotmentRoomTypeId);
        var reservedQty = await _db.ReservationItems
            .Where(x => x.AllotmentRoomTypeId == allotmentRoomTypeId && x.Reservation!.Status != ReservationStatus.Cancelled)
            .SumAsync(x => (int?)x.Qty) ?? 0;
        var available = art.Quantity - reservedQty;
        if (qty > available) return false;

        var item = new ReservationItem
        {
            ReservationId = reservationId,
            Kind = ReservationItemKind.AllotmentRoom,
            AllotmentRoomTypeId = allotmentRoomTypeId,
            Qty = qty,
            UnitPrice = art.PricePerNight,
            Currency = art.Currency,
            StartDate = art.Allotment!.StartDate,
            EndDate = art.Allotment!.EndDate
        };
        _db.ReservationItems.Add(item);
        await _db.SaveChangesAsync();
        return true;
    }
}

public class ReservationService
{
    private readonly TravelAgencyDbContext _db;
    public ReservationService(TravelAgencyDbContext db) => _db = db;

    public async Task<Reservation> CreateAsync(int customerId, string title, DateTime start, DateTime end,
        DateTime? depositDue = null, DateTime? balanceDue = null)
    {
        var r = new Reservation
        {
            CustomerId = customerId,
            Title = title,
            StartDate = start,
            EndDate = end,
            DepositDueDate = depositDue,
            BalanceDueDate = balanceDue,
            Status = ReservationStatus.Draft
        };
        _db.Reservations.Add(r);
        await _db.SaveChangesAsync();
        return r;
    }

    public async Task CancelAsync(int reservationId)
    {
        var r = await _db.Reservations.FindAsync(reservationId) ?? throw new InvalidOperationException();
        r.Status = ReservationStatus.Cancelled;
        await _db.SaveChangesAsync();
    }
}

public record AlertDto(string Message, DateTime DueDate, Severity Severity, string? Link = null);

public class AlertService
{
    private readonly TravelAgencyDbContext _db;
    public AlertService(TravelAgencyDbContext db) => _db = db;
    public async Task<bool> ReserveRoomsAsync(int reservationId, int allotmentRoomTypeId, int qty, CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

                    // 1) Lock το row του AllotmentRoomType για να σειριοποιήσουμε τα concurrent checks
                    //    Χρησιμοποιούμε raw SQL ώστε να προσθέσουμε FOR UPDATE
                    await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM `AllotmentRoomTypes` WHERE `Id` = {allotmentRoomTypeId} FOR UPDATE", ct);

                    // 2) Φόρτωση entity + υπολογισμός τρεχουσών δεσμεύσεων *εντός του ίδιου transaction*
                    var art = await _db.AllotmentRoomTypes
                        .Include(x => x.Allotment)
                        .FirstAsync(x => x.Id == allotmentRoomTypeId, ct);

                    var reservedQty = await _db.ReservationItems
                        .Where(x => x.AllotmentRoomTypeId == allotmentRoomTypeId && x.Reservation!.Status != ReservationStatus.Cancelled)
                        .SumAsync(x => (int?)x.Qty, ct) ?? 0;

                    var available = art.Quantity - reservedQty;
                    if (qty > available)
                    {
                        await tx.RollbackAsync(ct);
                        return false; // όχι αρκετά διαθέσιμα
                    }

                    // 3) Δημιουργία item
                    _db.ReservationItems.Add(new ReservationItem
                    {
                        ReservationId = reservationId,
                        Kind = ReservationItemKind.AllotmentRoom,
                        AllotmentRoomTypeId = allotmentRoomTypeId,
                        Qty = qty,
                        UnitPrice = art.PricePerNight,
                        Currency = art.Currency,
                        StartDate = art.Allotment!.StartDate,
                        EndDate = art.Allotment!.EndDate
                    });

                    await _db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return true;
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (attempt == 3) throw;
                    await Task.Delay(80 * attempt * attempt, ct); // backoff
                }
                catch (MySqlException ex) when (ex.Number == 1213 || ex.Number == 1205) // deadlock ή lock wait timeout
                {
                    if (attempt == 3) throw;
                    await Task.Delay(100 * attempt * attempt, ct);
                }
            }

            return false;
        });
    }
    public async Task<List<AlertDto>> GetAlertsAsync(DateTime today)
    {
        var upcomingOption = await _db.Allotments
            .Where(a => a.OptionDueDate != null && a.OptionDueDate >= today && a.OptionDueDate <= today.AddDays(3))
           .Select(a => new AlertDto(
                $"Πληρωμή option για {a.Hotel!.Name} έως {a.OptionDueDate:dd/MM}",
                a.OptionDueDate!.Value,
                a.OptionDueDate!.Value <= today.AddDays(1) ? Severity.Danger : Severity.Warning,
                $"allotment:{a.Id}"
            ))
            .ToListAsync();

        var upcomingDeposits = await _db.Reservations
            .Where(r => r.DepositDueDate != null && r.Status != ReservationStatus.Cancelled
                        && r.DepositDueDate >= today && r.DepositDueDate <= today.AddDays(3))
            .Select(r => new AlertDto(
                $"Προκαταβολή για {r.Title} έως {r.DepositDueDate:dd/MM}", r.DepositDueDate!.Value,
                r.DepositDueDate!.Value <= today.AddDays(1) ? Severity.Danger : Severity.Warning,
                $"reservation:{r.Id}"))
            .ToListAsync();

        var upcomingBalances = await _db.Reservations
            .Where(r => r.BalanceDueDate != null && r.Status != ReservationStatus.Cancelled
                        && r.BalanceDueDate >= today && r.BalanceDueDate <= today.AddDays(3))
            .Select(r => new AlertDto(
                $"Εξόφληση για {r.Title} έως {r.BalanceDueDate:dd/MM}", r.BalanceDueDate!.Value,
                r.BalanceDueDate!.Value <= today.AddDays(1) ? Severity.Danger : Severity.Warning,
                $"reservation:{r.Id}"))
            .ToListAsync();

        return upcomingOption.Concat(upcomingDeposits).Concat(upcomingBalances)
            .OrderBy(x => x.DueDate).ToList();
    }
}