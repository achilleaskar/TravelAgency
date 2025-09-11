// Views/ReservationEditorWindow.xaml.cs
using System.Windows;
using TravelAgency.Desktop.ViewModels;

namespace TravelAgency.Desktop.Views
{
    public partial class ReservationEditorWindow : Window
    {
        private readonly int? _reservationId; 
        private readonly int _customerId;
        private readonly ReservationEditorViewModel _vm;

        public ReservationEditorWindow(ReservationEditorViewModel vm, int? reservationId = null, int customerId = 0)
        {
            InitializeComponent();

            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            _reservationId = reservationId;
            _customerId = customerId;
            DataContext = _vm;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (_reservationId.HasValue)
                    await _vm.InitializeForEditAsync(_reservationId.Value);
                else
                    await _vm.InitializeAsNewForCustomerAsync();
            }
            catch (Exception ex)
            {
                // Show something instead of letting the app die
                MessageBox.Show(this, ex.Message, "Editor init failed", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }
    }
}