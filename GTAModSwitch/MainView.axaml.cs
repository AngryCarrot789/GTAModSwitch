using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Utils.Commands;

namespace GTAModSwitch;

public partial class MainView : UserControl {
    public ModSwitchManager ModSwitchManager { get; } = new ModSwitchManager();
    
    private readonly AutoUpdateAndEventPropertyBinder<ModSwitchConfiguration> gtaDirBinder = new AutoUpdateAndEventPropertyBinder<ModSwitchConfiguration>(TextBox.TextProperty, nameof(ModSwitchConfiguration.Instance.GtaDirChanged), x => ((TextBox) x.Control).Text = x.Model.GtaDir, x => x.Model.GtaDir = ((TextBox) x.Control).Text ?? "");
    private readonly AutoUpdateAndEventPropertyBinder<ModSwitchConfiguration> gtaModsDirBinder = new AutoUpdateAndEventPropertyBinder<ModSwitchConfiguration>(TextBox.TextProperty, nameof(ModSwitchConfiguration.Instance.GtaModsDirChanged), x => ((TextBox) x.Control).Text = x.Model.GtaModsDir, x => x.Model.GtaModsDir = ((TextBox) x.Control).Text ?? "");
    private readonly AutoUpdateAndEventPropertyBinder<ModSwitchConfiguration> overwriteBinder = new AutoUpdateAndEventPropertyBinder<ModSwitchConfiguration>(ToggleButton.IsCheckedProperty, nameof(ModSwitchConfiguration.Instance.OverwriteOrIgnoreChanged), x => {
        bool isChecked = x.Model.OverwriteOrIgnore;
        ((CheckBox) x.Control).IsChecked = isChecked;
        ((CheckBox) x.Control).Content = isChecked ? "Mode: overwrite existing files" : "Mode: ignore existing files";
    }, x => x.Model.OverwriteOrIgnore = ((CheckBox) x.Control).IsChecked ?? false);
    
    private readonly AsyncRelayCommand browseGtaDirCommand;
    private readonly AsyncRelayCommand browseModsFolderCommand;
    private readonly AsyncRelayCommand loadModsCommand;
    private readonly AsyncRelayCommand unloadModsCommand;

    public MainView() {
        this.InitializeComponent();

        this.browseGtaDirCommand = new AsyncRelayCommand(this.ModSwitchManager.BrowseNewGtaDirAsync);
        this.browseModsFolderCommand = new AsyncRelayCommand(this.ModSwitchManager.BrowseNewModsDirAsync);

        this.loadModsCommand = new AsyncRelayCommand(this.ModSwitchManager.LoadModsAsync, () => !this.ModSwitchManager.AreModsLoaded && this.ModSwitchManager.DoGtaAndModsDirsExist());
        this.unloadModsCommand = new AsyncRelayCommand(this.ModSwitchManager.UnloadModsAsync, () => this.ModSwitchManager.AreModsLoaded && this.ModSwitchManager.DoGtaAndModsDirsExist());

        this.ModSwitchManager.AreModsLoadedChanged += sender => {
            this.loadModsCommand.RaiseCanExecuteChanged();
            this.unloadModsCommand.RaiseCanExecuteChanged();
        };
        
        this.Loaded += OnLoadedMainView;

        return;

        void OnLoadedMainView(object? sender, RoutedEventArgs e) {
            this.Loaded -= OnLoadedMainView;

            bool canOpenFolder = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime;
            this.PART_BrowseGtaDirButton.IsEnabled = this.PART_BrowseModsDirButton.IsEnabled = canOpenFolder;
            if (canOpenFolder) {
                this.PART_BrowseGtaDirButton.Command = this.browseGtaDirCommand;
                this.PART_BrowseModsDirButton.Command = this.browseModsFolderCommand;
            }

            this.PART_LoadModsButton.Command = this.loadModsCommand;
            this.PART_UnloadModsButton.Command = this.unloadModsCommand;
        }
    }
    
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);
        this.gtaDirBinder.Attach(this.PART_GtaDir, ModSwitchConfiguration.Instance);
        this.gtaModsDirBinder.Attach(this.PART_ModsDir, ModSwitchConfiguration.Instance);
        this.overwriteBinder.Attach(this.PART_OverwriteOrIgnoreCheckBox, ModSwitchConfiguration.Instance);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnDetachedFromVisualTree(e);
        this.gtaDirBinder.Detach();
        this.gtaModsDirBinder.Detach();
        this.overwriteBinder.Detach();
    }

    private void OnTextChangedOnDirTextBox(object? sender, TextChangedEventArgs e) {
        this.RaiseCanLoadUnloadExecute();
    }

    private void RaiseCanLoadUnloadExecute() {
        this.loadModsCommand.RaiseCanExecuteChanged();
        this.unloadModsCommand.RaiseCanExecuteChanged();
    }
}