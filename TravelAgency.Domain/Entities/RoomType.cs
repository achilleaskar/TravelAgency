using System.ComponentModel.DataAnnotations;

namespace TravelAgency.Domain.Entities;

public class RoomType
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty; // e.g. DBL, TPL

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty; // Δίκλινο, Τρίκλινο
}
