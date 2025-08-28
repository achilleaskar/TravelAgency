namespace TravelAgency.Domain.Models;

public class ReservationBasketLine
{
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "EUR";

    // For allotment room lines
    public int? AllotmentRoomTypeId { get; set; }

    // Computed total
    public decimal LineTotal => Qty * UnitPrice;
}
