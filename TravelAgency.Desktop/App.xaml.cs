using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;
using TravelAgency.Data;
using TravelAgency.Desktop.ViewModels;
using TravelAgency.Services;

namespace TravelAgency.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static IHost? HostRef { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        HostRef = Host.CreateDefaultBuilder()
            .ConfigureServices((ctx, services) =>
            {
                // Use a factory to avoid concurrent DbContext usage
                services.AddDbContextFactory<TravelAgencyDbContext>(opt =>
                {
                    var cs = ctx.Configuration.GetConnectionString("MySql")!;
                    opt.UseMySql(cs, ServerVersion.AutoDetect(cs));
                });

                // Register the UI dispatcher (WPF)
                services.AddSingleton<IUiDispatcher, Infrastructure.WpfUiDispatcher>();

                // Lookup cache (shared)
                services.AddSingleton<LookupCacheService>();

                // Services already here...
                services.AddScoped<AllotmentService>();
                services.AddScoped<ReservationService>();
                services.AddScoped<AlertService>();

                // ViewModels
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<ReservationsViewModel>();
                services.AddTransient<AllotmentsViewModel>();
                services.AddTransient<HotelsViewModel>();
                services.AddTransient<CustomersViewModel>();
                services.AddTransient<RoomTypesViewModel>();
                services.AddTransient<CitiesViewModel>();
            })
            .Build();

        base.OnStartup(e);
        // Warm up cache so dropdowns are ready
        var cache = HostRef.Services.GetRequiredService<LookupCacheService>();
        cache.WarmUpAsync().GetAwaiter().GetResult();

        new MainWindow { DataContext = HostRef!.Services.GetRequiredService<MainViewModel>() }.Show();
    }
}