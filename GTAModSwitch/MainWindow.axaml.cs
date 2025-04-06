using System.Threading.Tasks;
using Avalonia.Controls;
using PFXToolKitUI.Avalonia.Themes.Controls;
using PFXToolKitUI.Services.Messaging;

namespace GTAModSwitch;

public partial class MainWindow : WindowEx {
    public ModSwitchManager ModSwitchManager => this.PART_MainView.ModSwitchManager;
    
    public MainWindow() {
        this.InitializeComponent();
    }

    protected override async Task<bool> OnClosingAsync(WindowCloseReason reason) {
        if (this.ModSwitchManager.AreModsLoaded) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Mods are loaded", $"There are still mods loaded. Close anyway?\n(You will be prompted to restore the files when you open the app again)", MessageBoxButton.OKCancel);
            
            // return true = cancelled, false = not canclled (so close)
            return result == MessageBoxResult.OK ? false : true;
        }

        return false;
    }
}