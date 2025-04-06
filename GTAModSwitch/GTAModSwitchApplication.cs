using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia;
using PFXToolKitUI.Avalonia.Configurations;
using PFXToolKitUI.Avalonia.Icons;
using PFXToolKitUI.Avalonia.Services;
using PFXToolKitUI.Avalonia.Services.Colours;
using PFXToolKitUI.Avalonia.Services.Files;
using PFXToolKitUI.Avalonia.Shortcuts.Avalonia;
using PFXToolKitUI.Avalonia.Shortcuts.Dialogs;
using PFXToolKitUI.Avalonia.Themes;
using PFXToolKitUI.Avalonia.Themes.BrushFactories;
using PFXToolKitUI.Avalonia.Toolbars.Toolbars;
using PFXToolKitUI.Configurations;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Persistence;
using PFXToolKitUI.Plugins;
using PFXToolKitUI.Services;
using PFXToolKitUI.Services.ColourPicking;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Services.InputStrokes;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Shortcuts;
using PFXToolKitUI.Themes;
using PFXToolKitUI.Toolbars;

namespace GTAModSwitch;

public class GTAModSwitchApplication : ApplicationPFX, IFrontEndApplication {
    /// <summary>
    /// Gets the avalonia application
    /// </summary>
    public Application Application { get; }
    
    public override PFXToolKitUI.IDispatcher Dispatcher { get; }

    public GTAModSwitchApplication(Application app) {
        this.Application = app ?? throw new ArgumentNullException(nameof(app));

        Dispatcher avd = global::Avalonia.Threading.Dispatcher.UIThread;
        this.Dispatcher = new AvaloniaDispatcherDelegate(avd);

        if (app.ApplicationLifetime is ClassicDesktopStyleApplicationLifetime e) {
            e.Exit += this.OnApplicationExit;
        }
        
        avd.ShutdownFinished += this.OnDispatcherShutDown;
    }

    private void OnDispatcherShutDown(object? sender, EventArgs e) {
        this.StartupPhase = ApplicationStartupPhase.Stopped;
    }
    
    private void OnApplicationExit(object? sender, ControlledApplicationLifetimeExitEventArgs e) {
        this.OnExiting(e.ApplicationExitCode);
    }

    protected override void RegisterServices(ServiceManager manager) {
        // We always want to make sure message dialogs are registered, just in case of errors
        manager.RegisterConstant<IMessageDialogService>(new MessageDialogServiceImpl());
        
        manager.RegisterConstant<ThemeManager>(new ThemeManagerImpl(this.Application));
        manager.RegisterConstant<IconManager>(new IconManagerImpl());
        manager.RegisterConstant<ShortcutManager>(new AvaloniaShortcutManager());
        manager.RegisterConstant<IStartupManager>(new StartupManagerFramePFX());
        base.RegisterServices(manager);
        manager.RegisterConstant<IUserInputDialogService>(new InputDialogServiceImpl());
        manager.RegisterConstant<IColourPickerDialogService>(new ColourPickerDialogServiceImpl());
        manager.RegisterConstant<IFilePickDialogService>(new FilePickDialogServiceImpl());
        manager.RegisterConstant<IConfigurationDialogService>(new ConfigurationDialogServiceImpl());
        manager.RegisterConstant<IInputStrokeQueryDialogService>(new InputStrokeDialogsImpl());
        manager.RegisterConstant<BrushManager>(new BrushManagerImpl());
        manager.RegisterConstant<ToolbarButtonFactory>(new ToolbarButtonFactoryImpl());
        manager.RegisterConstant<IIconPreferences>(new IconPreferencesImpl());
    }

    private class IconPreferencesImpl : IIconPreferences {
        public bool UseAntiAliasing { get; set; }
    }

    protected override async Task OnSetupApplication(IApplicationStartupProgress progress) {
        await progress.ProgressAndSynchroniseAsync("Loading themes...");
        ((ThemeManagerImpl) this.ServiceManager.GetService<ThemeManager>()).SetupBuiltInThemes();
    }

    public bool TryGetActiveWindow([NotNullWhen(true)] out Window? window, bool fallbackToMainWindow = true) {
        if (this.Application.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            return (window = desktop.Windows.FirstOrDefault(x => x.IsActive) ?? (fallbackToMainWindow ? desktop.MainWindow : null)) != null;
        }

        window = null;
        return false;
    }

    protected override void RegisterConfigurations() {
        PersistentStorageManager psm = this.PersistentStorageManager;
        psm.Register<ThemeConfigurationOptions>(new ThemeConfigurationOptionsImpl(), "themes", "themes");
        psm.Register(new ModSwitchConfiguration(), null, "basic");
    }

    protected override async Task OnPluginsLoaded() {
        List<(Plugin, string)> injectable = this.PluginLoader.GetInjectableXamlResources();
        if (injectable.Count > 0) {
            IList<IResourceProvider> resources = this.Application.Resources.MergedDictionaries;
            
            List<string> errorLines = new List<string>();
            foreach ((Plugin plugin, string path) in injectable) {
                int idx = resources.Count;
                try {
                    // adding resource here is the only way to actually get an exception e.g. when file does not exist or is invalid or whatever
                    resources.Add(new ResourceInclude((Uri?) null) { Source = new Uri(path) });
                }
                catch (Exception e) {
                    // remove invalid resource include
                    try {
                        resources.RemoveAt(idx);
                    }
                    catch { /* ignored */ }

                    errorLines.Add(plugin.Name + ": " + path + "\n" + e);
                }
            }

            if (errorLines.Count > 0) {
                string dblNewLine = Environment.NewLine + Environment.NewLine;
                await IMessageDialogService.Instance.ShowMessage(
                    "Error loading plugin XAML", 
                    "One or more plugins' XAML files are invalid. Issues may occur later on.", 
                    string.Join(dblNewLine, errorLines));
            }
        }
    }

    protected override async Task OnApplicationRunning(IApplicationStartupProgress progress, string[] envArgs) {
        if (this.Application.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            await progress.ProgressAndSynchroniseAsync("Startup completed", 1.0);
            await base.OnApplicationRunning(progress, envArgs);
            desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;
        }
        else {
            await base.OnApplicationRunning(progress, envArgs);
        }
    }
    
    protected override string? GetSolutionFileName() {
        return "GTAModSwitch.sln";
    }

    public override string GetApplicationName() {
        return "GTAModSwitch";
    }
}

public class StartupManagerFramePFX : IStartupManager {
    public async Task OnApplicationStartupWithArgs(string[] args) {
        ModSwitchManager? manager = null;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            MainWindow window = new MainWindow();
            manager = window.ModSwitchManager;
            desktop.MainWindow = window;
            window.Show();
            
            desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;

        }
        else if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleView) {
            MainView view = new MainView();
            manager = view.ModSwitchManager;
            singleView.MainView = view;
        }

        if (manager != null) {
            await await ApplicationPFX.Instance.Dispatcher.InvokeAsync(manager.ScanAndProcessExistingModFiles, DispatchPriority.Background);
        }
    }
}