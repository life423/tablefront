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
        public bool TutorialCompleted => PlayerPrefs.GetInt("Tablefront.TutorialCompleted", 0) != 0;
        public string TutorialFeedback => TutorialActive && Time.unscaledTime < tutorialFeedbackUntil ? tutorialFeedback : "";
        public string TutorialTitle => new[] { "Select your troops", "Move to the marker", "Capture WEST", "Recruit a rifle squad", "Attack the target", "Use rifle cover", "Tutorial complete" }[Mathf.Clamp(TutorialStep, 0, 6)];
        public int TutorialTargetId => TutorialActive && TutorialStep == 4 ? tutorialEnemyId : -1;
        public Vector2 TutorialTargetPosition
        {
            get
            {
                if (TutorialStep == 0) return FriendlyCentre();
                if (TutorialStep == 1) return tutorialMoveTarget;
                if (TutorialStep == 2 || TutorialStep >= 6) return Sim.Points[0].Position;
                if (TutorialStep == 3) return Sim.Armies[0].BasePosition;
                if (TutorialStep == 4) return Sim.Units.FirstOrDefault(u => u.Id == tutorialEnemyId)?.Position ?? tutorialEnemyPosition;
                return Sim.CoverZones[0].Position;
            }
        }
        public float TutorialTargetRadius => TutorialStep == 0 ? 2.8f : TutorialStep == 2 ? BattleSimulation.CaptureRadius : TutorialStep == 4 ? 1.1f : TutorialStep == 5 ? Sim.CoverZones[0].Radius : 1.8f;
        public string TutorialTargetLabel => new[] { "YOUR TROOPS", "MOVE HERE", "CAPTURE WEST", "YOUR HQ", "PRACTICE TARGET", "RIFLE COVER", "" }[Mathf.Clamp(TutorialStep, 0, 6)];
        public string CommandHint => Selected.Count == 0 ? "Click your troops or choose a force below" : "Click ground to move  ·  Click an enemy to attack";
        public string TutorialInstruction
        {
            get
            {
                switch (TutorialStep)
                {
                    case 0: return "Click a blue miniature, drag a box around troops, or click Select whole army below. A bright ring shows your selection.";
                    case 1: return "Click the gold MOVE HERE circle on the ground. Keep your troops selected and watch them reach the marker.";
                    case 2: return "Click the marked WEST beacon. Keep your troops inside its large ring until the flag turns blue.";
                    case 3: return "Click Recruit Rifles below. It spends credits and places a new rifle squad at your blue headquarters.";
                    case 4: return "Click the orange practice unit. Your selected troops move into range and fire automatically. Destroy it to continue.";
                    case 5: return "Click Rifles below, then click the marked cover position. A brass ring shows when a rifle squad is protected.";
                    default: return "You can select, move, capture, recruit, attack and use cover. Play a battle starts a fresh match against two AI commanders.";
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
        bool reducedMotion, dragging, pressStartedInWorld, navigatingUI;
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
        bool startedAsSpectator, tutorialMoveIssued, tutorialRecruited, tutorialAttackIssued, tutorialCoverIssued;
        readonly HashSet<int> tutorialMovingUnits = new HashSet<int>();
        readonly Vector2 tutorialMoveTarget = new Vector2(-7, -13);
        Vector2 tutorialEnemyPosition;
        int tutorialEnemyId = -1, tutorialRecruitBaseline;
        string tutorialFeedback = "";
        float tutorialFeedbackUntil;
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
            if (!InMenu && !Spectator && !TutorialActive && !Sim.Armies[0].Alive && !Sim.Finished)
            {
                Spectator = true; Selected.Clear(); Notify("Your HQ fell. Watching the remaining armies; Escape opens the menu.");
            }
            World.ReducedMotion = ReducedMotion;
            World.SetTutorialTarget(TutorialTargetPosition, TutorialTargetRadius, TutorialTargetLabel,
                TutorialActive && TutorialStep < 6 && !Paused && !HelpOpen && !InMenu);
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
            Speed = 1; accumulator = 0; Selected.Clear(); CancelPointerGesture();
            tutorialEnemyId = -1; tutorialMovingUnits.Clear(); tutorialFeedbackUntil = 0;
            // Keep NORTH and the complete player HQ between the header and command deck.
            wantedFocus = new Vector3(0, 0, -12);
            wantedDistance = 68; wantedYaw = 0;
            if (!spectator) foreach (var u in Sim.Units.Where(u => u.Team == 0 && u.Alive)) Selected.Add(u.Id);
            Notify(spectator ? "Three commanders. One battlefield." : "Choose a force below, then click a beacon to move.");
            World.Render(Sim, Selected); HUD.Refresh();
        }
        public void StartTutorial()
        {
            StartBattle(false);
            TutorialActive = true; TutorialStep = 0;
            Sim.PracticeMode = true;
            tutorialMoveIssued = tutorialRecruited = tutorialAttackIssued = tutorialCoverIssued = false;
            tutorialEnemyId = -1; tutorialMovingUnits.Clear(); tutorialFeedbackUntil = 0;
            foreach (var army in Sim.Armies) { army.IsAI = false; army.TurretTimer = 99999; }
            Sim.Units.RemoveAll(u => u.Team != 0);
            World.ClearBattle();
            Selected.Clear(); noticeUntil = 0;
            RecenterTutorial();
            World.Render(Sim, Selected);
            HUD.Refresh();
        }
        public void Rematch() { if (TutorialActive) StartTutorial(); else StartBattle(startedAsSpectator); }
        public void SkipTutorial() { if (TutorialActive) ReturnToMenu(); }
        public void CompleteTutorial()
        {
            if (!TutorialActive || TutorialStep < 6) return;
            PlayerPrefs.SetInt("Tablefront.TutorialCompleted", 1); PlayerPrefs.Save();
            StartBattle(false);
        }
        void UpdateTutorial()
        {
            if (!TutorialActive || Paused || HelpOpen || InMenu) return;
            switch (TutorialStep)
            {
                case 0:
                    if (Selected.Count > 0) AdvanceTutorial("Selected troops now follow your orders.");
                    break;
                case 1:
                    if (tutorialMoveIssued && Sim.Units.Any(u => u.Alive && tutorialMovingUnits.Contains(u.Id) && Vector2.Distance(u.Position, tutorialMoveTarget) < 2.4f))
                        AdvanceTutorial("Move complete. Click to give orders.");
                    break;
                case 2:
                    if (Sim.Points[0].Owner == 0) AdvanceTutorial("WEST captured. Beacons earn credits and points.");
                    break;
                case 3:
                    if (tutorialRecruited && Sim.Armies[0].ReinforcementsDeployed > tutorialRecruitBaseline)
                        AdvanceTutorial("Rifles recruited at your HQ.");
                    break;
                case 4:
                    var enemy = Sim.Units.FirstOrDefault(u => u.Id == tutorialEnemyId && u.Alive);
                    if (enemy == null)
                    {
                        if (tutorialAttackIssued) AdvanceTutorial("Target destroyed. Troops fire automatically.");
                        else CreateTutorialTarget();
                    }
                    break;
                case 5:
                    if (tutorialCoverIssued && Sim.Units.Any(u => u.Alive && u.Team == 0 && u.Kind == UnitKind.Rifle && Selected.Contains(u.Id) &&
                        Vector2.Distance(u.Position, Sim.CoverZones[0].Position) <= Sim.CoverZones[0].Radius && Sim.IsProtected(u)))
                        AdvanceTutorial("Rifles protected. All six tasks complete.");
                    break;
            }
        }
        void AdvanceTutorial(string feedback)
        {
            TutorialStep++; tutorialFeedback = feedback; tutorialFeedbackUntil = Time.unscaledTime + 3;
            if (TutorialStep == 3) { tutorialRecruited = false; tutorialRecruitBaseline = Sim.Armies[0].ReinforcementsDeployed; }
            if (TutorialStep == 4) CreateTutorialTarget();
            if (TutorialStep == 5) tutorialCoverIssued = false;
            RecenterTutorial();
        }
        void CreateTutorialTarget()
        {
            // An explicit stationary practice target, separate from ordinary AI battle setup.
            Sim.Armies[1].Credits = 200; Sim.Armies[1].RecruitCooldown = 0;
            if (!Sim.Recruit(1, UnitKind.Scout)) return;
            var enemy = Sim.Units.Last(u => u.Team == 1 && u.Kind == UnitKind.Scout);
            var candidates = new[] { new Vector2(6, -6), new Vector2(14, 0), new Vector2(-2, 13), new Vector2(-19, 4) };
            tutorialEnemyPosition = candidates.FirstOrDefault(p => Sim.Units.Where(u => u.Alive && u.Team == 0).All(u => Vector2.Distance(u.Position, p) > 12));
            if (tutorialEnemyPosition == default) tutorialEnemyPosition = candidates.OrderByDescending(p => Sim.Units.Where(u => u.Alive && u.Team == 0).Select(u => Vector2.Distance(u.Position, p)).DefaultIfEmpty(99).Min()).First();
            enemy.Position = enemy.Destination = tutorialEnemyPosition;
            enemy.HasOrder = false; enemy.AttackMove = false; enemy.WeaponTimer = 99999;
            tutorialEnemyId = enemy.Id; tutorialAttackIssued = false;
        }
        Vector2 FriendlyCentre()
        {
            var units = Sim.Units.Where(u => u.Alive && u.Team == 0).ToArray();
            if (units.Length == 0) return Sim.Armies[0].BasePosition;
            Vector2 sum = Vector2.zero; foreach (var unit in units) sum += unit.Position;
            return sum / units.Length;
        }
        public void RecenterTutorial()
        {
            if (!TutorialActive) return;
            Vector2 target = TutorialTargetPosition;
            Vector2 origin = TutorialStep == 0 ? target : FriendlyCentre();
            Vector2 centre = (origin + target) * .5f;
            wantedFocus = new Vector3(centre.x, 0, centre.y + 1.5f);
            wantedDistance = Mathf.Clamp(38 + Vector2.Distance(origin, target) * .9f, 42, 62);
            wantedYaw = 0;
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
        bool HasFocusedUI => UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null &&
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.activeInHierarchy;
        void ReleaseUIFocus()
        {
            navigatingUI = false;
            if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
        public bool IssueOrder(Vector2 location, int targetId = -1)
        {
            if (!CanCommand || Selected.Count == 0 || float.IsNaN(location.x) || float.IsNaN(location.y) || float.IsInfinity(location.x) || float.IsInfinity(location.y)) return false;
            if (targetId >= 0 && !Sim.Units.Any(u => u.Id == targetId && u.Alive && u.Team != 0)) return false;
            location = new Vector2(Mathf.Clamp(location.x, -24, 24), Mathf.Clamp(location.y, -24, 24));
            if (targetId >= 0) Sim.OrderAttack(Selected, targetId); else Sim.OrderMove(Selected, location);
            World.ShowOrder(location);
            if (TutorialActive)
            {
                if (TutorialStep == 1 && targetId < 0 && Vector2.Distance(location, tutorialMoveTarget) <= 2.5f)
                { tutorialMoveIssued = true; tutorialMovingUnits.Clear(); tutorialMovingUnits.UnionWith(Selected); }
                if (TutorialStep == 4 && targetId == tutorialEnemyId) tutorialAttackIssued = true;
                if (TutorialStep == 5 && targetId < 0 && Vector2.Distance(location, Sim.CoverZones[0].Position) <= Sim.CoverZones[0].Radius &&
                    Sim.Units.Any(u => u.Alive && u.Kind == UnitKind.Rifle && Selected.Contains(u.Id))) tutorialCoverIssued = true;
            }
            Notify(targetId >= 0 ? "Focus fire" : "Advance to position");
            return true;
        }
        static string FormatTime(float seconds) => ((int)seconds / 60).ToString("00") + ":" + ((int)seconds % 60).ToString("00");
        public void ReturnToMenu()
        {
            InMenu = true; HelpOpen = false; Paused = false; Selected.Clear(); accumulator = 0;
            TutorialActive = false; TutorialStep = 0; alertUntil = 0; CancelPointerGesture();
            World.ClearBattle(); Sim = new BattleSimulation(false);
            ResetBattleTracking();
            wantedFocus = new Vector3(0, 0, -2); wantedDistance = 64; wantedYaw = 0;
            HUD.Refresh();
        }
        void CancelPointerGesture() { dragging = pressStartedInWorld = false; if (HUD) HUD.DrawSelection(default, default, false); }
        public void TogglePause() { if (InMenu || Sim.Finished) return; Paused = !Paused; CancelPointerGesture(); }
        public void ToggleHelp() { HelpOpen = !HelpOpen; CancelPointerGesture(); }
        public void CycleSpeed() { Speed = Speed == 1 ? 2 : 1; }
        public void Recruit(UnitKind kind)
        {
            if (InMenu || Spectator || Paused || HelpOpen || Sim.Finished) return;
            if (TutorialActive && TutorialStep < 6 && (TutorialStep != 3 || kind != UnitKind.Rifle))
            { Notify("Follow the marked lesson. Recruitment unlocks at step 4."); return; }
            bool added = Sim.Recruit(0, kind);
            if (added)
            {
                var unit = Sim.Units.Last(u => u.Team == 0 && u.Alive && u.Kind == kind);
                if (!customGroups[(int)kind]) groups[(int)kind].Add(unit.Id);
                if (TutorialActive && TutorialStep == 3 && kind == UnitKind.Rifle) tutorialRecruited = true;
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
        public void SelectKind(UnitKind kind)
        {
            if (!CanCommand) return;
            ReleaseUIFocus();
            Selected.Clear();
            foreach (var unit in Sim.Units) if (unit.Alive && unit.Team == 0 && unit.Kind == kind) Selected.Add(unit.Id);
            if (Selected.Count == 0) Notify("No " + kind.ToString().ToLowerInvariant() + " units yet. Recruit one at your HQ.");
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
                if (kb.tabKey.wasPressedThisFrame) { navigatingUI = true; HUD.FocusNextControl(kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed); }
                if (kb.escapeKey.wasPressedThisFrame) { if (HelpOpen) ToggleHelp(); else TogglePause(); }
                if (InMenu) return; // EventSystem Submit follows the focused menu button.
                if (kb.spaceKey.wasPressedThisFrame && !HelpOpen && !HasFocusedUI) TogglePause();
                if (Paused || HelpOpen || Sim.Finished) return;
                if (kb.digit1Key.wasPressedThisFrame) SelectKind(UnitKind.Scout);
                if (kb.digit2Key.wasPressedThisFrame) SelectKind(UnitKind.Rifle);
                if (kb.digit3Key.wasPressedThisFrame) SelectKind(UnitKind.Tank);
                if (kb.zKey.wasPressedThisFrame) Recruit(UnitKind.Scout);
                if (kb.xKey.wasPressedThisFrame) Recruit(UnitKind.Rifle);
                if (kb.cKey.wasPressedThisFrame) Recruit(UnitKind.Tank);
                bool assigning = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
                for (int slot = 0; slot < 3; slot++)
                {
                    bool pressed = slot == 0 ? kb.digit4Key.wasPressedThisFrame : slot == 1 ? kb.digit5Key.wasPressedThisFrame : kb.digit6Key.wasPressedThisFrame;
                    if (pressed) { if (assigning) AssignGroup(slot); else RecallGroup(slot); }
                }
                if ((kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) && kb.aKey.wasPressedThisFrame) SelectAll();
                if (kb.homeKey.wasPressedThisFrame) FocusBase();
                if (kb.fKey.wasPressedThisFrame) FocusAlert();
                if (kb.f1Key.wasPressedThisFrame) ToggleHelp();
                float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
                Vector3 right = Quaternion.Euler(0, wantedYaw, 0) * Vector3.right;
                Vector3 forward = Quaternion.Euler(0, wantedYaw, 0) * Vector3.forward;
                float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0);
                float z = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0);
                bool rotate = kb.eKey.isPressed || kb.qKey.isPressed;
                if (!navigatingUI && (x != 0 || z != 0 || rotate)) ReleaseUIFocus();
                if ((!navigatingUI || !HasFocusedUI) && !(kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed)) wantedFocus += (right * x + forward * z) * dt * wantedDistance * .35f;
                if (!navigatingUI || !HasFocusedUI) wantedYaw += ((kb.eKey.isPressed ? 1 : 0) - (kb.qKey.isPressed ? 1 : 0)) * dt * 65;
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
                if (pressStartedInWorld) ReleaseUIFocus(); else navigatingUI = false;
            }
            if (pressStartedInWorld && mouse.leftButton.isPressed)
            {
                dragging |= Vector2.Distance(pressPosition, pointer) > 9;
                HUD.DrawSelection(pressPosition, pointer, dragging);
            }
            if (mouse.leftButton.wasReleasedThisFrame && pressStartedInWorld)
            {
                bool additive = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
                if (!HUD.PointerOverUI)
                {
                    if (dragging) SelectAt(pressPosition, pointer, true, additive);
                    else ClickWorld(pointer, additive);
                }
                HUD.DrawSelection(default, default, false); dragging = false; pressStartedInWorld = false;
            }
            if (mouse.rightButton.wasPressedThisFrame && !HUD.PointerOverUI && Selected.Count > 0 && TryGround(pointer, out var location))
            {
                int enemy = NearestAt(pointer, false, 24);
                IssueOrder(location, enemy);
            }
        }

        /// <summary>Mouse-first context: friendly selects; ground moves; an enemy is an attack order.</summary>
        public void ClickWorld(Vector2 screen, bool additive = false)
        {
            if (!CanCommand) return;
            ReleaseUIFocus();
            // In a lesson the marked destination is the explicit action target, even if
            // another friendly already stands there. Do not turn that click into a selection.
            if (TutorialActive && Selected.Count > 0 && (TutorialStep == 1 || TutorialStep == 2 || TutorialStep == 5) &&
                TryGround(screen, out var lessonGround) && Vector2.Distance(lessonGround, TutorialTargetPosition) <= TutorialTargetRadius)
            { IssueOrder(TutorialTargetPosition); return; }
            int friendly = NearestAt(screen, true, 25), enemy = NearestAt(screen, false, 25);
            float DistanceTo(int id)
            {
                var unit = Sim.Units.First(u => u.Id == id);
                return ((Vector2)ViewCamera.WorldToScreenPoint(new Vector3(unit.Position.x, .6f, unit.Position.y)) - screen).sqrMagnitude;
            }
            if (friendly >= 0 && (enemy < 0 || DistanceTo(friendly) <= DistanceTo(enemy)))
            { SelectAt(screen, screen, false, additive); return; }
            if (Selected.Count == 0) { Notify("Select blue troops or choose a force below first"); return; }
            if (enemy >= 0)
            {
                var target = Sim.Units.First(u => u.Id == enemy);
                IssueOrder(target.Position, enemy); return;
            }
            if (TryGround(screen, out var location)) IssueOrder(location);
        }

        public void SelectAt(Vector2 start, Vector2 end, bool box, bool additive)
        {
            if (!CanCommand) return;
            ReleaseUIFocus();
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
                var p = ray.GetPoint(hit);
                if (Mathf.Abs(p.x) > BattleSimulation.MapHalfSize || Mathf.Abs(p.z) > BattleSimulation.MapHalfSize) { position = default; return false; }
                position = new Vector2(Mathf.Clamp(p.x, -24, 24), Mathf.Clamp(p.z, -24, 24)); return true;
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
            if (!focused && !InMenu && Sim != null && !Sim.Finished) { Paused = true; CancelPointerGesture(); }
        }
        void OnDestroy() { if (commandTone) Destroy(commandTone); }
    }
}
