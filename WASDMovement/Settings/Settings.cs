using static Kingmaker.Visual.Animation.Kingmaker.Actions.UnitAnimationActionLocoMotion;

namespace WASDMovement;
internal class Settings : AbstractSettings {
    private static readonly Lazy<Settings> _instance = new Lazy<Settings>(() => {
        var instance = new Settings();
        instance.Load();
        return instance;
    });
    public static Settings Instance => _instance.Value;
    protected override string Name => "Settings.json";

    public float MovementMagnitude = 1f;
    public Main.WalkMode WalkMode = Main.WalkMode.Fast;
}