namespace TravelAgency.Desktop.ViewModels;

// Helper DTOs
public class AddLineCandidate
{
    public int AllotmentRoomTypeId { get; set; }
    public string Display { get; set; } = "";
    public int Remaining { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "EUR";
}