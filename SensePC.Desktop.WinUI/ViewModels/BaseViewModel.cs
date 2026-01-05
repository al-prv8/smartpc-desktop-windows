using CommunityToolkit.Mvvm.ComponentModel;

namespace SensePC.Desktop.WinUI.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    // Common properties like IsBusy
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string title = string.Empty;
}
