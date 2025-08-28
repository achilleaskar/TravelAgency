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
        try
        {
            var builder = Host.CreateDefaultBuilder()
                .ConfigureServices((ctx, services) =>
                {
                    // Use a factory to avoid concurrent DbContext usage
                    var cs = ctx.Configuration.GetConnectionString("MySql")!;
                    services.AddDbContextFactory<TravelAgencyDbContext>(opt =>
                    {
                        opt.UseMySql(cs, ServerVersion.AutoDetect(cs), b => b.CommandTimeout(15));
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
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<PlanViewModel>();
                    services.AddTransient<HotelsViewModel>();
                    services.AddTransient<CustomersViewModel>();
                    services.AddTransient<RoomTypesViewModel>();
                    services.AddTransient<CitiesViewModel>();
                });

            HostRef = builder.Build();

            var main = new MainWindow();
            main.Show();

            // now warm the cache (UI is up; if DB is slow you'll still see the app)
            // Warm the cache in background; if it faults, show a dialog
            var cache = HostRef.Services.GetRequiredService<LookupCacheService>();
            _ = Task.Run(async () =>
            {
                try { await cache.WarmUpAsync().ConfigureAwait(false); }
                catch (Exception ex)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                        MessageBox.Show(ex.ToString(), "Cache warm-up failed",
                            MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }
        catch (Exception ex)
        {
            // Show the full startup error so we can see what's wrong
            MessageBox.Show(ex.ToString(), "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }
}