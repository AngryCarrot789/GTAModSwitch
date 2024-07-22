using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using GTAModSwitch.Commands;

namespace GTAModSwitch;

public partial class MainView : UserControl
{
    private readonly AsyncRelayCommand browseGtaDirCommand;
    private readonly AsyncRelayCommand browseModsFolderCommand;
    private readonly AsyncRelayCommand loadModsCommand;
    private readonly AsyncRelayCommand unloadModsCommand;
    private List<(string Path, bool DidNotMove)>? loadedModPaths;

    public MainView()
    {
        this.InitializeComponent();

        this.browseGtaDirCommand = new AsyncRelayCommand(this.BrowseGtaDirAsync);
        this.browseModsFolderCommand = new AsyncRelayCommand(this.BrowseModsDirAsync);

        this.loadModsCommand = new AsyncRelayCommand(this.LoadModsAsync, () => this.loadedModPaths == null && this.DoGtaAndModsDirsExist());
        this.unloadModsCommand = new AsyncRelayCommand(this.UnloadModsAsync, () => this.loadedModPaths != null && Directory.Exists(this.PART_ModsDir.Text));

        this.Loaded += OnLoadedMainView;

        this.UpdateCheckBoxText();
        this.PART_OverwriteOrIgnoreCheckBox.IsChecked = true;

        return;

        void OnLoadedMainView(object? sender, RoutedEventArgs e)
        {
            this.Loaded -= OnLoadedMainView;

            bool canOpenFolder = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime;
            this.PART_BrowseGtaDirButton.IsEnabled = this.PART_BrowseModsDirButton.IsEnabled = canOpenFolder;
            if (canOpenFolder)
            {
                this.PART_BrowseGtaDirButton.Command = this.browseGtaDirCommand;
                this.PART_BrowseModsDirButton.Command = this.browseModsFolderCommand;
            }

            this.PART_LoadModsButton.Command = this.loadModsCommand;
            this.PART_UnloadModsButton.Command = this.unloadModsCommand;
        }
    }

    private void UpdateCheckBoxText()
    {
        bool isChecked = this.PART_OverwriteOrIgnoreCheckBox.IsChecked ?? false;

        this.PART_OverwriteOrIgnoreCheckBox.Content = isChecked ? "Overwrite existing files" : "Ignore existing files";
    }

    private bool DoGtaAndModsDirsExist() => Directory.Exists(this.PART_GtaDir.Text) && Directory.Exists(this.PART_ModsDir.Text);

    private async Task BrowseGtaDirAsync()
    {
        if (await BrowseFolder(this.PART_GtaDir.Text) is IStorageFolder folder && folder.TryGetLocalPath() is string path)
            this.PART_GtaDir.Text = path;
    }

    private async Task BrowseModsDirAsync()
    {
        if (await BrowseFolder(this.PART_ModsDir.Text) is IStorageFolder folder && folder.TryGetLocalPath() is string path)
            this.PART_ModsDir.Text = path;
    }

    private static async Task<IStorageFolder?> BrowseFolder(string? initialDir)
    {
        if (!(Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop))
            return null;

        IStorageProvider? storage = desktop.MainWindow?.StorageProvider;
        if (storage == null)
            return null;

        IStorageFolder? initialFolder = null;
        try
        {
            initialFolder = !string.IsNullOrWhiteSpace(initialDir) ? await storage.TryGetFolderFromPathAsync(initialDir) : null;
        }
        catch
        {
            // ignored
        }

        IReadOnlyList<IStorageFolder> list = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions() { SuggestedStartLocation = initialFolder, AllowMultiple = false });
        return list.Count == 1 ? list[0] : null;
    }

    private async Task LoadModsAsync()
    {
        if (this.loadedModPaths != null || !this.DoGtaAndModsDirsExist())
        {
            this.RaiseCanLoadUnloadExecute();
            return;
        }

        string? modsDir = this.PART_ModsDir.Text;
        Debug.Assert(!string.IsNullOrWhiteSpace(modsDir), "Mods dir was empty or whitespaces");

        string? gtaDir = this.PART_GtaDir.Text;
        Debug.Assert(!string.IsNullOrWhiteSpace(gtaDir), "Mods dir was empty or whitespaces");

        bool overwriteElseIgnore = this.PART_OverwriteOrIgnoreCheckBox.IsChecked ?? false;

        // Avalonia is pretty shit so no simple way to show errors yet. Would be List<(string, Exception)> for simplicity, string being full path

        List<(string Path, bool DidNotMove)>? paths = new List<(string, bool)>();

        foreach (FileSystemInfo info in new DirectoryInfo(modsDir).EnumerateFileSystemInfos())
        {
            string pathInGtaDir = Path.Combine(gtaDir, info.Name);
            if (info is FileInfo fileInfo)
            {
                if (MoveFile(fileInfo, pathInGtaDir, overwriteElseIgnore, out bool notMoved))
                    paths.Add((pathInGtaDir, notMoved));
            }
            else
            {
                DirectoryInfo dirInfo = (DirectoryInfo) info;
                if (MoveDir(dirInfo, pathInGtaDir, overwriteElseIgnore, out bool notMoved))
                    paths.Add((pathInGtaDir, notMoved));
            }
        }

        this.SetLoadedMods(paths);
    }

    private async Task UnloadModsAsync()
    {
        List<(string Path, bool DidNotMove)>? filesInModsFolder = this.loadedModPaths;
        if (filesInModsFolder == null || !Directory.Exists(this.PART_ModsDir.Text))
        {
            this.RaiseCanLoadUnloadExecute();
            return;
        }

        string? modsDir = this.PART_ModsDir.Text;
        Debug.Assert(!string.IsNullOrWhiteSpace(modsDir), "Mods dir was empty or whitespaces");

        bool overwriteElseIgnore = this.PART_OverwriteOrIgnoreCheckBox.IsChecked ?? false;

        // Avalonia is pretty shit so no simple way to show errors yet. Would be List<(string, Exception)> for simplicity, string being full path
        foreach ((string pathInGtaDir, bool didNotMove) in filesInModsFolder)
        {
            string fileName = Path.GetFileName(pathInGtaDir);
            string pathInModsDir = Path.Combine(modsDir, fileName);
            if (File.Exists(pathInGtaDir))
            {
                MoveFile(pathInGtaDir, pathInModsDir, overwriteElseIgnore, out _);
            }
            else if (Directory.Exists(pathInGtaDir))
            {
                MoveDir(pathInGtaDir, pathInModsDir, overwriteElseIgnore, out _);
            }
        }
        
        this.SetLoadedMods(null);
    }

    private static bool MoveFile(string src, string dst, bool overwrite, out bool didNotMove)
    {
        try
        {
            // Console.WriteLine($"Moving FILE '{src}' --> '{dst}'");
            if (File.Exists(dst) && !overwrite)
            {
                didNotMove = true;
                return true;
            }

            File.Move(src, dst, overwrite);
            didNotMove = false;
            return true;
        }
        catch
        {
            /* ignored */
            didNotMove = false;
            return false;
        }
    }

    private static bool MoveFile(FileInfo src, string dst, bool overwrite, out bool didNotMove) => MoveFile(src.FullName, dst, overwrite, out didNotMove);

    private static bool MoveDir(string src, string dst, bool overwrite, out bool didNotMove)
    {
        if (Directory.Exists(dst))
        {
            if (overwrite)
            {
                try
                {
                    // Console.WriteLine($"Trying to delete directory for overwrite: '{dst}'");
                    Directory.Delete(dst);
                }
                catch
                {
                    didNotMove = false;
                    return false;
                }
            }
            else
            {
                didNotMove = true;
                return true;
            }
        }

        try
        {
            // Console.WriteLine($"Moving DIR '{src}' --> '{dst}'");
            Directory.Move(src, dst);
            didNotMove = false;
            return true;
        }
        catch
        {
            /* ignored */
            didNotMove = false;
            return false;
        }
    }

    private static bool MoveDir(DirectoryInfo src, string dst, bool overwrite, out bool didNotMove) => MoveDir(src.FullName, dst, overwrite, out didNotMove);

    private void SetLoadedMods(List<(string Path, bool DidNotMove)>? paths)
    {
        if (paths != null && this.loadedModPaths != null)
            throw new InvalidOperationException("Mod paths already loaded");

        this.loadedModPaths = paths;
        this.RaiseCanLoadUnloadExecute();
    }

    private void PART_OverwriteOrIgnoreCheckBox_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        this.UpdateCheckBoxText();
    }

    private void OnTextChangedOnDirTextBox(object? sender, TextChangedEventArgs e)
    {
        this.RaiseCanLoadUnloadExecute();
    }

    private void RaiseCanLoadUnloadExecute()
    {
        this.loadModsCommand.RaiseCanExecuteChanged();
        this.unloadModsCommand.RaiseCanExecuteChanged();
    }
}