using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia;
using PFXToolKitUI.Services.Messaging;

namespace GTAModSwitch;

public delegate void ModSwitchManagerAreModsLoadedChangedEventHandler(ModSwitchManager sender);

public class ModSwitchManager {
    private readonly ModSwitchConfiguration options;
    private List<(string Path, bool DidNotMove)>? loadedModPaths;

    public bool AreModsLoaded => this.loadedModPaths != null;

    public event ModSwitchManagerAreModsLoadedChangedEventHandler? AreModsLoadedChanged;

    public ModSwitchManager() {
        this.options = ModSwitchConfiguration.Instance;
    }

    public bool DoGtaAndModsDirsExist() => Directory.Exists(this.options.GtaDir) && Directory.Exists(this.options.GtaModsDir);

    public async Task LoadModsAsync() {
        if (this.loadedModPaths != null || !this.DoGtaAndModsDirsExist()) {
            return;
        }

        string modsDir = this.options.GtaModsDir;
        Debug.Assert(!string.IsNullOrWhiteSpace(modsDir), "Mods dir was empty or whitespaces");

        string gtaDir = this.options.GtaDir;
        Debug.Assert(!string.IsNullOrWhiteSpace(gtaDir), "Mods dir was empty or whitespaces");

        bool overwriteElseIgnore = this.options.OverwriteOrIgnore;

        List<(string Path, bool DidNotMove)> paths = new List<(string, bool)>();

        foreach (FileSystemInfo info in new DirectoryInfo(modsDir).EnumerateFileSystemInfos()) {
            string pathInGtaDir = Path.Combine(gtaDir, info.Name);
            if (info is FileInfo fileInfo) {
                if (MoveFile(fileInfo, pathInGtaDir, overwriteElseIgnore, out bool notMoved))
                    paths.Add((pathInGtaDir, notMoved));
            }
            else {
                DirectoryInfo dirInfo = (DirectoryInfo) info;
                if (MoveDir(dirInfo, pathInGtaDir, overwriteElseIgnore, out bool notMoved))
                    paths.Add((pathInGtaDir, notMoved));
            }
        }

        this.SetLoadedMods(paths);
    }

    public async Task UnloadModsAsync() {
        if (this.loadedModPaths == null || !Directory.Exists(this.options.GtaModsDir)) {
            return;
        }

        string modsDir = this.options.GtaModsDir;
        Debug.Assert(!string.IsNullOrWhiteSpace(modsDir), "Mods dir was empty or whitespaces");

        bool overwriteElseIgnore = this.options.OverwriteOrIgnore;

        // Avalonia is pretty shit so no simple way to show errors yet. Would be List<(string, Exception)> for simplicity, string being full path
        foreach ((string pathInGtaDir, bool didNotMove) in this.loadedModPaths) {
            string fileName = Path.GetFileName(pathInGtaDir);
            string pathInModsDir = Path.Combine(modsDir, fileName);
            if (File.Exists(pathInGtaDir)) {
                MoveFile(pathInGtaDir, pathInModsDir, overwriteElseIgnore, out _);
            }
            else if (Directory.Exists(pathInGtaDir)) {
                MoveDir(pathInGtaDir, pathInModsDir, overwriteElseIgnore, out _);
            }
        }

        this.SetLoadedMods(null);
    }

    private static bool MoveFile(string src, string dst, bool overwrite, out bool didNotMove) {
        try {
            // Console.WriteLine($"Moving FILE '{src}' --> '{dst}'");
            if (File.Exists(dst) && !overwrite) {
                didNotMove = true;
                return true;
            }

            File.Move(src, dst, overwrite);
            didNotMove = false;
            return true;
        }
        catch {
            /* ignored */
            didNotMove = false;
            return false;
        }
    }

    private static bool MoveFile(FileInfo src, string dst, bool overwrite, out bool didNotMove) => MoveFile(src.FullName, dst, overwrite, out didNotMove);

    private static bool MoveDir(string src, string dst, bool overwrite, out bool didNotMove) {
        if (Directory.Exists(dst)) {
            if (overwrite) {
                try {
                    // Console.WriteLine($"Trying to delete directory for overwrite: '{dst}'");
                    Directory.Delete(dst);
                }
                catch {
                    didNotMove = false;
                    return false;
                }
            }
            else {
                didNotMove = true;
                return true;
            }
        }

        try {
            // Console.WriteLine($"Moving DIR '{src}' --> '{dst}'");
            Directory.Move(src, dst);
            didNotMove = false;
            return true;
        }
        catch {
            /* ignored */
            didNotMove = false;
            return false;
        }
    }

    private static bool MoveDir(DirectoryInfo src, string dst, bool overwrite, out bool didNotMove) => MoveDir(src.FullName, dst, overwrite, out didNotMove);

    private void SetLoadedMods(List<(string Path, bool DidNotMove)>? paths) {
        if (paths != null && this.loadedModPaths != null)
            throw new InvalidOperationException("Mod paths already loaded");

        this.loadedModPaths = paths;
        this.options.LastMovedFiles = paths?.Select(x => x.Path).ToList();
        this.AreModsLoadedChanged?.Invoke(this);

        this.options.StorageManager.SaveArea(this.options);
    }

    public async Task ScanAndProcessExistingModFiles() {
        List<string>? list = this.options.LastMovedFiles;
        this.options.LastMovedFiles = null;
        this.options.StorageManager.SaveArea(this.options);

        if (list != null && list.Count > 0 && list.Any(File.Exists)) {
            string modsDir = this.options.GtaModsDir;
            if (!Directory.Exists(modsDir)) {
                return;
            }
            
            MessageBoxInfo info = new MessageBoxInfo("Mods still loaded", "Some mod files still exist in the GTA5 directory, maybe the app crashed. \nWhat do you want to do?") {
                YesOkText = "Move files to mod folder",
                NoText = "Do nothing",
                Buttons = MessageBoxButton.YesNo, DefaultButton = MessageBoxResult.Yes
            };

            MessageBoxResult res = await IMessageDialogService.Instance.ShowMessage(info);
            if (res != MessageBoxResult.Yes) {
                return;
            }

            bool overwriteElseIgnore = this.options.OverwriteOrIgnore;
            foreach (string pathInGtaDir in list) {
                string fileName = Path.GetFileName(pathInGtaDir);
                string pathInModsDir = Path.Combine(modsDir, fileName);
                if (File.Exists(pathInGtaDir)) {
                    MoveFile(pathInGtaDir, pathInModsDir, overwriteElseIgnore, out _);
                }
                else if (Directory.Exists(pathInGtaDir)) {
                    MoveDir(pathInGtaDir, pathInModsDir, overwriteElseIgnore, out _);
                }
            }
        }
    }

    public async Task BrowseNewGtaDirAsync() {
        if (await BrowseFolder(this.options.GtaDir) is IStorageFolder folder && folder.TryGetLocalPath() is string path) {
            this.options.GtaDir = path;
            ModSwitchConfiguration.Instance.StorageManager.SaveArea(ModSwitchConfiguration.Instance);
        }
    }

    public async Task BrowseNewModsDirAsync() {
        if (await BrowseFolder(this.options.GtaModsDir) is IStorageFolder folder && folder.TryGetLocalPath() is string path) {
            this.options.GtaModsDir = path;
            this.options.StorageManager.SaveArea(this.options);
        }
    }

    private static async Task<IStorageFolder?> BrowseFolder(string? initialDir) {
        if (((IFrontEndApplication) ApplicationPFX.Instance).TryGetActiveWindow(out Window? window)) {
            IStorageProvider storage = window.StorageProvider;
            IStorageFolder? initialFolder = null;
            try {
                initialFolder = !string.IsNullOrWhiteSpace(initialDir) ? await storage.TryGetFolderFromPathAsync(initialDir) : null;
            }
            catch { /* ignored */
            }

            IReadOnlyList<IStorageFolder> list = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions() { SuggestedStartLocation = initialFolder, AllowMultiple = false });
            return list.Count == 1 ? list[0] : null;
        }

        return null;
    }
}