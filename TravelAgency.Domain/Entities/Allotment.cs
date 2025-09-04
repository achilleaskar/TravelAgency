// Domain/Entities/Allotment.cs
using System.ComponentModel.DataAnnotations.Schema;
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;
using TravelAgency.Domain.Entities;

namespace TravelAgency.Domain.Entities;

public class Allotment : AuditableEntity
{
    public int Id { get; set; }

    public int HotelId { get; set; }
    public Hotel? Hotel { get; set; }

    public string? Title { get; set; }

    public DateTime StartDate { get; set; }   // inclusive
    public DateTime EndDate { get; set; }   // exclusive προτείνεται
    public DateTime? OptionDueDate { get; set; }

    public AllotmentStatus Status { get; set; } = AllotmentStatus.Active;

    // ΝΕΟ: πολιτική ημερομηνιών (όπως ζήτησες)
    public AllotmentDatePolicy DatePolicy { get; set; } = AllotmentDatePolicy.ExactDates;

    public ICollection<AllotmentRoomType> RoomTypes { get; set; } = new List<AllotmentRoomType>();
    public ICollection<AllotmentPayment> Payments { get; set; } = new List<AllotmentPayment>();

    [NotMapped] public int Nights => Math.Max(0, (EndDate.Date - StartDate.Date).Days);

    // Δυναμικοί υπολογισμοί (μόνο για προβολή)
    [NotMapped] public decimal BaseCost => RoomTypes.Sum(l => l.Quantity * l.PricePerNight * Nights);
    [NotMapped] public decimal PaidTotal => Payments.Where(p => !p.IsVoided).Sum(p => p.Amount);
    [NotMapped] public decimal Balance => BaseCost - PaidTotal;
}
