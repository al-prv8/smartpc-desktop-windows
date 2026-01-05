using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SensePC.Desktop.WinUI.ViewModels;

namespace SensePC.Desktop.WinUI.Controls
{
    public sealed partial class PCInstanceCard : UserControl
    {
        public PCInstance ViewModel
        {
            get => (PCInstance)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(PCInstance), typeof(PCInstanceCard), new PropertyMetadata(null));

        public PCInstanceCard()
        {
            this.InitializeComponent();
        }
    }
}
