using System.Collections.Generic;
using System.Linq;
using System.Xml;
using PFXToolKitUI;
using PFXToolKitUI.Persistence;
using PFXToolKitUI.Persistence.Serialisation;

namespace GTAModSwitch;

public class ModSwitchConfiguration : PersistentConfiguration {
    public static ModSwitchConfiguration Instance => ApplicationPFX.Instance.PersistentStorageManager.GetConfiguration<ModSwitchConfiguration>();
    
    public static readonly PersistentProperty<string> GtaDirProperty = PersistentProperty.RegisterString<ModSwitchConfiguration>(nameof(GtaDir), "", x => x.gtaDir, (x, y) => x.gtaDir = y, false);
    public static readonly PersistentProperty<string> GtaModsDirProperty = PersistentProperty.RegisterString<ModSwitchConfiguration>(nameof(GtaModsDir), "", x => x.gtaModsDir, (x, y) => x.gtaModsDir = y, false);
    public static readonly PersistentProperty<bool> OverwriteOrIgnoreProperty = PersistentProperty.RegisterBool<ModSwitchConfiguration>(nameof(OverwriteOrIgnore), true, x => x.overwriteOrIgnore, (x, y) => x.overwriteOrIgnore = y, false);
    public static readonly PersistentProperty<List<string>?> LastMovedFilesProperty = PersistentProperty.RegisterCustom<List<string>?, ModSwitchConfiguration>(nameof(LastMovedFiles), null, x => x.lastMovedFiles, (x, y) => x.lastMovedFiles = y, new StringListSerializer());
    
    private string gtaDir = null!, gtaModsDir = null!;
    private bool overwriteOrIgnore;
    private List<string>? lastMovedFiles;
    
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
    
    public List<string>? LastMovedFiles {
        get => LastMovedFilesProperty.GetValue(this);
        set => LastMovedFilesProperty.SetValue(this, value);
    }

    public event PersistentPropertyInstanceValueChangeEventHandler<string> GtaDirChanged {
        add => GtaDirProperty.AddValueChangeHandler(this, value);
        remove => GtaDirProperty.RemoveValueChangeHandler(this, value);
    }
    
    public event PersistentPropertyInstanceValueChangeEventHandler<string> GtaModsDirChanged {
        add => GtaModsDirProperty.AddValueChangeHandler(this, value);
        remove => GtaModsDirProperty.RemoveValueChangeHandler(this, value);
    }
    
    public event PersistentPropertyInstanceValueChangeEventHandler<bool> OverwriteOrIgnoreChanged {
        add => OverwriteOrIgnoreProperty.AddValueChangeHandler(this, value);
        remove => OverwriteOrIgnoreProperty.RemoveValueChangeHandler(this, value);
    }

    public ModSwitchConfiguration() {
    }

    private class StringListSerializer : IValueSerializer<List<string>?> {
        public bool Serialize(List<string>? value, XmlDocument document, XmlElement parent) {
            if (value == null || value.Count < 1)
                return false;
            
            foreach (string filePath in value) {
                XmlElement elem = document.CreateElement("File");
                elem.SetAttribute("path", filePath);
                parent.AppendChild(elem);
            }
            
            return true;
        }

        public List<string>? Deserialize(XmlElement element) {
            List<string> list = new List<string>();
            foreach (XmlElement xmlElement in element.GetElementsByTagName("File").OfType<XmlElement>()) {
                string path = xmlElement.GetAttribute("path");
                if (!string.IsNullOrWhiteSpace(path)) {
                    list.Add(path);
                }
            }
            
            return list.Count > 0 ? list : null;
        }
    }
}