using HarmonyLib;
#if RT
using Kingmaker.UI.InputSystems;
using Kingmaker.UI.Models.SettingsUI;
using Kingmaker.Controllers;
#elif Wrath
using Kingmaker;
using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.GameModes;
using Kingmaker.UI;
using Kingmaker.UI._ConsoleUI;
using Kingmaker.UI._ConsoleUI.InputLayers.InGameLayer;
using Kingmaker.UI._ConsoleUI.Models;
using Kingmaker.UI.SettingsUI;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Owlcat.Runtime.Core.Utils;
using System.Reflection.Emit;
#endif
using UnityEngine;

namespace WASDMovement;
[HarmonyPatch]
internal static class Patches {
    private static readonly Lazy<string[]> m_Names = new(() => [
#if RT
        UISettingsRoot.Instance.UIKeybindGeneralSettings.CameraLeft.name,
        UISettingsRoot.Instance.UIKeybindGeneralSettings.CameraRight.name,
        UISettingsRoot.Instance.UIKeybindGeneralSettings.CameraUp.name,
        UISettingsRoot.Instance.UIKeybindGeneralSettings.CameraDown.name
#elif Wrath
        UISettingsRoot.Instance.CameraLeft.name,
        UISettingsRoot.Instance.CameraRight.name,
        UISettingsRoot.Instance.CameraUp.name,
        UISettingsRoot.Instance.CameraDown.name
#endif
    ]);
    private static readonly KeyCode[] m_KeyCodes = [KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D];
    [HarmonyPatch(typeof(KeyboardAccess.Binding), nameof(KeyboardAccess.Binding.InputMatched)), HarmonyPostfix]
    private static void InputMatched(KeyboardAccess.Binding __instance, ref bool __result) {
        if (m_KeyCodes.Contains(__instance.Key) && m_Names.Value.Contains(__instance.Name)
#if RT
            && GamepadInputController.CanProcessInput
#elif Wrath
            && CanProcess(out _, out _)
#endif
            ) {
            __result = false;
        }
    }
    internal static bool CanProcess(out UnitEntityData? unit, out Camera? camera) {
        unit = null;
        camera = null;
        if (Game.Instance.CurrentMode != GameModeType.Default
                || Game.Instance.CutsceneLock.Active
                || Game.Instance.Player.IsInCombat) {
            return false;
        }
        unit = Game.Instance.SelectionCharacter.SelectedUnit.Value.Value;
        if (unit == null) {
            return false;
        }
        camera = Game.Instance.UI?.GetCameraRig()?.Camera;
        if (camera == null) {
            return false;
        }

        return true;
    }
}
