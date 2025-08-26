using System.ComponentModel.DataAnnotations;

namespace TravelAgency.Domain.Entities;

public class Hotel : AuditableEntity
{
    public int Id { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public int CityId { get; set; }
    public City? City { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }
}
