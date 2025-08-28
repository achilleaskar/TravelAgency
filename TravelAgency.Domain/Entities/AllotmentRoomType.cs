using TravelAgency.Domain.Entities;

public class AllotmentRoomType
{
    public int Id { get; set; }

    public int AllotmentId { get; set; }
    public Allotment Allotment { get; set; } = null!;

    public int RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;

    public int QuantityTotal { get; set; }
    public int QuantityCancelled { get; set; }

    public decimal PricePerNight { get; set; }
    public string Currency { get; set; } = "EUR";

    // optional concurrency token
    public byte[]? RowVersion { get; set; }
}
