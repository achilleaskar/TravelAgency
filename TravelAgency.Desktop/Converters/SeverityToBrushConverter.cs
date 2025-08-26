using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Desktop.Converters
{
    public class SeverityToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is Severity s ? s switch
            {
                Severity.Info => Brushes.DodgerBlue,
                Severity.Warning => Brushes.Goldenrod,
                Severity.Danger => Brushes.Crimson,
                _ => Brushes.Gray
            } : Brushes.Gray;

        public object ConvertBack(object v, Type t, object p, CultureInfo c) =>
            throw new NotSupportedException();
    }
}
