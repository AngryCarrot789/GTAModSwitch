using PFXToolKitUI;
using PFXToolKitUI.Persistence;

namespace GTAModSwitch;

public class BasicAppConfigOptions : PersistentConfiguration {
    public static BasicAppConfigOptions Instance => ApplicationPFX.Instance.PersistentStorageManager.GetConfiguration<BasicAppConfigOptions>();
    
    public static readonly PersistentProperty<string> GtaDirProperty = PersistentProperty.RegisterString<BasicAppConfigOptions>(nameof(GtaDir), "", x => x.gtaDir, (x, y) => x.gtaDir = y, false);
    public static readonly PersistentProperty<string> GtaModsDirProperty = PersistentProperty.RegisterString<BasicAppConfigOptions>(nameof(GtaModsDir), "", x => x.gtaModsDir, (x, y) => x.gtaModsDir = y, false);
    public static readonly PersistentProperty<bool> OverwriteOrIgnoreProperty = PersistentProperty.RegisterBool<BasicAppConfigOptions>(nameof(OverwriteOrIgnore), true, x => x.overwriteOrIgnore, (x, y) => x.overwriteOrIgnore = y, false);
    
    private string gtaDir = null!, gtaModsDir = null!;
    private bool overwriteOrIgnore;
    
    public string GtaDir {
        get => GtaDirProperty.GetValue(this);
        set => GtaDirProperty.SetValue(this, value);
    }    
    
    public string GtaModsDir {
        get => GtaModsDirProperty.GetValue(this);
        set => GtaModsDirProperty.SetValue(this, value);
    } 
    
    public bool OverwriteOrIgnore {
        get => OverwriteOrIgnoreProperty.GetValue(this);
        set => OverwriteOrIgnoreProperty.SetValue(this, value);
    }

    public BasicAppConfigOptions() {
    }
}