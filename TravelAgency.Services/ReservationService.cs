using Microsoft.EntityFrameworkCore;
using TravelAgency.Data;
using TravelAgency.Domain.Dtos;
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Services;

// Simple option for the lines ComboBox
public sealed class AllotmentOptionVM
{
    public int AllotmentRoomTypeId { get; set; }
    public int AllotmentId { get; set; }
    public string HotelName { get; set; } = "";
    public string RoomTypeName { get; set; } = "";
    public decimal PricePerNight { get; set; }
    public string Display => $"{HotelName} – {RoomTypeName} – {PricePerNight:0.##}";
}

public sealed class ReservationService : IReservationService
{
    private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;

    public ReservationService(IDbContextFactory<TravelAgencyDbContext> dbf) => _dbf = dbf;

    // ------------ Lookups: Customers + ART options (optionally filter by dates) ------------
    public async Task<(IEnumerable<CustomerVM>, IEnumerable<AllotmentOptionVM>)> LoadLookupsAsync(DateTime? checkInUtc = null, DateTime? checkOutUtc = null)
    {
        await using var db = await _dbf.CreateDbContextAsync();

        var customers = await db.Customers
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CustomerVM { Id = c.Id, Name = c.Name })
            .ToListAsync();

        IQueryable<AllotmentRoomType> arts = db.AllotmentRoomTypes
            .Include(x => x.Allotment).ThenInclude(a => a.Hotel)
            .Include(x => x.RoomType)
            .Where(x => x.Allotment.Status == AllotmentStatus.Active);

        if (checkInUtc.HasValue && checkOutUtc.HasValue)
        {
            var ci = checkInUtc.Value.ToLocalTime().Date;
            var co = checkOutUtc.Value.ToLocalTime().Date;

            arts = arts.Where(x =>
                x.Allotment.AllotmentDatePolicy == AllotmentDatePolicy.PartialAllowed
                    ? (x.Allotment.EndDate > ci && x.Allotment.StartDate < co)
                    : (ci >= x.Allotment.StartDate && co <= x.Allotment.EndDate));
        }

        var opts = await arts
            .OrderBy(x => x.Allotment.Hotel!.Name).ThenBy(x => x.RoomType!.Name)
            .Select(x => new AllotmentOptionVM
            {
                AllotmentRoomTypeId = x.Id,
                AllotmentId = x.AllotmentId,
                HotelName = x.Allotment.Hotel!.Name,
                RoomTypeName = x.RoomType!.Name,
                PricePerNight = x.PricePerNight
            })
            .ToListAsync();

        return (customers, opts);
    }

    // ------------ Load one ------------
    public async Task<ReservationDto> LoadAsync(int id)
    {
        await using var db = await _dbf.CreateDbContextAsync();

        var r = await db.Reservations
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .AsNoTracking()
            .FirstAsync(x => x.Id == id);

        return new ReservationDto
        {
            Id = r.Id,
            CustomerId = r.CustomerId,
            CheckInUtc = r.CheckIn.ToUniversalTime(),
            CheckOutUtc = r.CheckOut.ToUniversalTime(),
            Lines = r.Lines.Select(l => new ReservationLineDto
            {
                Id = l.Id,
                AllotmentRoomTypeId = l.AllotmentRoomTypeId,
                Quantity = l.Quantity,
                PricePerNight = l.PricePerNight,
                Notes = l.Notes
            }).ToList(),
            Payments = r.Payments
                .OrderBy(p => p.Date)
                .Select(p => new PaymentDto
                {
                    Id = p.Id,
                    DateUtc = p.Date.ToUniversalTime(),
                    Title = p.Title,
                    Kind = p.Kind.ToString(),
                    Amount = p.Amount,
                    Notes = p.Notes,
                    IsVoided = p.IsVoided,
                    UpdatedAtUtc = p.UpdatedAtUtc
                }).ToList(),
            History = new List<HistoryDto>()
        };
    }

    // ------------ Save (create/edit with availability + policy checks) ------------
    public async Task<SaveResult> SaveAsync(ReservationDto dto)
    {
        await using var db = await _dbf.CreateDbContextAsync();

        var ci = dto.CheckInUtc.ToLocalTime().Date;
        var co = dto.CheckOutUtc.ToLocalTime().Date;
        if (co <= ci) return new SaveResult { Success = false, Message = "Check-out must be after check-in." };

        if (dto.Id == null)
        {
            var entity = new Reservation
            {
                CustomerId = dto.CustomerId,
                CheckIn = ci,
                CheckOut = co,
                Lines = new List<ReservationLine>(),
                Payments = new List<ReservationPayment>()
            };
            db.Reservations.Add(entity);
            await db.SaveChangesAsync();

            foreach (var l in dto.Lines)
            {
                var (ok, msg, price) = await ValidateAndHydrateAsync(db, l.AllotmentRoomTypeId, l.Quantity, ci, co, excludeReservationId: null);
                if (!ok) return new SaveResult { Success = false, Message = msg! };

                entity.Lines.Add(new ReservationLine
                {
                    AllotmentRoomTypeId = l.AllotmentRoomTypeId,
                    Quantity = l.Quantity,
                    PricePerNight = l.PricePerNight > 0 ? l.PricePerNight : price,
                    Notes = l.Notes
                });
            }

            foreach (var p in dto.Payments)
            {
                var kind = Enum.TryParse<PaymentKind>(p.Kind, true, out var k) ? k : PaymentKind.Deposit;
                entity.Payments.Add(new ReservationPayment
                {
                    Date = p.DateUtc.ToLocalTime().Date,
                    Title = string.IsNullOrWhiteSpace(p.Title) ? "Payment" : p.Title.Trim(),
                    Kind = k,
                    Amount = p.Amount,
                    Notes = p.Notes,
                    IsVoided = p.IsVoided,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync();
            List<HistoryDto>? hist = null;
            if (db.UpdateLogs != null)
            {
                var lineIds = await db.ReservationLines
                    .Where(x => x.ReservationId == entity.Id)
                    .Select(x => x.Id)
                    .ToListAsync();

                var payIds = await db.ReservationPayments
                    .Where(x => x.ReservationId == entity.Id)
                    .Select(x => x.Id)
                    .ToListAsync();

                var logs = await db.UpdateLogs
                    .Where(u =>
                        (u.EntityName == nameof(Reservation) && u.EntityId == entity.Id) ||
                        (u.EntityName == nameof(ReservationLine) && lineIds.Contains(u.EntityId)) ||
                        (u.EntityName == nameof(ReservationPayment) && payIds.Contains(u.EntityId)))
                    .OrderByDescending(u => u.ChangedAtUtc)
                    .Take(200)
                    .AsNoTracking()
                    .ToListAsync();

                hist = logs.Select(h => new HistoryDto
                {
                    ChangedAtUtc = h.ChangedAtUtc,
                    ChangedBy = h.ChangedBy,
                    EntityName = h.EntityName,
                    PropertyName = h.PropertyName,
                    OldValue = h.OldValue,
                    NewValue = h.NewValue
                }).ToList();
            }

            return new SaveResult { Success = true, Id = entity.Id, Message = null, History = hist };
        }
        else
        {
            var entity = await db.Reservations
                .Include(x => x.Lines)
                .Include(x => x.Payments)
                .FirstAsync(x => x.Id == dto.Id.Value);

            entity.CustomerId = dto.CustomerId;
            entity.CheckIn = ci;
            entity.CheckOut = co;

            var origLines = entity.Lines.ToDictionary(x => x.Id);
            var keepLineIds = new HashSet<int>();

            foreach (var l in dto.Lines)
            {
                var (ok, msg, price) = await ValidateAndHydrateAsync(db, l.AllotmentRoomTypeId, l.Quantity, ci, co, entity.Id);
                if (!ok) return new SaveResult { Success = false, Message = msg! };

                if (l.Id is int id && origLines.TryGetValue(id, out var e))
                {
                    e.AllotmentRoomTypeId = l.AllotmentRoomTypeId;
                    e.Quantity = l.Quantity;
                    e.PricePerNight = l.PricePerNight > 0 ? l.PricePerNight : price;
                    e.Notes = l.Notes;
                    keepLineIds.Add(id);
                }
                else
                {
                    entity.Lines.Add(new ReservationLine
                    {
                        AllotmentRoomTypeId = l.AllotmentRoomTypeId,
                        Quantity = l.Quantity,
                        PricePerNight = l.PricePerNight > 0 ? l.PricePerNight : price,
                        Notes = l.Notes
                    });
                }
            }
            foreach (var e in origLines.Values.Where(x => !keepLineIds.Contains(x.Id)))
                db.Remove(e);

            var origPays = entity.Payments.ToDictionary(x => x.Id);
            var keepPayIds = new HashSet<int>();

            foreach (var p in dto.Payments)
            {
                var kind = Enum.TryParse<PaymentKind>(p.Kind, true, out var k) ? k : PaymentKind.Deposit;

                if (p.Id is int id && origPays.TryGetValue(id, out var pay))
                {
                    pay.Date = p.DateUtc.ToLocalTime().Date;
                    pay.Title = string.IsNullOrWhiteSpace(p.Title) ? "Payment" : p.Title.Trim();
                    pay.Kind = k;
                    pay.Amount = p.Amount;
                    pay.Notes = p.Notes;
                    pay.IsVoided = p.IsVoided;
                    pay.UpdatedAtUtc = DateTime.UtcNow;
                    keepPayIds.Add(id);
                }
                else
                {
                    entity.Payments.Add(new ReservationPayment
                    {
                        Date = p.DateUtc.ToLocalTime().Date,
                        Title = string.IsNullOrWhiteSpace(p.Title) ? "Payment" : p.Title.Trim(),
                        Kind = k,
                        Amount = p.Amount,
                        Notes = p.Notes,
                        IsVoided = p.IsVoided,
                        UpdatedAtUtc = DateTime.UtcNow
                    });
                }
            }
            foreach (var e in origPays.Values.Where(x => !keepPayIds.Contains(x.Id)))
                db.Remove(e);

            await db.SaveChangesAsync();
            List<HistoryDto>? hist = null;
            if (db.UpdateLogs != null)
            {
                var lineIds = await db.ReservationLines
                    .Where(x => x.ReservationId == entity.Id)
                    .Select(x => x.Id)
                    .ToListAsync();

                var payIds = await db.ReservationPayments
                    .Where(x => x.ReservationId == entity.Id)
                    .Select(x => x.Id)
                    .ToListAsync();

                var logs = await db.UpdateLogs
                    .Where(u =>
                        (u.EntityName == nameof(Reservation) && u.EntityId == entity.Id) ||
                        (u.EntityName == nameof(ReservationLine) && lineIds.Contains(u.EntityId)) ||
                        (u.EntityName == nameof(ReservationPayment) && payIds.Contains(u.EntityId)))
                    .OrderByDescending(u => u.ChangedAtUtc)
                    .Take(200)
                    .AsNoTracking()
                    .ToListAsync();

                hist = logs.Select(h => new HistoryDto
                {
                    ChangedAtUtc = h.ChangedAtUtc,
                    ChangedBy = h.ChangedBy,
                    EntityName = h.EntityName,
                    PropertyName = h.PropertyName,
                    OldValue = h.OldValue,
                    NewValue = h.NewValue
                }).ToList();
            }

            return new SaveResult { Success = true, Id = entity.Id, Message = null, History = hist };
        }
    }

    // Validate date policy + capacity; return (ok, message, defaultPrice)
    private async Task<(bool ok, string? msg, decimal price)> ValidateAndHydrateAsync(
        TravelAgencyDbContext db, int allotmentRoomTypeId, int qty, DateTime ci, DateTime co, int? excludeReservationId)
    {
        var art = await db.AllotmentRoomTypes
            .Include(x => x.Allotment).ThenInclude(a => a.Hotel)
            .FirstAsync(x => x.Id == allotmentRoomTypeId);

        var policyOk = art.Allotment.AllotmentDatePolicy == AllotmentDatePolicy.PartialAllowed
            ? (art.Allotment.EndDate > ci && art.Allotment.StartDate < co)
            : (ci >= art.Allotment.StartDate && co <= art.Allotment.EndDate);

        if (!policyOk)
            return (false, "Dates not allowed by the selected allotment policy.", 0m);

        // Overlap: r.CheckIn < co && r.CheckOut > ci
        var booked = await db.ReservationLines
                .Where(rl => rl.AllotmentRoomTypeId == allotmentRoomTypeId)
                .Where(rl => rl.Reservation.CheckIn < co &&
                             rl.Reservation.CheckOut > ci &&
                             (excludeReservationId == null || rl.ReservationId != excludeReservationId.Value))
                .SumAsync(rl => (int?)rl.Quantity) ?? 0;


        var remaining = art.Quantity - booked;
        if (qty > remaining)
            return (false, $"Insufficient availability (need {qty}, have {remaining}).", 0m);

        return (true, null, art.PricePerNight);
    }
}
