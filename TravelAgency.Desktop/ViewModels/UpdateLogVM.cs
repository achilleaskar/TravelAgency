// Desktop/ViewModels/UpdateLogVM.cs
namespace TravelAgency.Desktop.ViewModels
{
    public class UpdateLogVM
    {
        public DateTime ChangedAtUtc { get; set; }
        public string? ChangedBy { get; set; }

        // IMPORTANT: correct naming aligned to repo
        public string EntityName { get; set; } = "";
        public string PropertyName { get; set; } = "";

        public string? OldValue { get; set; }
        public string? NewValue { get; set; }

        public DateTime ChangedAtLocal => ChangedAtUtc.ToLocalTime();
    }

}
