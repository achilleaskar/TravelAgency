// Desktop/ViewModels/AllotmentLineVM.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TravelAgency.Domain.Dtos;     // AllotmentDto, AllotmentLineDto, PaymentDto, HistoryDto

namespace TravelAgency.Desktop.ViewModels
{
    #region Child VMs

    public class AllotmentLineVM : INotifyPropertyChanged
    {
        public int? Id { get; set; }
        private RoomTypeVM? _roomType;
        private int _roomTypeId; // <-- bind to this from XAML
        private int _quantity;
        private decimal _pricePerNight;
        private string _currency = "EUR";
        private string? _notes;
        private int _nights;
        private decimal _lineTotal;

        public int RoomTypeId
        {
            get => _roomType?.Id ?? _roomTypeId;
            set
            {
                if (_roomTypeId == value) return;
                _roomTypeId = value;
                ResolveRoomTypeFromId?.Invoke(value);   // keeps RoomType in sync
                OnPropertyChanged(nameof(RoomTypeId));
            }
        }


        // Parent VM should assign this (see HookLine in the editor VM)
        internal Action<int>? ResolveRoomTypeFromId { get; set; }

        public RoomTypeVM? RoomType
        {
            get => _roomType;
            set
            {
                if (Set(ref _roomType, value))
                {
                    _roomTypeId = value?.Id ?? 0;   // keep in sync
                    Recalc();
                    OnPropertyChanged(nameof(RoomTypeId));
                }
            }
        }

        public int Quantity { get => _quantity; set { if (Set(ref _quantity, value)) Recalc(); } }
        public decimal PricePerNight { get => _pricePerNight; set { if (Set(ref _pricePerNight, value)) Recalc(); } }
        public string Currency { get => _currency; set { if (Set(ref _currency, value)) Recalc(); } }
        public string? Notes { get => _notes; set => Set(ref _notes, value); }

        public decimal LineTotal { get => _lineTotal; private set => Set(ref _lineTotal, value); }

        public void SetNightsForLineTotal(int nights) { _nights = nights; Recalc(); }

        private void Recalc() => LineTotal = _nights * _pricePerNight * _quantity;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #endregion
}
