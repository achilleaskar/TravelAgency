// Desktop/ViewModels/PaymentVM.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TravelAgency.Desktop.ViewModels
{
    public class PaymentVM : INotifyPropertyChanged
    {
        private DateTime _date = DateTime.Today;
        private string _title = "";
        private string _kind = "Deposit"; // string για απλότητα στο grid
        private decimal _amount;
        private string _currency = "EUR";
        private string? _notes;
        private bool _isVoided;

        public DateTime Date { get => _date; set => Set(ref _date, value); }
        public string Title { get => _title; set => Set(ref _title, value); }
        public string Kind { get => _kind; set => Set(ref _kind, value); }
        public decimal Amount { get => _amount; set => Set(ref _amount, value); }
        public string Currency { get => _currency; set => Set(ref _currency, value); }
        public string? Notes { get => _notes; set => Set(ref _notes, value); }
        public bool IsVoided { get => _isVoided; set => Set(ref _isVoided, value); }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
    }

}
