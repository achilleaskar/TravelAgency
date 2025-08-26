using System.ComponentModel.DataAnnotations;

namespace TravelAgency.Domain.Entities;

public class City
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Country { get; set; } = string.Empty;
}
