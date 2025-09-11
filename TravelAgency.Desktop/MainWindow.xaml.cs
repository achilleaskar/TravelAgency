using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace TravelAgency.Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent(); DataContext = App.HostRef!.Services.GetRequiredService<MainViewModel>();
    }
}