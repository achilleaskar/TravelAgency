using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                TryLog($"[AppDomain] {args.ExceptionObject}");
            };

            DispatcherUnhandledException += (_, args) =>
            {
                TryLog($"[Dispatcher] {args.Exception}");
                args.Handled = true; // prevent hard crash
                MessageBox.Show(args.Exception.Message, "Unhandled UI error");
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                TryLog($"[TaskScheduler] {args.Exception}");
                args.SetObserved();
            };

            var builder = Host.CreateDefaultBuilder()
                .ConfigureServices((ctx, services) =>
                {
                    // Use a factory to avoid concurrent DbContext usage
                    var cs = ctx.Configuration.GetConnectionString("MySql")!;
                    services.AddPooledDbContextFactory<TravelAgencyDbContext>(opt =>
                    {
                        opt.UseMySql(cs, ServerVersion.AutoDetect(cs), b => b.CommandTimeout(60));
                        // optional defaults you might like for perf:
                        // opt.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                    }, poolSize: 128); // pick 64–256 typically; adjust to your workload

                    // Register the UI dispatcher (WPF)
                    services.AddSingleton<IUiDispatcher, Infrastructure.WpfUiDispatcher>();

                    // Lookup cache (shared)
                    services.AddSingleton<LookupCacheService>();
                    services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

                    // Services already here...
                    services.AddScoped<IAllotmentService, AllotmentService>();
                    services.AddScoped<IReservationService, ReservationService>();
                    services.AddScoped<ReservationService>();
                    services.AddScoped<AlertService>();

                    // ViewModels
                    services.AddTransient<ReservationsViewModel>();
                    services.AddTransient<ReservationEditorViewModel>();
                    services.AddTransient<AllotmentsViewModel>();
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<PlanViewModel>();
                    services.AddTransient<HotelsViewModel>();
                    services.AddTransient<CustomersViewModel>();
                    services.AddTransient<RoomTypesViewModel>();
                    services.AddTransient<CitiesViewModel>();
                    services.AddTransient<DashboardViewModel>();
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

    private static void TryLog(string msg)
    {
        try { System.Diagnostics.Trace.TraceError(msg); }
        catch { /* ignore */ }
    }
}