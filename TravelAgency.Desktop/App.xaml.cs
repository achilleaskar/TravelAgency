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
                var cs = ctx.Configuration.GetConnectionString("MySql")!;
                services.AddDbContext<TravelAgencyDbContext>(opt =>
                    opt.UseMySql(cs, ServerVersion.AutoDetect(cs)));

                // Services already here...
                services.AddScoped<AllotmentService>();
                services.AddScoped<ReservationService>();
                services.AddScoped<AlertService>();
                services.AddTransient<AllotmentsViewModel>();
                services.AddTransient<ReservationsViewModel>();


                // ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddTransient<PlanViewModel>();
                services.AddTransient<RoomTypesViewModel>();
                services.AddTransient<HotelsViewModel>();
                services.AddTransient<CustomersViewModel>();

            })
            .Build();

        base.OnStartup(e);
        new MainWindow { DataContext = HostRef!.Services.GetRequiredService<MainViewModel>() }.Show();
    }
}
