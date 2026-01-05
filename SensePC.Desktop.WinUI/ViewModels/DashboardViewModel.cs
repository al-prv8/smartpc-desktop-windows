using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SensePC.Desktop.WinUI.ViewModels;

public partial class PCInstance : ObservableObject
{
    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private string status; // "Online", "Offline", "In Session"

    [ObservableProperty]
    private string specs;

    [ObservableProperty]
    private string connectionUrl;
}

public partial class DashboardViewModel : BaseViewModel
{
    public ObservableCollection<PCInstance> PCs { get; } = new();

    public DashboardViewModel()
    {
        Title = "Dashboard";
        LoadPCs();
    }

    private void LoadPCs()
    {
        // Mock data for UI development
        PCs.Add(new PCInstance { Name = "Gaming Rig 1", Status = "Online", Specs = "RTX 4090 • 64GB RAM", ConnectionUrl = "dcv://example1" });
        PCs.Add(new PCInstance { Name = "Workstation Alpha", Status = "Offline", Specs = "RTX A6000 • 128GB RAM", ConnectionUrl = "dcv://example2" });
        PCs.Add(new PCInstance { Name = "VR Space", Status = "In Session", Specs = "RTX 4080 • 32GB RAM", ConnectionUrl = "dcv://example3" });
    }

    [RelayCommand]
    private async Task ConnectAsync(PCInstance pc)
    {
        // Integration with DCVClientManager will go here
        await Task.Delay(100); 
    }
}
