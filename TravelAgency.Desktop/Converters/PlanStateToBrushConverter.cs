using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TravelAgency.Desktop.Converters
{
    public enum PlanCellState { FreePaid, FreeDueSoon, Overdue, FullUnpaid, FullPaid, Empty }

    public class PlanStateToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is PlanCellState s ? s switch
            {
                PlanCellState.FreePaid => Brushes.Green,
                PlanCellState.FreeDueSoon => Brushes.Goldenrod,
                PlanCellState.Overdue => Brushes.OrangeRed,
                PlanCellState.FullUnpaid => Brushes.Gray,
                PlanCellState.FullPaid => Brushes.Crimson,
                _ => Brushes.Transparent
            } : Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
