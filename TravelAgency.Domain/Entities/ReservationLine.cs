using System.ComponentModel.DataAnnotations.Schema;

namespace TravelAgency.Domain.Entities;

public class ReservationLine
{
    public int Id { get; set; }

    public int ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;

    // Link to concrete allotment inventory
    public int AllotmentRoomTypeId { get; set; }
    public AllotmentRoomType AllotmentRoomType { get; set; } = null!;

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal PricePerNight { get; set; }

    public string? Notes { get; set; }
}
