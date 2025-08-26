using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TravelAgency.Domain.Entities;

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
