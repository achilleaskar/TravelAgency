using TravelAgency.Domain.Entities;

public class AllotmentRoomType :AuditableEntity
{
    public int Id { get; set; }

    public int AllotmentId { get; set; }
    public Allotment? Allotment { get; set; }

    public int RoomTypeId { get; set; }
    public RoomType? RoomType { get; set; }

    // ΜΟΝΟ αυτό — χωρίς cancelled
    public int Quantity { get; set; }

    public decimal PricePerNight { get; set; }

    public string Currency { get; set; } = "EUR";
}
