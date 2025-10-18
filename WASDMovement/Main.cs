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
#elif Wrath
using Kingmaker.UI._ConsoleUI;
using Kingmaker.UI._ConsoleUI.InputLayers.InGameLayer;
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
using UnityEngine.ProBuilder.MeshOperations;
using Kingmaker.Formations;
using Kingmaker.Controllers.Clicks.Handlers;
using static Kingmaker.Visual.Animation.Kingmaker.Actions.UnitAnimationActionLocoMotion;

namespace WASDMovement;

public static class Main {
    public enum WalkMode {
        Fast = 1,
        Normal = 2,
        Slow = 10,
#if Wrath
        Stealth = 20
#endif
    }
#if Wrath
    private static readonly Dictionary<WalkMode, WalkSpeedType> WalkMap = new() {
        { WalkMode.Fast, WalkSpeedType.Normal }, { WalkMode.Normal, WalkSpeedType.Normal },
        { WalkMode.Slow, WalkSpeedType.Slow }, { WalkMode.Stealth, WalkSpeedType.Stealth },
    };
#endif
    internal static Harmony HarmonyInstance = null!;
    internal static UnityModManager.ModEntry.ModLogger Log => ModEntry.Logger;
    internal static UnityModManager.ModEntry ModEntry = null!;
    private static bool m_MovedLastFrame = false;

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
        using (new GUILayout.HorizontalScope()) {
            UI.Label("Walk Mode".Green());
            GUILayout.Space(10);
            UI.SelectionGrid(ref Settings.Instance.WalkMode,
#if RT
                3
#elif Wrath
                4
#endif
                , null);
            // UI.LogSlider(ref Settings.Instance.MovementMagnitude, 0.0001f, 10f, 1f, 4);
        }
    }
#if Wrath
    private static int m_FramesSinceLastCompanionUpdate = 0;
#endif
    public static void OnUpdate(UnityModManager.ModEntry modEntry, float z) {
#if RT
        if (!GamepadInputController.CanProcessInput) {
            return;
        }
        var unit = SelectionManagerBase.Instance.SelectedUnit.Value;
        if (!UINetUtility.IsDirectlyControllable(unit)) {
            return;
        }

        var movement = ReadKeyboardInput();
        bool movedThisFrame = movement.sqrMagnitude > 0;
        if (!movedThisFrame && !m_MovedLastFrame) {
            return;
        }
        m_MovedLastFrame = movedThisFrame;
        movement = (movement.x * CameraRig.Instance.Right + movement.y * CameraRig.Instance.Up).To2D();
        Game.Instance.SynchronizedDataController.PushLeftStickMovement(unit, movement, movement.magnitude);
#elif Wrath
        if (!Patches.CanProcess(out var unit, out var camera)) {
            return;
        }
        var movement = ReadKeyboardInput();
        if (!unit!.IsDirectlyControllable) {
            movement = Vector2.zero;
        }
        var movedThisFrame = movement.sqrMagnitude > 0;
        if (movedThisFrame || m_MovedLastFrame) {
            Vector2 mov = (movement.x * camera!.transform.right + movement.y * camera.transform.forward).To2D();
            mov.Normalize();
            if (unit.View?.AgentOverride is UnitMovementAgentContinuous agent) {
                agent.DirectionFromController = mov;
                agent.DirectionFromControllerMagnitude = movement.magnitude;
                if (unit.GetSaddledUnit()?.View?.AgentOverride is UnitMovementAgentContinuous agent2) {
                    agent2.DirectionFromController = mov;
                    agent2.DirectionFromControllerMagnitude = movement.magnitude;
                }
            }
            if (unit.Commands.MoveContinuously == null) {
                unit.Commands.InterruptMove();
                UnitMoveContiniously cmd = new() {
                    CreatedByPlayer = true,
                    SpeedLimit = Math.Max(30.Feet().Meters / 3, unit.ModifiedSpeedMps),
                    MovementType = WalkMap[Settings.Instance.WalkMode]
                };
                cmd.Init(unit);
                unit.Commands.Run(cmd);
                if (Settings.Instance.WalkMode == WalkMode.Fast) {
                    cmd.Accelerate();
                }
                if (unit.View?.AgentOverride is UnitMovementAgentContinuous agent3) {
                    agent3.DirectionFromController = mov;
                }
            }
            Game.Instance.CameraController?.Follower?.Follow(unit);
            m_FramesSinceLastCompanionUpdate++;
            if (Game.Instance.SelectionCharacter.SelectedUnits?.Count > 1 && (m_FramesSinceLastCompanionUpdate % 40 == 0 || !movedThisFrame)) {
                var list = Game.Instance.Player.PartyAndPets.Where((UnitEntityData c) => c.IsDirectlyControllable).ToList<UnitEntityData>();
                var index = list.IndexOf(unit);
                var forward = unit.View!.Transform.forward;
                var pos = PartyFormationHelper.FindFormationCenterFromOneUnit(FormationAnchor.Front, forward, index, unit.Position, list, Game.Instance.SelectionCharacter.SelectedUnits);
                ClickGroundHandler.MoveSelectedUnitsToPoint(pos, forward, false, false, 1f, false, (unit2, settings) => {
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
                        if (Settings.Instance.WalkMode == WalkMode.Fast) {
                            cmd.Accelerate();
                        }
                    }
                });
            }
        }
        m_MovedLastFrame = movedThisFrame;
#endif
    }

    public const KeyCode UpKey = KeyCode.W;
    public const KeyCode DownKey = KeyCode.S;
    public const KeyCode LeftKey = KeyCode.A;
    public const KeyCode RightKey = KeyCode.D;
    private static Vector2 ReadKeyboardInput() {
        float x = 0f, y = 0f;

        if (Input.GetKey(LeftKey)) {
            x -= Settings.Instance.MovementMagnitude;
        }
        if (Input.GetKey(RightKey)) {
            x += Settings.Instance.MovementMagnitude;
        }
        if (Input.GetKey(UpKey)) {
            y += Settings.Instance.MovementMagnitude;
        }
        if (Input.GetKey(DownKey)) {
            y -= Settings.Instance.MovementMagnitude;
        }

        var v = new Vector2(x, y);

        if (v.sqrMagnitude > 1f) {
            v = v.normalized;
        }
#if RT
        v /= (int)Settings.Instance.WalkMode;
#endif

        return v;
    }
}
