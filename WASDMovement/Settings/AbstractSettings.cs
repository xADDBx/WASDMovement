using Newtonsoft.Json;

namespace WASDMovement;
internal abstract class AbstractSettings {
    protected abstract string Name { get; }
    private static readonly JsonSerializerSettings m_SerializerSettings = new() {
        Formatting = Formatting.Indented,
        PreserveReferencesHandling = PreserveReferencesHandling.None,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };
    private string GetFilePath() {
        var userConfigFolder = Path.Combine(Main.ModEntry.Path, "Settings");
        Directory.CreateDirectory(userConfigFolder);
        return Path.Combine(userConfigFolder, Name);
    }
    internal void Save() {
        File.WriteAllText(GetFilePath(), JsonConvert.SerializeObject(this, m_SerializerSettings));
    }
    internal void Load() {
        var userPath = GetFilePath();
        if (File.Exists(userPath)) {
            string content = File.ReadAllText(userPath);
            try {
                JsonConvert.PopulateObject(content, this, m_SerializerSettings);
            } catch {
                Main.Log.Log($"[Error] Failed to load user settings at {userPath}. Settings will be rebuilt.");
                File.WriteAllText(userPath, JsonConvert.SerializeObject(this, m_SerializerSettings));
            }
        } else {
            Main.Log.Log($"[Warn] No Settings file found with path {userPath}, creating new.");
            File.WriteAllText(userPath, JsonConvert.SerializeObject(this, m_SerializerSettings));
        }
    }
}
