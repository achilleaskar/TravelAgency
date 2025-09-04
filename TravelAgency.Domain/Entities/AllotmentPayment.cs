// Domain/Entities/AllotmentPayment.cs
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Domain.Entities;

public class AllotmentPayment : AuditableEntity
{
    public int Id { get; set; }

    public int AllotmentId { get; set; }
    public Allotment? Allotment { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";

    public PaymentKind Kind { get; set; } = PaymentKind.Other;
    public string Title { get; set; } = "Payment";
    public string? Notes { get; set; }

    public bool IsVoided { get; set; } = false;
}
