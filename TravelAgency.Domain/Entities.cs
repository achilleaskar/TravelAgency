using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Domain.Entities;

public class City
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Country { get; set; } = string.Empty;
}

public class Hotel
{
    public int Id { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public int CityId { get; set; }
    public City? City { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }
}

public class RoomType
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty; // e.g. DBL, TPL

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty; // Δίκλινο, Τρίκλινο
}

public class Allotment
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public Hotel? Hotel { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty; // optional label

    public DateTime StartDate { get; set; } // inclusive
    public DateTime EndDate { get; set; } // exclusive (ή inclusive, αρκεί να είμαστε συνεπείς)

    public DateTime? OptionDueDate { get; set; } // deadline πληρωμής προς ξενοδοχείο
    public AllotmentStatus Status { get; set; } = AllotmentStatus.Active;

    [Timestamp] public byte[]? RowVersion { get; set; }

    public ICollection<AllotmentRoomType> RoomTypes { get; set; } = new List<AllotmentRoomType>();
}

public class AllotmentRoomType
{
    public int Id { get; set; }
    public int AllotmentId { get; set; }
    public Allotment? Allotment { get; set; }

    public int RoomTypeId { get; set; }
    public RoomType? RoomType { get; set; }

    public int Quantity { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal PricePerNight { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "EUR";

    public bool IsSpecific { get; set; } // true=συγκεκριμένα δωμάτια, false=γενικά

    public bool IsCancelled { get; set; } // σήμανση για μη πωληθέντα
    public DateTime? CancelledAt { get; set; }

    [Timestamp] public byte[]? RowVersion { get; set; } // Optimistic concurrency token
}

public class Customer
{
    public int Id { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal OldBalance { get; set; } // παλιό υπόλοιπο
}

public class Reservation
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty; // π.χ. Καππαδοκία 20-25 Αυγούστου

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public DateTime? DepositDueDate { get; set; }
    public DateTime? BalanceDueDate { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Draft;
    [Timestamp] public byte[]? RowVersion { get; set; }

    public ICollection<ReservationItem> Items { get; set; } = new List<ReservationItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class ReservationItem
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    public ReservationItemKind Kind { get; set; }

    // Allotment link (αν Kind=AllotmentRoom)
    public int? AllotmentRoomTypeId { get; set; }

    public AllotmentRoomType? AllotmentRoomType { get; set; }

    // Service details (αν Kind=Service)
    [MaxLength(200)]
    public string? ServiceName { get; set; } // Bus, Guide, Ferry κ.λπ.

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public int Qty { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal UnitPrice { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "EUR";

    public DateTime? DepositDueDate { get; set; }
    public DateTime? BalanceDueDate { get; set; }

    public bool IsPaid { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    public DateTime PaymentDate { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal Amount { get; set; }

    [MaxLength(50)]
    public string Method { get; set; } = "Cash"; // Card/Bank/etc.

    [MaxLength(300)]
    public string? Notes { get; set; }
}