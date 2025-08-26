using System.ComponentModel.DataAnnotations;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Domain.Entities;

public class Allotment : AuditableEntity
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
