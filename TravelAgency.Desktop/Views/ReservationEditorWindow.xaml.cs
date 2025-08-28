// ReservationEditorWindow.xaml.cs
using System.Windows;
using TravelAgency.Desktop.ViewModels;

namespace TravelAgency.Desktop.Views
{
    public partial class ReservationEditorWindow : Window
    {
        public ReservationEditorWindow(object viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // fire initial load if present
            Loaded += (_, __) =>
            {
                (DataContext as  ReservationEditorViewModel)
                    ?.InitializeAsync();
            };
        }
    }
}
