using System;
using System.IO;
using Avalonia.Markup.Xaml;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia;

namespace GTAModSwitch;

public partial class App : global::Avalonia.Application {
    static App() {
    }

    public App() {
    }

    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
        AvUtils.OnApplicationInitialised();

        ApplicationPFX.InitializeInstance(new GTAModSwitchApplication(this));
    }

    public override async void OnFrameworkInitializationCompleted() {
        base.OnFrameworkInitializationCompleted();
        AvUtils.OnFrameworkInitialised();

        EmptyApplicationStartupProgress progress = new EmptyApplicationStartupProgress();
        string[] envArgs = Environment.GetCommandLineArgs();
        if (envArgs.Length > 0 && Path.GetDirectoryName(envArgs[0]) is string dir && dir.Length > 0) {
            Directory.SetCurrentDirectory(dir);
        }
        
        await ApplicationPFX.InitializeApplication(progress, envArgs);
    }
}