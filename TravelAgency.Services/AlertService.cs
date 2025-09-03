using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Data;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Services;

public class AlertService
{
    private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
    public AlertService(IDbContextFactory<TravelAgencyDbContext> dbf) => _dbf = dbf;

    public async Task<bool> ReserveRoomsAsync(int reservationId, int allotmentRoomTypeId, int qty, CancellationToken ct = default)
    {
        await using var db = await _dbf.CreateDbContextAsync(ct);

        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

                    // lock the line row to serialize concurrent checks
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT 1 FROM `AllotmentRoomTypes` WHERE `Id` = {allotmentRoomTypeId} FOR UPDATE", ct);

                    var art = await db.AllotmentRoomTypes
                        .Include(x => x.Allotment)
                        .FirstAsync(x => x.Id == allotmentRoomTypeId, ct);

                    var reservedQty = await db.ReservationItems
                        .Where(x => x.AllotmentRoomTypeId == allotmentRoomTypeId &&
                                    x.Reservation!.Status != ReservationStatus.Cancelled)
                        .SumAsync(x => (int?)x.Qty, ct) ?? 0;

                    // NEW: availability per the new model
                    var available = art.QuantityTotal - art.QuantityCancelled - reservedQty;
                    if (qty > available)
                    {
                        await tx.RollbackAsync(ct);
                        return false; // not enough remaining
                    }

                    db.ReservationItems.Add(new ReservationItem
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

                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return true;
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (attempt == 3) throw;
                    await Task.Delay(80 * attempt * attempt, ct);
                }
                catch (MySqlConnector.MySqlException ex) when (ex.Number == 1213 || ex.Number == 1205) // deadlock / lock wait timeout
                {
                    if (attempt == 3) throw;
                    await Task.Delay(100 * attempt * attempt, ct);
                }
            }

            return false;
        });
    }


    public async Task<List<AlertDto>> GetAlertsAsync(
    DateTime today,
    int? hotelId = null,
    string? country = null,
    int? customerId = null,
    string? search = null)
    {
        await using var db = await _dbf.CreateDbContextAsync();

        // Allotment option dues
        var allotQ = db.Allotments
            .Include(a => a.Hotel)!.ThenInclude(h => h.City)
            .Where(a => a.OptionDueDate != null &&
                        a.OptionDueDate <= today.AddDays(7));

        if (hotelId != null) allotQ = allotQ.Where(a => a.HotelId == hotelId);
        if (!string.IsNullOrWhiteSpace(country)) allotQ = allotQ.Where(a => a.Hotel!.City!.Country == country);
        if (!string.IsNullOrWhiteSpace(search))
            allotQ = allotQ.Where(a =>
                a.Hotel!.Name.Contains(search) ||
                a.Hotel!.City!.Country.Contains(search));
        var a = await allotQ.ToListAsync();
        var optionAlerts = await allotQ
            .Select(a => new AlertDto(
                $"Hotel option payment for {a.Hotel!.Name} due {a.OptionDueDate!.Value:dd/MM}",
                a.OptionDueDate!.Value,
                a.OptionDueDate!.Value <= today.AddDays(1) ? Severity.Danger : Severity.Warning,
                $"allotment:{a.Id}",
                a.Hotel!.Name,
                a.Hotel!.City!.Country,
                null
            ))
            .ToListAsync();

        // Reservation deposit/balance dues
        var resQ = db.Reservations
            .Include(r => r.Customer)
            .Where(r => r.Status != ReservationStatus.Cancelled &&
                ((r.DepositDueDate != null && r.DepositDueDate >= today && r.DepositDueDate <= today.AddDays(3)) ||
                 (r.BalanceDueDate != null && r.BalanceDueDate >= today && r.BalanceDueDate <= today.AddDays(3))));

        if (customerId != null) resQ = resQ.Where(r => r.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(search))
            resQ = resQ.Where(r => r.Customer!.Name.Contains(search));

        // If hotel/country filters are set, join to items->allotment->hotel
        if (hotelId != null || !string.IsNullOrWhiteSpace(country))
        {
            resQ = resQ.Where(r => r.Items.Any(i =>
                i.AllotmentRoomTypeId != null &&
                db.AllotmentRoomTypes
                    .Where(x => x.Id == i.AllotmentRoomTypeId)
                    .Any(x => (hotelId == null || x.Allotment!.HotelId == hotelId) &&
                              (country == null || x.Allotment!.Hotel!.City!.Country == country))));
        }

        var resList = await resQ.AsNoTracking().ToListAsync();

        var dep = resList.Where(r => r.DepositDueDate != null)
            .Select(r => new AlertDto(
                $"Reservation deposit for {r.Title} due {r.DepositDueDate:dd/MM}",
                r.DepositDueDate!.Value,
                r.DepositDueDate!.Value <= today.AddDays(1) ? Severity.Danger : Severity.Warning,
                $"reservation:{r.Id}",
                HotelName: null, // optional enrich later if you want top hotel name
                Country: null,
                CustomerName: r.Customer!.Name
            ));

        var bal = resList.Where(r => r.BalanceDueDate != null)
            .Select(r => new AlertDto(
                $"Reservation balance for {r.Title} due {r.BalanceDueDate:dd/MM}",
                r.BalanceDueDate!.Value,
                r.BalanceDueDate!.Value <= today.AddDays(1) ? Severity.Danger : Severity.Warning,
                $"reservation:{r.Id}",
                HotelName: null,
                Country: null,
                CustomerName: r.Customer!.Name
            ));

        return optionAlerts.Concat(dep).Concat(bal)
            .OrderBy(x => x.DueDate)
            .ToList();
    }
}