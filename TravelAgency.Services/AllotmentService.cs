using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Services;

public class AllotmentService
{
    private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;

    public AllotmentService(IDbContextFactory<TravelAgencyDbContext> dbf)
        => _dbf = dbf;

    public async Task<List<Allotment>> GetUpcomingAsync(DateTime from, DateTime to, int? cityId = null)
    {
        await using var db = await _dbf.CreateDbContextAsync();

        var q = db.Allotments
            .Include(a => a.Hotel)!.ThenInclude(h => h.City)
            .Include(a => a.RoomTypes)!.ThenInclude(rt => rt.RoomType)
            .Where(a => a.StartDate < to && a.EndDate > from);

        if (cityId != null)
            q = q.Where(a => a.Hotel!.CityId == cityId);

        return await q.AsNoTracking().ToListAsync();
    }

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

                    // Serialize concurrent checks for this line
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT 1 FROM `AllotmentRoomTypes` WHERE `Id` = {allotmentRoomTypeId} FOR UPDATE", ct);

                    var art = await db.AllotmentRoomTypes
                        .Include(x => x.Allotment)
                        .FirstAsync(x => x.Id == allotmentRoomTypeId, ct);

                    var reservedQty = await db.ReservationItems
                        .Where(x => x.AllotmentRoomTypeId == allotmentRoomTypeId &&
                                    x.Reservation!.Status != ReservationStatus.Cancelled)
                        .SumAsync(x => (int?)x.Qty, ct) ?? 0;

                    var available = art.QuantityTotal - art.QuantityCancelled - reservedQty;
                    if (qty > available)
                    {
                        await tx.RollbackAsync(ct);
                        return false;
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
                catch (MySqlConnector.MySqlException ex) when (ex.Number == 1213 || ex.Number == 1205)
                {
                    if (attempt == 3) throw;
                    await Task.Delay(100 * attempt * attempt, ct);
                }
            }

            return false;
        });
    }
}
