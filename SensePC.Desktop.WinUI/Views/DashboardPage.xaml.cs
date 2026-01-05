using Microsoft.UI.Xaml.Controls;
using SensePC.Desktop.WinUI.ViewModels;

namespace SensePC.Desktop.WinUI.Views
{
    public sealed partial class DashboardPage : Page
    {
        public DashboardViewModel ViewModel { get; } = new DashboardViewModel();

        public DashboardPage()
        {
            this.InitializeComponent();
        }
    }
}
