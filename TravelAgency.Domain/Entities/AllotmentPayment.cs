using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Domain.Entities
{
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

        public bool IsVoided { get; set; } = false;
    }
}
