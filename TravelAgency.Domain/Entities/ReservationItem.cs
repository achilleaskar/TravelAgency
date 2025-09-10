using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Domain.Entities;

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

    public DateTime? DepositDueDate { get; set; }
    public DateTime? BalanceDueDate { get; set; }

    public bool IsPaid { get; set; }
}
