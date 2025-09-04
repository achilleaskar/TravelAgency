using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TravelAgency.Desktop.ViewModels;

namespace TravelAgency.Desktop.Views
{
    /// <summary>
    /// Interaction logic for AllotmentEditorWindow.xaml
    /// </summary>
    public partial class AllotmentEditorWindow : Window
    {
        public AllotmentEditorWindow(object vm)
        {
            InitializeComponent();
            DataContext = vm;

            Loaded += async (_, __) =>
            {
                if (DataContext is AllotmentEditorViewModel m)
                    await m.InitializeAsync();
            };
        }
    }
}
