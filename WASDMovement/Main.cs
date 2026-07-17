using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.GameModes;
using Kingmaker.UI.Common;
using Kingmaker.UI.Selection;
using Kingmaker.View;
#if RT
using Owlcat.Runtime.Core.Utility;
#elif Wrath || KM
using Kingmaker.UI._ConsoleUI;
using Kingmaker.UI._ConsoleUI.InputLayers.InGameLayer;
using static Kingmaker.Visual.Animation.Kingmaker.Actions.UnitAnimationActionLocoMotion;
#endif
#if Wrath
using Owlcat.Runtime.Core.Utils;
#endif
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;
using UnityModManagerNet;
using UniRx;
using Kingmaker.PubSubSystem;
using Kingmaker.Utility;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic;
#if !KM
using UnityEngine.ProBuilder.MeshOperations;
#endif
using Kingmaker.Formations;
using Kingmaker.Controllers.Clicks.Handlers;

namespace WASDMovement;

public static class Main {
    public enum WalkMode {
        None = 0,
        Fast = 1,
        Normal = 2,
        Slow = 10,
#if Wrath || KM
        Stealth = 20
#endif
    }
#if Wrath || KM
    private static readonly Dictionary<WalkMode, WalkSpeedType> WalkMap = new() {
        { WalkMode.Fast, WalkSpeedType.Normal }, { WalkMode.Normal, WalkSpeedType.Normal },
        { WalkMode.Slow, WalkSpeedType.Slow }, { WalkMode.Stealth, WalkSpeedType.Stealth },
    };
#endif
    internal static Harmony HarmonyInstance = null!;
    internal static UnityModManager.ModEntry.ModLogger Log => ModEntry.Logger;
    internal static UnityModManager.ModEntry ModEntry = null!;
    private static bool m_MovedLastFrame = false;
    private static readonly Lazy<int> m_NumEnums = new(() => Enum.GetValues(typeof(WalkMode)).Length);
    private static WalkMode m_CurrentlyBinding = WalkMode.Fast;
    private static Hotkey? m_IsBindingSomething;
    public static bool Load(UnityModManager.ModEntry modEntry) {
        ModEntry = modEntry;
        modEntry.OnUpdate = OnUpdate;
        modEntry.OnGUI = OnGUI;
        modEntry.OnSaveGUI = OnSaveGUI;
        HarmonyInstance = new Harmony(modEntry.Info.Id);
        try {
            HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        } catch {
            HarmonyInstance.UnpatchAll(HarmonyInstance.Id);
            throw;
        }
        return true;
    }
    public static void OnSaveGUI(UnityModManager.ModEntry modEntry) {
        Settings.Instance.Save();
    }
    public static void OnGUI(UnityModManager.ModEntry modEntry) {
        using (new GUILayout.VerticalScope()) {
            using (new GUILayout.HorizontalScope()) {
                UI.Label("Walk Mode".Green());
                GUILayout.Space(10);
                if (UI.SelectionGrid(ref Settings.Instance.WalkMode, m_NumEnums.Value, null)) {
                    Settings.Instance.Save();
                }
            }
            UI.Toggle("Hold Binding Mode", "If activated, holding a binding will temporarily override the walk mode. If disabled it will swap to the selected mode.", ref Settings.Instance.HoldBindingMode, null, null);
            using (new GUILayout.HorizontalScope()) {
                UI.Label("Binding Section - Add keybinds for the various walk modes".Orange());
                if (UI.SelectionGrid(ref m_CurrentlyBinding, m_NumEnums.Value, null)) {
                    m_IsBindingSomething = null;
                }
            }
            if (Settings.Instance.Hotkeys.TryGetValue(m_CurrentlyBinding, out var current)) {
                using (new GUILayout.HorizontalScope()) {
                    UI.Label(current.ToString().Orange());
                    GUILayout.Space(5);
                    if (UI.Button("Delete".Orange())) {
                        Settings.Instance.Hotkeys.Remove(m_CurrentlyBinding);
                    }
                }
            } else {
                UI.Label("No Bind".Orange());
            }
            if (m_IsBindingSomething == null) {
                if (UI.Button("Rebind".Green())) {
                    m_IsBindingSomething = new(default);
                }
            } else {
                using (new GUILayout.HorizontalScope()) {
                    UI.Label($"Currently: {m_IsBindingSomething}");
                    GUILayout.Space(5);
                    if (UI.Button("Cancel".Orange())) {
                        m_IsBindingSomething = null;
                    }
                    GUILayout.Space(5);
                    if (UI.Button("Apply".Green()) && m_IsBindingSomething != null) {
                        Settings.Instance.Hotkeys[m_CurrentlyBinding] = m_IsBindingSomething;
                        m_IsBindingSomething = null;
                    }
                    if (m_IsBindingSomething != null) {
                        bool anyMouseButtonPressed = false;
                        foreach (var button in (List<KeyCode>)[KeyCode.Mouse2, KeyCode.Mouse3,
                                                    KeyCode.Mouse4, KeyCode.Mouse5, KeyCode.Mouse6]) {
                            if (Input.GetKeyDown(button)) {
                                anyMouseButtonPressed = true;
                                break;
                            }
                        }
                        if (Event.current.type == EventType.KeyDown || anyMouseButtonPressed) {
                            m_IsBindingSomething.IsShift = Event.current.modifiers.HasFlag(EventModifiers.Shift);
                            m_IsBindingSomething.IsAlt = Event.current.modifiers.HasFlag(EventModifiers.Alt);
                            m_IsBindingSomething.IsCtrl = Event.current.modifiers.HasFlag(EventModifiers.Control) || Event.current.modifiers.HasFlag(EventModifiers.Command);
                            if (!IsSpecial(Event.current.keyCode)) {
                                m_IsBindingSomething.Key = Event.current.keyCode;
                            } else {
                                var found = false;
                                foreach (KeyCode c in Enum.GetValues(typeof(KeyCode))) {
                                    if (IsSpecial(c)) {
                                        continue;
                                    }
                                    if (Input.GetKeyDown(c)) {
                                        m_IsBindingSomething.Key = c;
                                        found = true;
                                        break;
                                    }
                                }
                                if (!found) {
                                    m_IsBindingSomething.Key = KeyCode.None;
                                }
                            }
                            if (Event.current.type == EventType.KeyDown || Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp) {
                                Event.current.Use();
                            }
                        }
                    }
                }
            }
        }
    }
    private static bool IsSpecial(KeyCode code) {
        return code switch {
            KeyCode.LeftControl or KeyCode.RightControl or KeyCode.LeftCommand or KeyCode.RightCommand or KeyCode.LeftShift or KeyCode.RightShift or KeyCode.LeftAlt or KeyCode.RightAlt or KeyCode.None => true,
            _ => false,
        };
    }
#if Wrath || KM
    private static int m_FramesSinceLastCompanionUpdate = 0;
#endif
    private static WalkMode? m_LastOverride = null;
    private static WalkMode? m_LastHandled = null;
    public static void OnUpdate(UnityModManager.ModEntry modEntry, float z) {
        WalkMode? overrideMode = null;
        bool overriden = false;
        foreach (var pair in Settings.Instance.Hotkeys) {
            if (pair.Value.IsPressed()) {
                if (Settings.Instance.HoldBindingMode) {
                    overrideMode = pair.Key;
                } else {
                    overriden = Settings.Instance.WalkMode != pair.Key;
                    if (overriden) {
                        Settings.Instance.WalkMode = pair.Key;
                        Settings.Instance.Save();
                    }
                }
            }
        }
        var actualWalkMode = overrideMode ?? Settings.Instance.WalkMode;

        if (actualWalkMode == WalkMode.None && m_LastHandled == WalkMode.None) {
            return;
        }
#if RT
        if (!GamepadInputController.CanProcessInput) {
            return;
        }
        var unit = SelectionManagerBase.Instance.SelectedUnit.Value;
        if (!UINetUtility.IsDirectlyControllable(unit)) {
            return;
        }

        m_LastHandled = actualWalkMode;

        var movement = ReadKeyboardInput(actualWalkMode);
        if (overrideMode != m_LastOverride || overriden) {
            unit.Commands.InterruptMove();
            movement = Vector2.zero;
        }
        m_LastOverride = overrideMode;
        bool movedThisFrame = movement.sqrMagnitude > 0;
        if (!movedThisFrame && !m_MovedLastFrame) {
            return;
        }
        m_MovedLastFrame = movedThisFrame;
        movement = (movement.x * CameraRig.Instance.Right + movement.y * CameraRig.Instance.Up).To2D();
        Game.Instance.SynchronizedDataController.PushLeftStickMovement(unit, movement, movement.magnitude);
#elif Wrath || KM
        if (!Patches.CanProcess(out var unit, out var camera)) {
            return;
        }

        m_LastHandled = actualWalkMode;

        var movement = ReadKeyboardInput(actualWalkMode);
        if (!unit!.IsDirectlyControllable) {
            movement = Vector2.zero;
        }
        if (overrideMode != m_LastOverride || overriden) {
            unit.Commands.InterruptMove();
            movement = Vector2.zero;
        }
        m_LastOverride = overrideMode;
        var movedThisFrame = movement.sqrMagnitude > 0;
        if (movedThisFrame || m_MovedLastFrame) {
            Vector2 mov = (movement.x * camera!.transform.right + movement.y * camera.transform.forward).To2D();
            mov.Normalize();
#if Wrath
            if (unit.View?.AgentOverride is UnitMovementAgentContinuous agent) {
#elif KM
            if (unit.View?.AgentOverride is UnitMovementAgentContinious agent) {
#endif
                agent.DirectionFromController = mov;
                agent.DirectionFromControllerMagnitude = mov.magnitude;
                if (actualWalkMode == WalkMode.Fast) {
                    agent.MaxSpeedOverride = unit.CurrentSpeedMps * 1.8f;
                }
#if Wrath
                if (unit.GetSaddledUnit()?.View?.AgentOverride is UnitMovementAgentContinuous agent2) {
                    agent2.DirectionFromController = mov;
                    agent2.DirectionFromControllerMagnitude = mov.magnitude;
                    if (actualWalkMode == WalkMode.Fast) {
                        agent2.MaxSpeedOverride = unit.GetSaddledUnit().CurrentSpeedMps * 1.8f;
                    }
                }
#endif
            }
#if Wrath
            if (unit.Commands.MoveContinuously == null || overriden) {
#elif KM
            if (unit.Commands.MoveContiniously == null || overriden) {
#endif
                unit.Commands.InterruptMove();
                UnitMoveContiniously cmd = new() {
                    CreatedByPlayer = true,
                    SpeedLimit = Math.Min(30.Feet().Meters / 3, unit.ModifiedSpeedMps) * (actualWalkMode == WalkMode.Fast ? 1.8f : 1f),
                    MovementType = WalkMap[actualWalkMode],
                };
                cmd.Init(unit);
                unit.Commands.Run(cmd);
                if (actualWalkMode == WalkMode.Fast) {
                    cmd.Accelerate();
                } else {
                    cmd.Deaccelerate();
                }
#if Wrath
                if (unit.View?.AgentOverride is UnitMovementAgentContinuous agent3) {
#elif KM
                if (unit.View?.AgentOverride is UnitMovementAgentContinious agent3) {
#endif
                    agent3.DirectionFromController = mov;
                    if (actualWalkMode == WalkMode.Fast) {
                        agent3.MaxSpeedOverride = unit.CurrentSpeedMps * 1.8f;
                    }
                }
            }
            Game.Instance.CameraController?.Follower?.Follow(unit);
            m_FramesSinceLastCompanionUpdate++;
#if Wrath
            var selected = Game.Instance.SelectionCharacter.SelectedUnits;
#elif KM
            var selected = Game.Instance.UI.SelectionManager.SelectedUnits;
#endif
            if (selected?.Count > 1 && (m_FramesSinceLastCompanionUpdate % 2 == 0 || !movedThisFrame)) {
#if Wrath
                var list = Game.Instance.Player.PartyAndPets.Where((UnitEntityData c) => c.IsDirectlyControllable).ToList<UnitEntityData>();
#elif KM
                var list = Game.Instance.Player.Party.Where((UnitEntityData c) => c.IsDirectlyControllable).ToList<UnitEntityData>();
#endif
                var index = list.IndexOf(unit);
#if Wrath
                var forward = unit.View!.Transform.forward;
#elif KM
                var forward = unit.View!.transform.forward;
#endif
                var pos = PartyFormationHelper.FindFormationCenterFromOneUnit(FormationAnchor.Front, forward, index, unit.Position, list, selected);

                ClickGroundHandler.MoveSelectedUnitsToPoint(pos, forward, false, false, 1f, false,
#if Wrath
                (unit2, settings) => {
                    if (unit2 != unit) {
                        var cmd = new UnitMoveTo(settings.Destination) {
                            Orientation = settings.Orientation,
                            MovementDelay = settings.Delay,
                            SpeedLimit = settings.SpeedLimit,
                            CreatedByPlayer = true,
                            ShowTargetMarker = settings.ShowTargetMarker
                        };
                        unit2.Commands.InterruptMove();
                        cmd.Init(unit2);
                        unit2.Commands.Run(cmd);
                        if (actualWalkMode == WalkMode.Fast) {
                            cmd.Accelerate();
                        }
                    }
                }
#elif KM
                (unit2, destination, speedLimit, orientation, delay, showTargetMarker) => {
                    if (unit2 != unit) {
                        var cmd = new UnitMoveTo(destination) {
                            Orientation = orientation,
                            MovementDelay = delay,
                            SpeedLimit = speedLimit,
                            CreatedByPlayer = true,
                            ShowTargetMarker = showTargetMarker
                        };
                        unit2.Commands.InterruptMove();
                        cmd.Init(unit2);
                        unit2.Commands.Run(cmd);
                        if (actualWalkMode == WalkMode.Fast) {
                            cmd.Accelerate();
                        }
                    }
                }
#endif
                );
            }
        }
        m_MovedLastFrame = movedThisFrame;
#endif
    }
    private static Vector2 ReadKeyboardInput(WalkMode mode) {
        if (mode == WalkMode.None) {
            return new Vector2(0, 0);
        }
        float x = 0f, y = 0f;

        if (Input.GetKey(Settings.Instance.Left)) {
            x -= Settings.Instance.MovementMagnitude;
        }
        if (Input.GetKey(Settings.Instance.Right)) {
            x += Settings.Instance.MovementMagnitude;
        }
        if (Input.GetKey(Settings.Instance.Up)) {
            y += Settings.Instance.MovementMagnitude;
        }
        if (Input.GetKey(Settings.Instance.Down)) {
            y -= Settings.Instance.MovementMagnitude;
        }

        var v = new Vector2(x, y);

        if (v.sqrMagnitude > 1f) {
            v = v.normalized;
        }
#if RT
        v /= (int)mode;
#endif

        return v;
    }
}
