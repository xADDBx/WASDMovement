using Newtonsoft.Json;
using UnityEngine;

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
    public bool HoldBindingMode = false;
    public Main.WalkMode WalkMode = Main.WalkMode.Fast;
    public Dictionary<Main.WalkMode, Hotkey> Hotkeys = [];
    public KeyCode Up = KeyCode.W;
    public KeyCode Down = KeyCode.S;
    public KeyCode Left = KeyCode.A;
    public KeyCode Right = KeyCode.D;
}
public class Hotkey(KeyCode key, bool ctrl = false, bool shift = false, bool alt = false) {
    [JsonProperty]
    internal bool IsCtrl = ctrl;
    [JsonProperty]
    internal bool IsShift = shift;
    [JsonProperty]
    internal bool IsAlt = alt;
    [JsonProperty]
    internal KeyCode Key = key;
    public bool IsPressed() {
        if (IsCtrl && !IsControlHeld()) return false;
        if (IsShift && !IsShiftHeld()) return false;
        if (IsAlt && !IsAltHeld()) return false;

        return Key == KeyCode.None || Input.GetKeyDown(Key);
    }
    private static bool IsControlHeld() {
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
            || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
    }
    private static bool IsShiftHeld() {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }
    private static bool IsAltHeld() {
        return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
    }
    public override string ToString() {
        string result = "";
        if (IsAlt) {
            result += "Alt + ";
        }
        if (IsCtrl) {
            result += "Ctrl + ";
        }
        if (IsShift) {
            result += "Shift + ";
        }
        result += Key.ToString();
        return result;
    }
}