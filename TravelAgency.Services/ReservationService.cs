using Microsoft.EntityFrameworkCore;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Services;

public class ReservationService
{
    private readonly TravelAgencyDbContext db;

    // FIX: assign to the field (was: db = db)
    public ReservationService(TravelAgencyDbContext db) => this.db = db;

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
        db.Reservations.Add(r);
        await db.SaveChangesAsync();
        return r;
    }

    public async Task CancelAsync(int reservationId)
    {
        var r = await db.Reservations.FindAsync(reservationId) ?? throw new InvalidOperationException();
        r.Status = ReservationStatus.Cancelled;
        await db.SaveChangesAsync();
    }
}
