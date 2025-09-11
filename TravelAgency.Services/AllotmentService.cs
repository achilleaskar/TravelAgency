// Services/AllotmentService.cs
using Microsoft.EntityFrameworkCore;
using TravelAgency.Data;
using TravelAgency.Desktop.ViewModels;
using TravelAgency.Domain.Dtos;
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Services
{
    public class AllotmentService : IAllotmentService
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        public AllotmentService(IDbContextFactory<TravelAgencyDbContext> dbf) => _dbf = dbf;

        public async Task<(IEnumerable<CityVM> cities, IEnumerable<HotelVM> hotels, IEnumerable<RoomTypeVM> roomTypes)> LoadLookupsAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            var cities = await db.Cities
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CityVM { Id = c.Id, Name = c.Name })
                .ToListAsync();

            var hotels = await db.Hotels
                .AsNoTracking()
                .OrderBy(h => h.Name)
                .Select(h => new HotelVM { Id = h.Id, Name = h.Name, CityId = h.CityId })
                .ToListAsync();

            var roomTypes = await db.RoomTypes
                .AsNoTracking()
                .OrderBy(r => r.Name)
                .Select(r => new RoomTypeVM { Id = r.Id, Name = r.Name })
                .ToListAsync();

            return (cities, hotels, roomTypes);
        }

        public async Task<AllotmentDto> LoadAsync(int id)
        {
            await using var db = await _dbf.CreateDbContextAsync();

            var a = await db.Allotments
                .Include(x => x.Hotel)
                .Include(x => x.RoomTypes) // AllotmentRoomType
                .Include(x => x.Payments)  // AllotmentPayment
                .AsNoTracking()
                .FirstAsync(x => x.Id == id);

            var dto = new AllotmentDto
            {
                Id = a.Id,
                Title = a.Title,
                CityId = a.Hotel?.CityId ?? 0,     // UI filter
                HotelId = a.HotelId,
                StartDateUtc = a.StartDate.ToUniversalTime(),
                EndDateUtc = a.EndDate.ToUniversalTime(),
                OptionDueUtc = a.OptionDueDate?.ToUniversalTime(),
                AllotmentDatePolicy = a.AllotmentDatePolicy == AllotmentDatePolicy.PartialAllowed ? "PartialAllowed" : "ExactDates",
                Lines = a.RoomTypes.Select(l => new AllotmentLineDto
                {
                    Id = l.Id,
                    RoomTypeId = l.RoomTypeId,
                    Quantity = l.Quantity,
                    PricePerNight = l.PricePerNight,
                    Notes = l.Notes
                }).ToList(),
                Payments = a.Payments
                        .OrderBy(p => p.Date)
                        .Select(p => new PaymentDto
                        {
                            Id = p.Id,                                 // if you use Ids
                            DateUtc = p.Date.ToUniversalTime(),
                            Title = p.Title,
                            Kind = p.Kind.ToString(),
                            Amount = p.Amount,
                            Notes = p.Notes,
                            IsVoided = p.IsVoided,
                            UpdatedAtUtc = p.UpdatedAtUtc              // <-- NEW
                        }).ToList(),
                History = new List<HistoryDto>()
            };

            // Optional UpdateLogs (EntityName / PropertyName)
            if (db.UpdateLogs != null)
            {
                var lineIds = await db.AllotmentRoomTypes
                    .Where(rt => rt.AllotmentId == id)
                    .Select(rt => rt.Id)
                    .ToListAsync();

                var paymentIds = await db.AllotmentPayments
                    .Where(p => p.AllotmentId == id)
                    .Select(p => p.Id)
                    .ToListAsync();

                var logs = await db.UpdateLogs
                    .Where(u =>
                        (u.EntityName == nameof(Allotment) && u.EntityId == id) ||
                        (u.EntityName == nameof(AllotmentRoomType) && lineIds.Contains(u.EntityId)) ||
                        (u.EntityName == nameof(AllotmentPayment) && paymentIds.Contains(u.EntityId)))
                    .OrderByDescending(u => u.ChangedAtUtc)
                    .Take(200)
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var h in logs)
                {
                    dto.History.Add(new HistoryDto
                    {
                        ChangedAtUtc = h.ChangedAtUtc,
                        ChangedBy = h.ChangedBy,
                        EntityName = h.EntityName,          // <-- correct name
                        PropertyName = h.PropertyName,      // <-- correct name
                        OldValue = h.OldValue,
                        NewValue = h.NewValue
                    });
                }
            }

            return dto;
        }

        public async Task<SaveResult> SaveAsync(AllotmentDto dto)
        {
            await using var db = await _dbf.CreateDbContextAsync();

            var policy = dto.AllotmentDatePolicy == "PartialAllowed"
                ? AllotmentDatePolicy.PartialAllowed
                : AllotmentDatePolicy.ExactDates;

            Allotment entity;

            if (dto.Id == null)
            {
                entity = new Allotment
                {
                    Title = dto.Title.Trim(),
                    HotelId = dto.HotelId,
                    StartDate = dto.StartDateUtc.ToLocalTime().Date,
                    EndDate = dto.EndDateUtc.ToLocalTime().Date,
                    OptionDueDate = dto.OptionDueUtc?.ToLocalTime().Date,
                    AllotmentDatePolicy = policy,
                    Status = AllotmentStatus.Active
                };
                db.Allotments.Add(entity);
                await db.SaveChangesAsync();

                foreach (var l in dto.Lines)
                {
                    db.AllotmentRoomTypes.Add(new AllotmentRoomType
                    {
                        AllotmentId = entity.Id,
                        RoomTypeId = l.RoomTypeId,
                        Quantity = l.Quantity,
                        PricePerNight = l.PricePerNight,
                        Notes = l.Notes
                    });
                }

                foreach (var p in dto.Payments)
                {
                    var kind = Enum.TryParse<PaymentKind>(p.Kind, true, out var k) ? k : PaymentKind.Deposit;
                    db.AllotmentPayments.Add(new AllotmentPayment
                    {
                        Id = p.Id ?? 0,
                        AllotmentId = entity.Id,
                        Date = p.DateUtc.ToLocalTime().Date,
                        Title = string.IsNullOrWhiteSpace(p.Title) ? "Payment" : p.Title.Trim(),
                        Kind = kind,
                        Amount = p.Amount,
                        Notes = p.Notes,
                        IsVoided = p.IsVoided,
                        UpdatedAtUtc = DateTime.UtcNow                  // <-- NEW
                    });
                }

                await db.SaveChangesAsync();
                return new SaveResult { Success = true, Id = entity.Id, History = null};
            }
            else
            {
                entity = await db.Allotments
                    .Include(x => x.RoomTypes)
                    .Include(x => x.Payments)
                    .FirstAsync(x => x.Id == dto.Id.Value);

                // ---------- parent fields ----------
                entity.Title = dto.Title.Trim();
                entity.HotelId = dto.HotelId;
                entity.StartDate = dto.StartDateUtc.ToLocalTime().Date;
                entity.EndDate = dto.EndDateUtc.ToLocalTime().Date;
                entity.OptionDueDate = dto.OptionDueUtc?.ToLocalTime().Date;
                entity.AllotmentDatePolicy = policy;

                // ======== SNAPSHOTS (originals before we mutate) ========
                var originalLinesById = entity.RoomTypes.ToDictionary(r => r.Id);
                var originalLinesByRoomType = entity.RoomTypes
                                                    .GroupBy(r => r.RoomTypeId)
                                                    .ToDictionary(g => g.Key, g => g.First()); // in case of uniqueness
                var keepLineIds = new HashSet<int>();

                var originalPaysById = entity.Payments.ToDictionary(p => p.Id);
                var keepPayIds = new HashSet<int>();

                // ---------- RoomTypes upsert ----------
                foreach (var l in dto.Lines)
                {
                    AllotmentRoomType target = null!;

                    // 1) prefer Id-based update if provided
                    if (l.Id is int lid && lid > 0 && originalLinesById.TryGetValue(lid, out var byId))
                    {
                        target = byId;
                        keepLineIds.Add(lid);
                    }
                    // 2) otherwise, try match by natural key (one line per RoomTypeId)
                    else if (originalLinesByRoomType.TryGetValue(l.RoomTypeId, out var byRt))
                    {
                        target = byRt;
                        keepLineIds.Add(byRt.Id);
                    }

                    if (target != null)
                    {
                        // update existing
                        target.RoomTypeId = l.RoomTypeId;
                        target.Quantity = l.Quantity;
                        target.PricePerNight = l.PricePerNight;
                        target.Notes = l.Notes;
                    }
                    else
                    {
                        // insert new
                        entity.RoomTypes.Add(new AllotmentRoomType
                        {
                            RoomTypeId = l.RoomTypeId,
                            Quantity = l.Quantity,
                            PricePerNight = l.PricePerNight,
                            Notes = l.Notes
                        });
                        // do NOT add to keepLineIds (no Id yet); we only delete from the original snapshot
                    }
                }

                // delete only original lines that are no longer present
                foreach (var line in originalLinesById.Values.Where(x => !keepLineIds.Contains(x.Id)))
                    db.AllotmentRoomTypes.Remove(line);

                // ---------- Payments upsert ----------
                foreach (var p in dto.Payments)
                {
                    var kind = Enum.TryParse<PaymentKind>(p.Kind, true, out var k) ? k : PaymentKind.Deposit;

                    if (p.Id is int pid && pid > 0 && originalPaysById.TryGetValue(pid, out var found))
                    {
                        // update existing
                        found.Date = p.DateUtc.ToLocalTime().Date;
                        found.Title = string.IsNullOrWhiteSpace(p.Title) ? "Payment" : p.Title.Trim();
                        found.Kind = kind;
                        found.Amount = p.Amount;
                        found.Notes = p.Notes;
                        found.IsVoided = p.IsVoided;
                        found.UpdatedAtUtc = DateTime.UtcNow;              // <-- NEW
                        keepPayIds.Add(pid);
                    }
                    else
                    {
                        // insert new
                        entity.Payments.Add(new AllotmentPayment
                        {
                            Date = p.DateUtc.ToLocalTime().Date,
                            Title = string.IsNullOrWhiteSpace(p.Title) ? "Payment" : p.Title.Trim(),
                            Kind = kind,
                            Amount = p.Amount,
                            Notes = p.Notes,
                            IsVoided = p.IsVoided,
                            UpdatedAtUtc = DateTime.UtcNow             // <-- NEW

                        });
                        // do NOT add to keepPayIds (new row, no Id yet)
                    }
                }

                // delete only original payments that are no longer present
                foreach (var pay in originalPaysById.Values.Where(x => !keepPayIds.Contains(x.Id)))
                    db.AllotmentPayments.Remove(pay);

                await db.SaveChangesAsync();
                return new SaveResult { Success = true, Id = entity.Id };
            }
        }

    }
}
