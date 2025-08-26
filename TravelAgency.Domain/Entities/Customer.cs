using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TravelAgency.Domain.Entities;

public class Customer : AuditableEntity
{
    public int Id { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(length: 200)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal OldBalance { get; set; } // παλιό υπόλοιπο
}
