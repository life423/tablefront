using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Tablefront
{
    /// <summary>The local match shell. The simulation never reads native input.</summary>
    public sealed class TablefrontGame : MonoBehaviour
    {
        public BattleSimulation Sim { get; private set; }
        public HashSet<int> Selected { get; } = new HashSet<int>();
        public bool InMenu { get; private set; } = true;
        public bool Paused { get; private set; }
        public bool Spectator { get; private set; }
        public bool HelpOpen { get; private set; }
        public int Speed { get; private set; } = 1;
        public string Notice => Time.unscaledTime < noticeUntil ? notice : "";
        public Camera ViewCamera { get; private set; }
        public TablefrontHUD HUD { get; private set; }
        public TablefrontWorld World { get; private set; }
        public bool TutorialActive { get; private set; }
        public bool PlayerMatch => !startedAsSpectator;
        public int TutorialStep { get; private set; }
        public bool TacticalAlertAvailable => !InMenu && !Sim.Finished && !TutorialActive && Time.unscaledTime < alertUntil;
        public string TacticalAlert => TacticalAlertAvailable ? tacticalAlert : "";
        public string TutorialTitle => new[] { "Select your force", "Give your first order", "Capture the beacon", "Call reinforcements", "Ready for battle" }[Mathf.Clamp(TutorialStep, 0, 4)];
        public string TutorialInstruction
        {
            get
            {
                switch (TutorialStep)
                {
                    case 0: return "Click or drag over your blue troops. You can also use Select all. Opposing commanders wait during this lesson.";
                    case 1: return "Right-click inside the WEST beacon ring, on the left side of the table. Your selected force's route stays visible as it advances.";
                    case 2: return "Keep troops inside the ring until its flag turns blue. Scouts add capture strength; more troops capture faster.";
                    case 3: return "Press 2 or click Rifle to recruit reinforcements at your headquarters. Supplies and preparation time are shown below.";
                    default: return "You selected, moved, captured and recruited. Enter battle to activate both opposing commanders. Your army keeps its position.";
                }
            }
        }
        public string SelectedOrderSummary
        {
            get
            {
                var units = Sim.Units.Where(u => u.Alive && Selected.Contains(u.Id)).ToArray();
                if (units.Length == 0) return "Choose a force to give orders";
                int moving = units.Count(u => u.HasOrder && Vector2.Distance(u.Position, u.Destination) > 1.2f);
                if (units.Any(u => u.AttackTargetId >= 0)) return "Focus fire · enemy unit";
                if (moving == 0) return "Holding ground · attacks nearby enemies";
                Vector2 destination = Vector2.zero;
                foreach (var unit in units.Where(u => u.HasOrder)) destination += unit.Destination;
                destination /= Mathf.Max(1, units.Count(u => u.HasOrder));
                var point = Sim.Points.OrderBy(p => (p.Position - destination).sqrMagnitude).First();
                string place = Vector2.Distance(point.Position, destination) < 6 ? point.Name + " beacon" : "marked position";
                return moving + " advancing · " + place;
            }
        }
        public string SelectedRoleSummary
        {
            get
            {
                var units = Sim.Units.Where(u => u.Alive && Selected.Contains(u.Id)).ToArray();
                if (units.Length == 0) return "Scouts capture · Rifles use cover · Tanks break armor";
                int protectedCount = units.Count(u => Sim.IsProtected(u));
                var kinds = units.Select(u => u.Kind).Distinct().ToArray();
                string role = kinds.Length == 1 ? kinds[0] == UnitKind.Scout ? "Fast objective capture" : kinds[0] == UnitKind.Rifle ? "Hold cover · 35% less incoming damage" : "Break armor · 20% more damage to tanks" : "Scouts capture · Rifles hold cover · Tanks break armor";
                return role + (protectedCount > 0 ? "\n" + protectedCount + " rifle units protected by cover" : "");
            }
        }
        public string BattleRecap
        {
            get
            {
                if (!Sim.Finished) return "";
                if (Sim.Winner < 0) return "Draw · all headquarters fell.\nChoose Rematch for a fresh battle.";
                Army winner = Sim.Armies[Sim.Winner];
                string reason = winner.Score >= BattleSimulation.VictoryScore ? "won through beacon control" : "was the last headquarters standing";
                var lines = new List<string> { winner.Name + " " + reason + "." };
                foreach (Army army in Sim.Armies)
                    lines.Add(army.Name + "  ·  " + army.BeaconsCaptured + " captures  ·  " + army.UnitsDestroyed + " destroyed / " + army.UnitsLost + " lost");
                if (!startedAsSpectator) lines.Add("Your reinforcements: " + Sim.Armies[0].ReinforcementsDeployed + "  ·  Combined beacon hold: " + FormatTime(Sim.Armies[0].BeaconSeconds));
                if (!string.IsNullOrEmpty(Sim.LastTurningPoint)) lines.Add(FormatTime(Sim.LastTurningPointTime) + "  ·  " + Sim.LastTurningPoint);
                return string.Join("\n", lines);
            }
        }
        public bool ReducedMotion
        {
            get => reducedMotion;
            set { reducedMotion = value; PlayerPrefs.SetInt("Tablefront.ReducedMotion", value ? 1 : 0); }
        }
        public float MasterVolume
        {
            get => masterVolume;
            set { masterVolume = Mathf.Clamp01(value); AudioListener.volume = masterVolume; PlayerPrefs.SetFloat("Tablefront.Volume", masterVolume); }
        }
        public string SelectionSummary
        {
            get
            {
                if (Spectator) return "SPECTATOR  /  Three AI commanders";
                var troops = Sim.Units.Where(u => u.Alive && Selected.Contains(u.Id)).ToArray();
                if (troops.Length == 0) return "No units selected";
                return troops.Length + " selected  /  " + string.Join("  ", troops.GroupBy(u => u.Kind).Select(g => g.Count() + " " + g.Key));
            }
        }

        Transform eye;
        CinemachineCamera rig;
        Vector3 focus = new Vector3(0, 0, -2), wantedFocus;
        float distance = 64, wantedDistance = 64, yaw = 0, wantedYaw;
        float accumulator, noticeUntil;
        string notice = "";
        bool reducedMotion, dragging, pressStartedInWorld;
        float masterVolume = .65f;
        Vector2 pressPosition;
        int matchIndex;
        readonly HashSet<int>[] groups = { new HashSet<int>(), new HashSet<int>(), new HashSet<int>() };
        readonly bool[] customGroups = new bool[3];
        readonly int[] previousOwners = new int[3];
        float previousBaseHealth, nextDangerAlert, alertUntil;
        string tacticalAlert = "";
        Vector2 alertPosition;
        int alertPriority;
        bool startedAsSpectator, tutorialMoveIssued, tutorialRecruited;
        AudioSource uiAudio;
        AudioClip commandTone;
        readonly Plane ground = new Plane(Vector3.up, Vector3.zero);

        void Awake()
        {
            Application.targetFrameRate = 144;
            TablefrontGraphics.Configure();
#if !UNITY_EDITOR
            if (!Application.isBatchMode) TablefrontGraphics.NativeFullscreen();
#endif
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            masterVolume = PlayerPrefs.GetFloat("Tablefront.Volume", .65f);
            reducedMotion = PlayerPrefs.GetInt("Tablefront.ReducedMotion", 0) != 0;
            AudioListener.volume = masterVolume;
            var camObject = new GameObject("Tabletop lens", typeof(Camera), typeof(AudioListener), typeof(CinemachineBrain));
            ViewCamera = camObject.GetComponent<Camera>();
            ViewCamera.tag = "MainCamera";
            ViewCamera.nearClipPlane = .3f; ViewCamera.farClipPlane = 180;
            ViewCamera.fieldOfView = 43;
            eye = new GameObject("Strategy camera dolly").transform;
            rig = new GameObject("Tablefront Cinemachine rig").AddComponent<CinemachineCamera>();
            rig.Follow = eye; rig.Lens.FieldOfView = 43;
            rig.Lens.NearClipPlane = .3f; rig.Lens.FarClipPlane = 180;
            rig.gameObject.AddComponent<CinemachineHardLockToTarget>();
            rig.gameObject.AddComponent<CinemachineRotateWithFollowTarget>();
            wantedFocus = focus;
            UpdateCamera(1, true);
            World = new GameObject("Miniature battlefield").AddComponent<TablefrontWorld>();
            World.Build(ViewCamera);
            Sim = new BattleSimulation(false);
            World.Render(Sim, Selected);
            HUD = new GameObject("Tablefront interface").AddComponent<TablefrontHUD>();
            HUD.Build(this, ViewCamera);
            uiAudio = gameObject.AddComponent<AudioSource>();
            uiAudio.playOnAwake = false; uiAudio.spatialBlend = 0; uiAudio.volume = .22f;
            commandTone = MakeTone();
            HUD.Refresh();
        }

        void Update()
        {
            ReadInput();
            bool running = !InMenu && !Paused && !HelpOpen && !Sim.Finished;
            if (running)
            {
                accumulator += Mathf.Min(Time.unscaledDeltaTime, .15f) * Speed;
                while (accumulator >= .05f)
                {
                    Sim.Step(.05f); World.PlayEvents(Sim.Events); ObserveBattle(); accumulator -= .05f;
                    if (Sim.Finished) { accumulator = 0; Notify(Sim.Winner >= 0 ? Sim.Armies[Sim.Winner].Name + " wins the battle" : "Draw · all headquarters fell"); break; }
                }
            }
            Selected.RemoveWhere(id => !Sim.Units.Any(u => u.Id == id && u.Alive));
            foreach (var group in groups) group.RemoveWhere(id => !Sim.Units.Any(u => u.Id == id && u.Alive));
            UpdateTutorial();
            if (!InMenu && !Spectator && !Sim.Armies[0].Alive && !Sim.Finished)
            {
                Spectator = true; Selected.Clear(); Notify("Your HQ fell. Watching the remaining armies; Escape opens the menu.");
            }
            World.ReducedMotion = ReducedMotion;
            World.Render(Sim, Selected);
            HUD.Refresh();
        }

        void LateUpdate() { UpdateCamera(Time.unscaledDeltaTime); }

        public void StartBattle(bool spectator)
        {
            World.ClearBattle();
            Sim = new BattleSimulation(spectator, 42 + matchIndex++);
            startedAsSpectator = spectator; TutorialActive = false; TutorialStep = 0;
            ResetBattleTracking();
            Spectator = spectator; InMenu = false; Paused = false; HelpOpen = false;
            Speed = 1; accumulator = 0; Selected.Clear();
            // Keep NORTH and the complete player HQ between the header and command deck.
            wantedFocus = new Vector3(0, 0, -12);
            wantedDistance = 68; wantedYaw = 0;
            if (!spectator) foreach (var u in Sim.Units.Where(u => u.Team == 0 && u.Alive)) Selected.Add(u.Id);
            Notify(spectator ? "Three commanders. One battlefield." : "Your army is selected. Right-click a command post to advance.");
            World.Render(Sim, Selected); HUD.Refresh();
        }
        public void StartTutorial()
        {
            StartBattle(false);
            TutorialActive = true; TutorialStep = 0; tutorialMoveIssued = tutorialRecruited = false;
            foreach (var army in Sim.Armies) army.IsAI = false;
            Selected.Clear(); noticeUntil = 0;
            HUD.Refresh();
        }
        public void Rematch() { StartBattle(startedAsSpectator); }
        public void SkipTutorial() { EndTutorial(); }
        public void CompleteTutorial() { if (TutorialActive && TutorialStep >= 4) EndTutorial(); }
        void EndTutorial()
        {
            if (!TutorialActive) return;
            TutorialActive = false;
            Sim.Armies[1].IsAI = Sim.Armies[2].IsAI = true;
            Notify("Live battle · both opposing commanders are now active");
        }
        void UpdateTutorial()
        {
            if (!TutorialActive) return;
            if (Sim.Finished || !Sim.Armies[0].Alive) { EndTutorial(); return; }
            if (TutorialStep == 0 && Selected.Count > 0) TutorialStep = 1;
            if (TutorialStep == 1 && tutorialMoveIssued) TutorialStep = 2;
            if (TutorialStep == 2 && Sim.OwnedPoints(0) > 0) TutorialStep = 3;
            if (TutorialStep == 3 && tutorialRecruited) TutorialStep = 4;
        }
        void ResetBattleTracking()
        {
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i].Clear(); customGroups[i] = false;
                foreach (var unit in Sim.Units) if (unit.Team == 0 && (int)unit.Kind == i) groups[i].Add(unit.Id);
            }
            for (int i = 0; i < previousOwners.Length; i++) previousOwners[i] = Sim.Points[i].Owner;
            previousBaseHealth = Sim.Armies[0].BaseHealth; nextDangerAlert = alertUntil = 0; tacticalAlert = "";
        }
        void ObserveBattle()
        {
            for (int i = 0; i < Sim.Points.Count; i++)
            {
                var point = Sim.Points[i];
                if (point.Owner == previousOwners[i]) continue;
                if (point.Owner == 0 || previousOwners[i] == 0) World.PlayCaptureCue(point.Owner == 0);
                if (!TutorialActive && previousOwners[i] == 0 && point.Owner != 0)
                    SetAlert(point.Name + " beacon lost", point.Position);
                previousOwners[i] = point.Owner;
            }
            float health = Sim.Armies[0].BaseHealth;
            if (!TutorialActive && !startedAsSpectator && health < previousBaseHealth && health > 0 && Sim.Elapsed >= nextDangerAlert)
            {
                SetAlert(health < BattleSimulation.BaseMaxHealth * .3f ? "Headquarters critical · defend now" : "Headquarters under attack", Sim.Armies[0].BasePosition, health < BattleSimulation.BaseMaxHealth * .3f ? 3 : 2);
                nextDangerAlert = Sim.Elapsed + 10;
            }
            previousBaseHealth = health;
        }
        void SetAlert(string message, Vector2 position, int priority = 1)
        {
            if (Time.unscaledTime < alertUntil && alertPriority > priority) return;
            tacticalAlert = message; alertPosition = position; alertPriority = priority; alertUntil = Time.unscaledTime + 9;
        }
        public void FocusAlert()
        {
            if (!TacticalAlertAvailable) return;
            wantedFocus = new Vector3(alertPosition.x, 0, alertPosition.y); wantedDistance = 42;
        }
        public int GroupCount(int slot) => slot >= 0 && slot < groups.Length ? groups[slot].Count : 0;
        public void AssignGroup(int slot)
        {
            if (!CanCommand || slot < 0 || slot >= groups.Length || Selected.Count == 0) return;
            groups[slot].Clear(); groups[slot].UnionWith(Selected); customGroups[slot] = true;
            Notify(new[] { "Alpha", "Bravo", "Charlie" }[slot] + " assigned · " + groups[slot].Count + " units");
        }
        public void RecallGroup(int slot)
        {
            if (!CanCommand || slot < 0 || slot >= groups.Length) return;
            if (groups[slot].Count == 0) { Notify("This force is empty. Select troops, then choose Set."); return; }
            Selected.Clear(); Selected.UnionWith(groups[slot]);
        }
        bool CanCommand => !InMenu && !Spectator && !Paused && !HelpOpen && !Sim.Finished && Sim.Armies[0].Alive;
        public bool IssueOrder(Vector2 location, int targetId = -1)
        {
            if (!CanCommand || Selected.Count == 0 || float.IsNaN(location.x) || float.IsNaN(location.y) || float.IsInfinity(location.x) || float.IsInfinity(location.y)) return false;
            if (targetId >= 0 && !Sim.Units.Any(u => u.Id == targetId && u.Alive && u.Team != 0)) return false;
            location = new Vector2(Mathf.Clamp(location.x, -24, 24), Mathf.Clamp(location.y, -24, 24));
            if (targetId >= 0) Sim.OrderAttack(Selected, targetId); else Sim.OrderMove(Selected, location);
            World.ShowOrder(location);
            if (TutorialActive && targetId < 0 && Sim.Points.Any(p => Vector2.Distance(p.Position, location) < BattleSimulation.CaptureRadius + 1.5f)) tutorialMoveIssued = true;
            Notify(targetId >= 0 ? "Focus fire" : "Advance to position");
            return true;
        }
        static string FormatTime(float seconds) => ((int)seconds / 60).ToString("00") + ":" + ((int)seconds % 60).ToString("00");
        public void ReturnToMenu()
        {
            InMenu = true; HelpOpen = false; Paused = false; Selected.Clear(); accumulator = 0;
            TutorialActive = false; TutorialStep = 0; alertUntil = 0;
            World.ClearBattle(); Sim = new BattleSimulation(false);
            ResetBattleTracking();
            wantedFocus = new Vector3(0, 0, -2); wantedDistance = 64; wantedYaw = 0;
            HUD.Refresh();
        }
        public void TogglePause() { if (InMenu || Sim.Finished) return; Paused = !Paused; dragging = false; HUD.DrawSelection(default, default, false); }
        public void ToggleHelp() { HelpOpen = !HelpOpen; dragging = false; HUD.DrawSelection(default, default, false); }
        public void CycleSpeed() { Speed = Speed == 1 ? 2 : 1; }
        public void Recruit(UnitKind kind)
        {
            if (InMenu || Spectator || Paused || HelpOpen || Sim.Finished) return;
            bool added = Sim.Recruit(0, kind);
            if (added)
            {
                var unit = Sim.Units.Last(u => u.Team == 0 && u.Alive && u.Kind == kind);
                if (!customGroups[(int)kind]) groups[(int)kind].Add(unit.Id);
                if (TutorialActive) tutorialRecruited = true;
                World.PlayRecruitCue();
            }
            Notify(added ? kind + " deployed at your HQ" : Sim.UnitCount(0) >= BattleSimulation.UnitCap ? "Army at capacity" : Sim.Armies[0].RecruitCooldown > 0 ? "Reinforcements preparing" : "Need " + BattleSimulation.Cost(kind) + " supplies");
        }
        public void SelectAll()
        {
            if (!CanCommand) return;
            Selected.Clear(); foreach (var u in Sim.Units.Where(u => u.Team == 0 && u.Alive)) Selected.Add(u.Id);
            Notify("All " + Selected.Count + " units selected");
        }
        public void FocusBase() { var p = Sim.Armies[0].BasePosition; wantedFocus = new Vector3(p.x, 0, p.y + 2); wantedDistance = 42; }
        public void ToggleCommander()
        {
            if (InMenu || Spectator || Sim.Finished || TutorialActive) return;
            Sim.Armies[0].IsAI = !Sim.Armies[0].IsAI;
            Notify(Sim.Armies[0].IsAI ? "AI commander engaged. You can still issue orders." : "Manual command restored");
        }
        public void ExitGame()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        public void Notify(string message) { notice = message; noticeUntil = Time.unscaledTime + 4; if (uiAudio && commandTone) uiAudio.PlayOneShot(commandTone); }

        void ReadInput()
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (kb != null)
            {
                if (kb.f11Key.wasPressedThisFrame) TablefrontGraphics.ToggleFullscreen();
                if (kb.escapeKey.wasPressedThisFrame) { if (HelpOpen) ToggleHelp(); else TogglePause(); }
                if (InMenu) return; // EventSystem Submit follows the focused menu button.
                if (kb.spaceKey.wasPressedThisFrame && !HelpOpen) TogglePause();
                if (Paused || HelpOpen || Sim.Finished) return;
                if (kb.digit1Key.wasPressedThisFrame) Recruit(UnitKind.Scout);
                if (kb.digit2Key.wasPressedThisFrame) Recruit(UnitKind.Rifle);
                if (kb.digit3Key.wasPressedThisFrame) Recruit(UnitKind.Tank);
                bool assigning = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
                for (int slot = 0; slot < 3; slot++)
                {
                    bool pressed = slot == 0 ? kb.digit4Key.wasPressedThisFrame : slot == 1 ? kb.digit5Key.wasPressedThisFrame : kb.digit6Key.wasPressedThisFrame;
                    if (pressed) { if (assigning) AssignGroup(slot); else RecallGroup(slot); }
                }
                if ((kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) && kb.aKey.wasPressedThisFrame) SelectAll();
                if (kb.homeKey.wasPressedThisFrame) FocusBase();
                if (kb.fKey.wasPressedThisFrame) FocusAlert();
                if (kb.tabKey.wasPressedThisFrame) ToggleCommander();
                if (kb.f1Key.wasPressedThisFrame) ToggleHelp();
                float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
                Vector3 right = Quaternion.Euler(0, wantedYaw, 0) * Vector3.right;
                Vector3 forward = Quaternion.Euler(0, wantedYaw, 0) * Vector3.forward;
                float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0);
                float z = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0);
                if (!(kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed)) wantedFocus += (right * x + forward * z) * dt * wantedDistance * .35f;
                wantedYaw += ((kb.eKey.isPressed ? 1 : 0) - (kb.qKey.isPressed ? 1 : 0)) * dt * 65;
            }
            if (InMenu || Paused || HelpOpen || Sim.Finished || mouse == null) return;
            Vector2 pointer = mouse.position.ReadValue();
            if (!HUD.PointerOverUI)
            {
                wantedDistance = Mathf.Clamp(wantedDistance - mouse.scroll.ReadValue().y * .018f, 26, 83);
                if (mouse.middleButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    wantedFocus -= Quaternion.Euler(0, wantedYaw, 0) * new Vector3(delta.x, 0, delta.y) * wantedDistance * .0012f;
                }
            }
            if (Spectator) return;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressStartedInWorld = !HUD.PointerOverUI; pressPosition = pointer; dragging = false;
            }
            if (pressStartedInWorld && mouse.leftButton.isPressed)
            {
                dragging |= Vector2.Distance(pressPosition, pointer) > 9;
                HUD.DrawSelection(pressPosition, pointer, dragging);
            }
            if (mouse.leftButton.wasReleasedThisFrame && pressStartedInWorld)
            {
                bool additive = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
                SelectAt(pressPosition, pointer, dragging, additive);
                HUD.DrawSelection(default, default, false); dragging = false; pressStartedInWorld = false;
            }
            if (mouse.rightButton.wasPressedThisFrame && !HUD.PointerOverUI && Selected.Count > 0 && TryGround(pointer, out var location))
            {
                int enemy = NearestAt(pointer, false, 24);
                IssueOrder(location, enemy);
            }
        }

        public void SelectAt(Vector2 start, Vector2 end, bool box, bool additive)
        {
            if (!CanCommand) return;
            if (!additive) Selected.Clear();
            if (box)
            {
                var rect = Rect.MinMaxRect(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y), Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y));
                foreach (var u in Sim.Units)
                {
                    if (!u.Alive || u.Team != 0) continue;
                    Vector3 screen = ViewCamera.WorldToScreenPoint(new Vector3(u.Position.x, .5f, u.Position.y));
                    if (screen.z > 0 && rect.Contains(screen)) Selected.Add(u.Id);
                }
            }
            else { int id = NearestAt(end, true, 27); if (id >= 0) { if (additive && Selected.Contains(id)) Selected.Remove(id); else Selected.Add(id); } }
        }
        int NearestAt(Vector2 position, bool friendly, float radius)
        {
            int id = -1; float best = radius * radius;
            foreach (var u in Sim.Units)
            {
                if (!u.Alive || (friendly ? u.Team != 0 : u.Team == 0)) continue;
                var screen = ViewCamera.WorldToScreenPoint(new Vector3(u.Position.x, .6f, u.Position.y));
                float d = ((Vector2)screen - position).sqrMagnitude;
                if (screen.z > 0 && d < best) { best = d; id = u.Id; }
            }
            return id;
        }
        public bool TryGround(Vector2 screen, out Vector2 position)
        {
            var ray = ViewCamera.ScreenPointToRay(screen);
            if (ground.Raycast(ray, out float hit))
            {
                var p = ray.GetPoint(hit); position = new Vector2(Mathf.Clamp(p.x, -24, 24), Mathf.Clamp(p.z, -24, 24)); return true;
            }
            position = default; return false;
        }
        void UpdateCamera(float dt, bool snap = false)
        {
            wantedFocus.x = Mathf.Clamp(wantedFocus.x, -20, 20); wantedFocus.z = Mathf.Clamp(wantedFocus.z, -20, 20);
            float blend = snap || ReducedMotion ? 1 : 1 - Mathf.Exp(-dt * 9);
            focus = Vector3.Lerp(focus, wantedFocus, blend); distance = Mathf.Lerp(distance, wantedDistance, blend); yaw = Mathf.LerpAngle(yaw, wantedYaw, blend);
            Quaternion angle = Quaternion.Euler(53, yaw, 0);
            eye.SetPositionAndRotation(focus - angle * Vector3.forward * distance, angle);
            if (snap) ViewCamera.transform.SetPositionAndRotation(eye.position, eye.rotation);
        }
        static AudioClip MakeTone()
        {
            const int rate = 22050; var data = new float[2205];
            for (int i = 0; i < data.Length; i++) { float t = (float)i / rate; data[i] = Mathf.Sin(2 * Mathf.PI * (630 - 130 * t) * t) * Mathf.Exp(-t * 40) * .2f; }
            var clip = AudioClip.Create("Tablefront order click", data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        void OnApplicationFocus(bool focused)
        {
            if (!focused && !InMenu && Sim != null && !Sim.Finished) { Paused = true; dragging = false; if (HUD) HUD.DrawSelection(default, default, false); }
        }
        void OnDestroy() { if (commandTone) Destroy(commandTone); }
    }
}
