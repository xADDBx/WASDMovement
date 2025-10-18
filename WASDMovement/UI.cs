using UnityEngine;

namespace WASDMovement;
public static class UI {
    public static bool Button(string? title = null, Action? onPressed = null, GUIStyle? style = null, params GUILayoutOption[] options) {
        options = options.Length == 0 ? [GUILayout.ExpandWidth(false)] : options;
        bool pressed = false;
        if (GUILayout.Button(title ?? "", style ?? GUI.skin.button, options)) {
            onPressed?.Invoke();
            pressed = true;
        }
        return pressed;
    }
    public static void Label(string? title = null, params GUILayoutOption[] options) {
        options = options.Length == 0 ? [GUILayout.ExpandWidth(false)] : options;
        GUILayout.Label(title ?? "", options);
    }
    public static string Color(this string s, string color) => $"<color={color}>{s}</color>";
    public static string Green(this string s) => s.Color("green");
    public static string Orange(this string s) => s.Color("orange");
    private static Dictionary<Type, Array> m_EnumCache = new();
    private static Dictionary<Type, Dictionary<object, int>> m_IndexToEnumCache = new();
    private static Dictionary<Type, string[]> m_EnumNameCache = new();
    public static bool SelectionGrid<TEnum>(ref TEnum selected, int xCols, Func<TEnum, string>? titler, params GUILayoutOption[] options) where TEnum : Enum {
        if (!m_EnumCache.TryGetValue(typeof(TEnum), out var vals)) {
            vals = Enum.GetValues(typeof(TEnum));
            m_EnumCache[typeof(TEnum)] = vals;
        }
        if (!m_EnumNameCache.TryGetValue(typeof(TEnum), out var names)) {
            Dictionary<object, int> indexToEnum = new();
            List<string> tmpNames = new();
            for (int i = 0; i < vals.Length; i++) {
                string name;
                var val = vals.GetValue(i);
                indexToEnum[val] = i;
                if (titler != null) {
                    name = titler((TEnum)val);
                } else {
                    name = Enum.GetName(typeof(TEnum), val);
                }
                tmpNames.Add(name);
            }
            names = [.. tmpNames];
            m_EnumNameCache[typeof(TEnum)] = names;
            m_IndexToEnumCache[typeof(TEnum)] = indexToEnum;
        }
        if (xCols <= 0) {
            xCols = vals.Length;
        }
        var selectedInt = m_IndexToEnumCache[typeof(TEnum)][selected];
        // Create a copy to not recolour the selected element permanently
        // names = [.. names];
        // Better idea: Just cache that one name and change it back after
        var uncolored = names[selectedInt];
        names[selectedInt] = uncolored.Orange();
        var newSel = GUILayout.SelectionGrid(selectedInt, names, xCols, options);
        names[selectedInt] = uncolored;
        bool changed = selectedInt != newSel;
        if (changed) {
            selected = (TEnum)vals.GetValue(newSel);
        }
        return changed;
    }
    public static bool Toggle(string name, string? description, ref bool setting, Action? onEnable, Action? onDisable, params GUILayoutOption[] options) {
        options = options.Length == 0 ? [GUILayout.ExpandWidth(false)] : options;
        bool changed = false;
        using (new GUILayout.HorizontalScope()) {
            var newValue = GUILayout.Toggle(setting, name.Green(), options);
            if (newValue != setting) {
                changed = true;
                setting = newValue;
                if (newValue) {
                    onEnable?.Invoke();
                } else {
                    onDisable?.Invoke();
                }
            }
            if (description != null) {
                GUILayout.Space(10);
                Label(description.Green());
            }
        }
        return changed;
    }
    public static bool Slider(ref float value, float minValue, float maxValue, float? defaultValue = null, Action<(float oldValue, float newValue)>? onValueChanged = null, params GUILayoutOption[] options) {
        options = options.Length == 0 ? [GUILayout.ExpandWidth(false), GUILayout.Width(600)] : options;
        var oldValue = value;
        float result = (float)Math.Round(GUILayout.HorizontalSlider(oldValue, minValue, maxValue, options), 0);
        Label(value.ToString().Orange() + " ");
        if (defaultValue != null) {
            GUILayout.Space(4);
            Button("Reset", () => {
                result = defaultValue.Value;
            });
        }
        if (result != value) {
            value = result;
            onValueChanged?.Invoke((oldValue, value));
            return true;
        }
        return false;
    }
    public static bool LogSlider(ref float value, float minValue, float maxValue, float? defaultValue = null, int digits = 2, Action<(float oldValue, float newValue)>? onValueChanged = null, params GUILayoutOption[] options) {
        options = options.Length == 0 ? [GUILayout.ExpandWidth(false), GUILayout.Width(600)] : options;
        var oldValue = value;
        // Log(0) is bad; so shift to positive
        double offset = minValue + 1;

        float logValue = 100f * (float)Math.Log10(value + offset);
        float logMin = 100f * (float)Math.Log10(minValue + offset);
        float logMax = 100f * (float)Math.Log10(maxValue + offset);

        float logResult = GUILayout.HorizontalSlider(logValue, logMin, logMax, options);
        float result = (float)Math.Round(Math.Pow(10, logResult / 100f) - offset, digits);
        Label(value.ToString().Orange() + " ");
        if (defaultValue != null) {
            GUILayout.Space(4);
            Button("Reset", () => {
                result = defaultValue.Value;
            });
        }
        if (Math.Abs((result - value)) > float.Epsilon) {
            value = result;
            onValueChanged?.Invoke((oldValue, value));
            return true;
        }
        return false;
    }
}
