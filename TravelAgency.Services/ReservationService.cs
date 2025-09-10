// Services/ReservationService.cs
using Microsoft.EntityFrameworkCore;
using TravelAgency.Data;
using TravelAgency.Domain.Dtos;
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Services;



public sealed class ReservationService : IReservationService
{
    private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;

    public ReservationService(IDbContextFactory<TravelAgencyDbContext> dbf)
        => _dbf = dbf;

    // ---------- Lookups (Customers + RoomTypes only) ----------
    public async Task<(IEnumerable<CustomerVM> customers, IEnumerable<RoomTypeVM> roomTypes)> LoadLookupsAsync()
    {
        await using var db = await _dbf.CreateDbContextAsync();

        var customers = await db.Customers
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CustomerVM { Id = c.Id, Name = c.Name })
            .ToListAsync();

        var roomTypes = await db.RoomTypes
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoomTypeVM { Id = r.Id, Name = r.Name })
            .ToListAsync();

        return (customers, roomTypes);
    }

    // ---------- Load ----------
    public async Task<ReservationDto> LoadAsync(int id)
    {
        await using var db = await _dbf.CreateDbContextAsync();

        var r = await db.Reservations
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .AsNoTracking()
            .FirstAsync(x => x.Id == id);

        var dto = new ReservationDto
        {
            Id = r.Id,
            CustomerId = r.CustomerId,
            CheckInUtc = r.CheckIn.ToUniversalTime(),
            CheckOutUtc = r.CheckOut.ToUniversalTime(),

            Lines = r.Lines.Select(l => new ReservationLineDto
            {
                Id = l.Id,
                RoomTypeId = l.RoomTypeId,
                Quantity = l.Quantity,
                PricePerNight = l.PricePerNight,
                Notes = l.Notes
            }).ToList(),

            Payments = r.Payments.OrderBy(p => p.Date).Select(p => new PaymentDto
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

            History = new List<HistoryDto>() // populate if you log reservation updates
        };

        return dto;
    }

    // ---------- Save (create + edit with snapshot upserts) ----------
    public async Task<SaveResult> SaveAsync(ReservationDto dto)
    {
        await using var db = await _dbf.CreateDbContextAsync();

        if (dto.Id == null)
        {
            var entity = new Reservation
            {
                CustomerId = dto.CustomerId,
                CheckIn = dto.CheckInUtc.ToLocalTime().Date,
                CheckOut = dto.CheckOutUtc.ToLocalTime().Date,
                Lines = new List<ReservationLine>(),
                Payments = new List<ReservationPayment>()
            };

            db.Reservations.Add(entity);
            await db.SaveChangesAsync();

            foreach (var l in dto.Lines)
            {
                entity.Lines.Add(new ReservationLine
                {
                    RoomTypeId = l.RoomTypeId,
                    Quantity = l.Quantity,
                    PricePerNight = l.PricePerNight,
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
            return new SaveResult { Success = true, Id = entity.Id };
        }
        else
        {
            var entity = await db.Reservations
                .Include(x => x.Lines)
                .Include(x => x.Payments)
                .FirstAsync(x => x.Id == dto.Id.Value);

            entity.CustomerId = dto.CustomerId;
            entity.CheckIn = dto.CheckInUtc.ToLocalTime().Date;
            entity.CheckOut = dto.CheckOutUtc.ToLocalTime().Date;

            // snapshots (originals only)
            var origLines = entity.Lines.ToDictionary(x => x.Id);
            var keepLineIds = new HashSet<int>();

            var origPays = entity.Payments.ToDictionary(x => x.Id);
            var keepPayIds = new HashSet<int>();

            // lines upsert
            foreach (var l in dto.Lines)
            {
                if (l.Id is int id && origLines.TryGetValue(id, out var e))
                {
                    e.RoomTypeId = l.RoomTypeId;
                    e.Quantity = l.Quantity;
                    e.PricePerNight = l.PricePerNight;
                    e.Notes = l.Notes;
                    keepLineIds.Add(id);
                }
                else
                {
                    entity.Lines.Add(new ReservationLine
                    {
                        RoomTypeId = l.RoomTypeId,
                        Quantity = l.Quantity,
                        PricePerNight = l.PricePerNight,
                        Notes = l.Notes
                    });
                }
            }
            foreach (var e in origLines.Values.Where(x => !keepLineIds.Contains(x.Id)))
                db.Remove(e);

            // payments upsert
            foreach (var p in dto.Payments)
            {
                var kind = Enum.TryParse<PaymentKind>(p.Kind, true, out var k) ? k : PaymentKind.Deposit;

                if (p.Id is int id && origPays.TryGetValue(id, out var e))
                {
                    e.Date = p.DateUtc.ToLocalTime().Date;
                    e.Title = string.IsNullOrWhiteSpace(p.Title) ? "Payment" : p.Title.Trim();
                    e.Kind = k;
                    e.Amount = p.Amount;
                    e.Notes = p.Notes;
                    e.IsVoided = p.IsVoided;
                    e.UpdatedAtUtc = DateTime.UtcNow;
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
            return new SaveResult { Success = true, Id = entity.Id };
        }
    }
}
