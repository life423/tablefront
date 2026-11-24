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
        public BattlefieldId SelectedBattlefield { get; private set; } = BattlefieldId.Greenfield;
        public bool AttackMoveArmed { get; private set; }
        public bool MoveArmed { get; private set; }
        public bool MissionActive => !InMenu && Sim != null && Sim.MissionActive;
        public string Notice => Time.unscaledTime < noticeUntil ? notice : "";
        public Camera ViewCamera { get; private set; }
        public TablefrontHUD HUD { get; private set; }
        public TablefrontWorld World { get; private set; }
        public TablefrontOrderPreview OrderPreview { get; private set; }
        public string PlanningHint => CanCommand && Selected.Count > 0 && OrderPreview ? OrderPreview.Hint : "";
        public int QueuedCount => Sim == null ? 0 : Sim.Units.Where(u => u.Alive && u.Team == 0 && Selected.Contains(u.Id)).Select(u => u.QueuedOrders.Count).DefaultIfEmpty(0).Max();
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
        public string CommandHint => Selected.Count == 0 ? "Click your troops or choose a force below" : AttackMoveArmed ? "Click to advance and engage · Shift adds a waypoint · Esc cancels" : MoveArmed ? "Click a destination · Shift adds a waypoint · Esc cancels" : "Click ground to move · Click an enemy to attack · Shift queues";
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
                    default: return "You can select, move, capture, recruit, attack and use cover. Select Play a battle to start a fresh match against two AI commanders.";
                }
            }
        }
        public string SelectedOrderSummary
        {
            get
            {
                var units = Sim.Units.Where(u => u.Alive && Selected.Contains(u.Id)).ToArray();
                if (units.Length == 0) return "Choose a force to give orders";
                int riding = Sim.MissionActive ? units.Count(u => Sim.Bridge.IsRiding(u.Id)) : 0;
                if (riding > 0) return riding + " riding bridge · orders resume when docked";
                int waiting = units.Count(u => u.WaitingForBridge);
                if (waiting > 0) return waiting + (waiting == 1 ? " unit waiting for the crossing" : " units waiting for the crossing");
                if (units.All(u => u.HoldPosition)) return "Holding position · fire at enemies in range";
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
                string role = kinds.Length == 1 ? BattleSimulation.RoleDescription(kinds[0]) : "Scouts capture · Rifles hold cover · Support units need escorts";
                return role + (protectedCount > 0 ? "\n" + protectedCount + (protectedCount == 1 ? " rifle unit protected by cover" : " rifle units protected by cover") : "");
            }
        }
        public string BattleRecap
        {
            get
            {
                if (!Sim.Finished) return "";
                if (Sim.Winner < 0) return "Draw · " + (Sim.MissionActive ? Sim.MissionWinReason : "all headquarters fell") + ".\nChoose Rematch for a fresh battle.";
                Army winner = Sim.Armies[Sim.Winner];
                string reason = Sim.MissionActive ? Sim.MissionHoldSeconds(Sim.Winner) >= BattleSimulation.MissionHoldRequired ? "held the enemy Depot for 45 seconds" : "destroyed the opposing headquarters" : winner.Score >= BattleSimulation.VictoryScore ? "won through beacon control" : "was the last headquarters standing";
                var lines = new List<string> { winner.Name + " " + reason + "." };
                foreach (Army army in Sim.Armies)
                {
                    if (Sim.MissionActive && army.Id > 1) continue;
                    lines.Add(army.Name + "  ·  " + army.BeaconsCaptured + " captures  ·  " + army.UnitsDestroyed + " destroyed / " + army.UnitsLost + " lost");
                }
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
                return troops.Length + " selected / " + string.Join(" · ", troops.GroupBy(u => u.Kind).Select(g => g.Count() + " " + BattleSimulation.UnitName(g.Key, g.Count() != 1)));
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
        int lastClickedUnit = -1;
        float lastUnitClickTime = -10;
        public const int ControlGroupCount = 10;
        readonly HashSet<int>[] groups = Enumerable.Range(0, ControlGroupCount).Select(_ => new HashSet<int>()).ToArray();
        int lastGroupSlot = -1;
        float lastGroupTap = -10;
        readonly int[] previousOwners = new int[3];
        float previousBaseHealth, nextDangerAlert, alertUntil;
        string tacticalAlert = "";
        Vector2 alertPosition;
        int alertPriority;
        bool startedAsSpectator, tutorialMoveIssued, tutorialRecruited, tutorialCoverIssued;
        Troop tutorialEnemy;
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
            SelectedBattlefield = TablefrontBattlefields.Get((BattlefieldId)PlayerPrefs.GetInt("Tablefront.Battlefield", 0)).Id;
            if (SelectedBattlefield == BattlefieldId.BridgeGambit) SelectedBattlefield = BattlefieldId.Greenfield;
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
            Sim = new BattleSimulation(false, 42, SelectedBattlefield);
            World.SetBattlefield(Sim);
            World.Render(Sim, Selected);
            HUD = new GameObject("Tablefront interface").AddComponent<TablefrontHUD>();
            HUD.Build(this, ViewCamera);
            OrderPreview = new GameObject("Contextual order planning").AddComponent<TablefrontOrderPreview>();
            OrderPreview.Build();
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
            World.HoveredUnitId = !InMenu && !Paused && !HelpOpen && Mouse.current != null && !HUD.PointerOverUI
                ? NearestUnitAt(Mouse.current.position.ReadValue(), 27) : -1;
            World.SetTutorialTarget(TutorialTargetPosition, TutorialTargetRadius, TutorialTargetLabel,
                TutorialActive && TutorialStep < 6 && !Paused && !HelpOpen && !InMenu);
            World.Render(Sim, Selected);
            OrderPreview.Render(this, dragging);
            HUD.Refresh();
        }

        void LateUpdate() { UpdateCamera(Time.unscaledDeltaTime); }

        public void StartBattle(bool spectator)
        {
            StartBattleOn(spectator, SelectedBattlefield);
        }
        void StartBattleOn(bool spectator, BattlefieldId battlefield)
        {
            World.ClearBattle();
            Sim = new BattleSimulation(spectator, 42 + matchIndex++, battlefield) { ExpandedRoster = battlefield != BattlefieldId.BridgeGambit };
            World.SetBattlefield(Sim);
            startedAsSpectator = spectator; TutorialActive = false; TutorialStep = 0;
            ResetBattleTracking();
            Spectator = spectator; InMenu = false; Paused = false; HelpOpen = false;
            Speed = 1; accumulator = 0; Selected.Clear(); CancelPointerGesture();
            tutorialEnemyId = -1; tutorialMovingUnits.Clear(); tutorialFeedbackUntil = 0;
            // Keep NORTH and the complete player HQ between the header and command deck.
            bool northernHQ = battlefield == BattlefieldId.RiverCrossing;
            wantedFocus = new Vector3(0, 0, northernHQ ? -7 : -12);
            wantedDistance = northernHQ ? 74 : 68; wantedYaw = 0;
            if (Sim.MissionActive) { wantedFocus = new Vector3(0, 0, -2); wantedDistance = 73; }
            if (!spectator) foreach (var u in Sim.Units.Where(u => u.Team == 0 && u.Alive)) Selected.Add(u.Id);
            Notify(Sim.MissionActive ? "Bridge Gambit · Capture the enemy Depot or destroy its HQ" : spectator ? "Three commanders. One battlefield." : "Choose a force below, then click a beacon to move.");
            World.Render(Sim, Selected); HUD.Refresh();
        }
        public void StartTutorial()
        {
            StartBattleOn(false, BattlefieldId.Greenfield);
            TutorialActive = true; TutorialStep = 0;
            Sim.PracticeMode = true; Sim.ExpandedRoster = false;
            tutorialMoveIssued = tutorialRecruited = tutorialCoverIssued = false;
            tutorialEnemyId = -1; tutorialEnemy = null; tutorialMovingUnits.Clear(); tutorialFeedbackUntil = 0;
            foreach (var army in Sim.Armies) { army.IsAI = false; army.TurretTimer = 99999; }
            Sim.Units.RemoveAll(u => u.Team != 0);
            World.ClearBattle();
            Selected.Clear(); noticeUntil = 0;
            RecenterTutorial();
            World.Render(Sim, Selected);
            HUD.Refresh();
        }
        public void StartBridgeMission() { StartBattleOn(false, BattlefieldId.BridgeGambit); }
        public void Rematch() { if (TutorialActive) StartTutorial(); else if (Sim.MissionActive) StartBridgeMission(); else StartBattle(startedAsSpectator); }
        public void SetBattlefield(BattlefieldId battlefield)
        {
            if (!InMenu) return;
            if (battlefield == BattlefieldId.BridgeGambit) battlefield = BattlefieldId.Greenfield;
            SelectedBattlefield = TablefrontBattlefields.Get(battlefield).Id;
            PlayerPrefs.SetInt("Tablefront.Battlefield", (int)SelectedBattlefield);
            World.ClearBattle();
            Sim = new BattleSimulation(false, 42, SelectedBattlefield);
            World.SetBattlefield(Sim); World.Render(Sim, Selected); HUD.Refresh();
        }
        public void NextBattlefield()
        {
            if (!Sim.Finished || TutorialActive || Sim.MissionActive) return;
            SelectedBattlefield = TablefrontBattlefields.All[((int)Sim.Battlefield.Id + 1) % TablefrontBattlefields.All.Length].Id;
            PlayerPrefs.SetInt("Tablefront.Battlefield", (int)SelectedBattlefield);
            StartBattle(startedAsSpectator);
        }
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
                    // Combat removes dead units from the simulation list. Keep the target
                    // reference so a real kill counts even when troops fired automatically.
                    if (tutorialEnemy != null && !tutorialEnemy.Alive)
                        AdvanceTutorial("Target destroyed. Troops fire automatically.");
                    else if (!Sim.Units.Any(u => u.Id == tutorialEnemyId && u.Alive))
                    { CreateTutorialTarget(); RecenterTutorial(); }
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
            Sim.Armies[1].BaseHealth = BattleSimulation.BaseMaxHealth;
            Sim.Armies[1].IsAI = false; Sim.Armies[1].TurretTimer = 99999;
            Sim.Armies[1].Credits = 200; Sim.Armies[1].RecruitCooldown = 0;
            if (!Sim.Recruit(1, UnitKind.Scout)) return;
            var enemy = Sim.Units.Last(u => u.Team == 1 && u.Kind == UnitKind.Scout);
            var candidates = new[] { new Vector2(6, -6), new Vector2(14, 0), new Vector2(-2, 13), new Vector2(-19, 4) };
            tutorialEnemyPosition = candidates.FirstOrDefault(p => Sim.Units.Where(u => u.Alive && u.Team == 0).All(u => Vector2.Distance(u.Position, p) > 12));
            if (tutorialEnemyPosition == default) tutorialEnemyPosition = candidates.OrderByDescending(p => Sim.Units.Where(u => u.Alive && u.Team == 0).Select(u => Vector2.Distance(u.Position, p)).DefaultIfEmpty(99).Min()).First();
            enemy.Position = enemy.Destination = tutorialEnemyPosition;
            enemy.HasOrder = false; enemy.AttackMove = false; enemy.WeaponTimer = 99999;
            tutorialEnemyId = enemy.Id; tutorialEnemy = enemy;
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
            Vector2 centre = TutorialStep == 4 ? target : (origin + target) * .5f;
            wantedFocus = new Vector3(centre.x, 0, centre.y + 1.5f);
            wantedDistance = Mathf.Clamp(38 + Vector2.Distance(origin, target) * .9f, 42, 62);
            wantedYaw = 0;
        }
        void ResetBattleTracking()
        {
            foreach (var group in groups) group.Clear();
            ResetGroupTap();
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
        static string GroupKey(int slot) => ((slot + 1) % 10).ToString();
        bool ValidGroup(int slot) => slot >= 0 && slot < groups.Length;
        void PruneGroup(int slot) => groups[slot].RemoveWhere(id => !Sim.Units.Any(u => u.Id == id && u.Alive && u.Team == 0));
        void ResetGroupTap() { lastGroupSlot = -1; lastGroupTap = -10; }
        public int GroupCount(int slot)
        {
            if (!ValidGroup(slot)) return 0;
            PruneGroup(slot); return groups[slot].Count;
        }
        public void AssignGroup(int slot)
        {
            if (!CanCommand || !ValidGroup(slot)) return;
            ResetGroupTap();
            var members = Sim.Units.Where(u => u.Alive && u.Team == 0 && Selected.Contains(u.Id)).Select(u => u.Id).ToArray();
            if (members.Length == 0) { Notify("Select your troops, then press Ctrl+" + GroupKey(slot) + " to save a group."); return; }
            ReleaseUIFocus();
            groups[slot].Clear(); groups[slot].UnionWith(members);
            Notify("Group " + GroupKey(slot) + " saved · " + groups[slot].Count + " units");
        }
        public void AddToGroup(int slot)
        {
            if (!CanCommand || !ValidGroup(slot)) return;
            ResetGroupTap(); PruneGroup(slot);
            var members = Sim.Units.Where(u => u.Alive && u.Team == 0 && Selected.Contains(u.Id)).Select(u => u.Id).ToArray();
            if (members.Length == 0) { Notify("Select troops to add to group " + GroupKey(slot) + "."); return; }
            ReleaseUIFocus(); groups[slot].UnionWith(members);
            Notify("Group " + GroupKey(slot) + " updated · " + groups[slot].Count + " units");
        }
        public void RecallGroup(int slot)
        {
            if (!CanCommand || !ValidGroup(slot)) return;
            ReleaseUIFocus(); PruneGroup(slot);
            if (groups[slot].Count == 0) { Notify("Group " + GroupKey(slot) + " is empty. Select troops, then press Ctrl+" + GroupKey(slot) + "."); return; }
            CancelPlanning();
            Selected.Clear(); Selected.UnionWith(groups[slot]);
            Notify("Group " + GroupKey(slot) + " selected · " + Selected.Count + " units");
        }
        public void FocusGroup(int slot)
        {
            if (!CanCommand || !ValidGroup(slot)) return;
            PruneGroup(slot);
            if (groups[slot].Count == 0) return;
            Vector2 centre = Vector2.zero;
            foreach (var unit in Sim.Units) if (groups[slot].Contains(unit.Id)) centre += unit.Position;
            centre /= groups[slot].Count;
            wantedFocus = new Vector3(centre.x, 0, centre.y);
        }
        void ReadGroupKey(int slot, bool assigning, bool adding)
        {
            if (!CanCommand) return;
            if (assigning) { AssignGroup(slot); return; }
            if (adding) { AddToGroup(slot); return; }
            bool center = lastGroupSlot == slot && Time.unscaledTime - lastGroupTap <= .35f;
            RecallGroup(slot);
            if (center) FocusGroup(slot);
            lastGroupSlot = slot; lastGroupTap = Time.unscaledTime;
        }
        bool CanCommand => !InMenu && !Spectator && !Paused && !HelpOpen && !Sim.Finished && Sim.Armies[0].Alive;
        bool HasFocusedUI => UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null &&
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.activeInHierarchy;
        void ReleaseUIFocus()
        {
            ResetGroupTap();
            navigatingUI = false;
            if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
        public bool IssueOrder(Vector2 location, int targetId = -1, bool attackMove = false, bool append = false)
        {
            if (!CanCommand || Selected.Count == 0 || float.IsNaN(location.x) || float.IsNaN(location.y) || float.IsInfinity(location.x) || float.IsInfinity(location.y)) return false;
            if (targetId >= 0 && !Sim.Units.Any(u => u.Id == targetId && u.Alive && u.Team != 0)) return false;
            int commanded = 0, available = 0;
            foreach (var unit in Sim.Units)
            {
                if (!unit.Alive || unit.Team != 0 || !Selected.Contains(unit.Id)) continue;
                commanded++;
                if (!append || !unit.HasOrder || unit.QueuedOrders.Count < BattleSimulation.MaxQueuedOrders) available++;
            }
            if (commanded == 0) return false;
            if (available == 0) { Notify("Plan full · 12 queued orders · release Shift to replace"); return false; }
            location = new Vector2(Mathf.Clamp(location.x, -24, 24), Mathf.Clamp(location.y, -24, 24));
            if (Sim.MissionActive && targetId < 0)
            {
                var largest = Sim.Units.Where(u => u.Alive && u.Team == 0 && Selected.Contains(u.Id)).OrderByDescending(u => BattleSimulation.UnitRadius(u.Kind)).FirstOrDefault();
                if (largest == null) return false;
                if (!Sim.DestinationAllowed(location, largest.Kind))
                { Notify("Water and stone walls block movement · choose open ground"); return false; }
                if (Sim.Bridge.Phase != BridgePhase.Stable && Mathf.Abs(location.x) < TablefrontBridge.RiverHalfWidth + BattleSimulation.UnitRadius(largest.Kind))
                { Notify("The bridge is switching. Choose a landing bank."); return false; }
            }
            int accepted = targetId >= 0 ? Sim.OrderAttack(Selected, targetId, append) : Sim.OrderMove(Selected, location, attackMove, append);
            if (accepted == 0) { Notify("No order accepted · choose a legal destination or replace a full queue"); return false; }
            AttackMoveArmed = MoveArmed = false; lastClickedUnit = -1;
            World.ShowOrder(location);
            if (TutorialActive)
            {
                if (TutorialStep == 1 && targetId < 0 && Vector2.Distance(location, tutorialMoveTarget) <= 2.5f)
                { tutorialMoveIssued = true; tutorialMovingUnits.Clear(); tutorialMovingUnits.UnionWith(Selected); }
                if (TutorialStep == 5 && targetId < 0 && Vector2.Distance(location, Sim.CoverZones[0].Position) <= Sim.CoverZones[0].Radius &&
                    Sim.Units.Any(u => u.Alive && u.Kind == UnitKind.Rifle && Selected.Contains(u.Id))) tutorialCoverIssued = true;
            }
            bool bridgeWait = Sim.MissionActive && Sim.Bridge.Phase != BridgePhase.Stable &&
                Sim.Units.Any(u => u.Alive && u.Team == 0 && Selected.Contains(u.Id) && u.Position.x * location.x < 0);
            Notify(accepted < commanded ? "Queued for " + accepted + " of " + commanded + " troops · some queues are full" :
                bridgeWait ? "Order saved · waiting for the bridge to dock" :
                append ? targetId >= 0 ? "Attack queued · finish current orders first" : "Waypoint queued · Shift-click to add another" :
                targetId >= 0 ? "Focus fire" : attackMove ? "Advance and engage" : "Move to position");
            return true;
        }
        public void ArmMove()
        {
            if (!CanCommand || Selected.Count == 0) return;
            ReleaseUIFocus();
            MoveArmed = !MoveArmed; AttackMoveArmed = false;
            Notify(MoveArmed ? "Choose a destination · Shift-click queues it" : "Move cancelled");
        }
        public void CancelPlanning()
        {
            AttackMoveArmed = MoveArmed = false;
            if (OrderPreview) OrderPreview.HideHover();
        }
        public void ToggleAttackMove()
        {
            if (!CanCommand || Selected.Count == 0) return;
            ReleaseUIFocus();
            AttackMoveArmed = !AttackMoveArmed;
            MoveArmed = false;
            Notify(AttackMoveArmed ? "Choose an attack-move destination" : "Attack-move cancelled");
        }
        public void StopSelected()
        {
            if (!CanCommand || Selected.Count == 0) return;
            ReleaseUIFocus(); Sim.OrderStop(Selected); CancelPointerGesture(); Notify("Selected troops stopped");
        }
        public void HoldSelected()
        {
            if (!CanCommand || Selected.Count == 0) return;
            ReleaseUIFocus(); Sim.OrderStop(Selected, true); CancelPointerGesture(); Notify("Hold position · fire without pursuing");
        }
        public bool RequestBridge(BridgeRoute route)
        {
            if (!CanCommand || !Sim.MissionActive) return false;
            if (Sim.Bridge.Phase != BridgePhase.Stable) { Notify("The bridge is already changing routes"); return false; }
            if (route == Sim.Bridge.CurrentRoute) { Notify("That crossing is already open"); return false; }
            if (Sim.Bridge.CommandsRemaining(0) <= 0) { Notify("All three bridge commands are spent"); return false; }
            if (Sim.Bridge.CooldownRemaining(0) > 0) { Notify("Bridge control ready in " + Mathf.CeilToInt(Sim.Bridge.CooldownRemaining(0)) + " seconds"); return false; }
            if (!Sim.TryRequestBridge(0, route)) return false;
            ReleaseUIFocus(); CancelPointerGesture();
            Notify("Bridge switch queued · three-second warning"); return true;
        }
        public bool ToggleBridge()
        {
            if (Sim == null || !Sim.MissionActive) return false;
            return RequestBridge(Sim.Bridge.CurrentRoute == BridgeRoute.North ? BridgeRoute.South : BridgeRoute.North);
        }
        static string FormatTime(float seconds) => ((int)seconds / 60).ToString("00") + ":" + ((int)seconds % 60).ToString("00");
        public void ReturnToMenu()
        {
            InMenu = true; HelpOpen = false; Paused = false; Selected.Clear(); accumulator = 0;
            TutorialActive = false; TutorialStep = 0; alertUntil = 0; CancelPointerGesture();
            World.ClearBattle(); Sim = new BattleSimulation(false, 42, SelectedBattlefield); World.SetBattlefield(Sim);
            ResetBattleTracking();
            wantedFocus = new Vector3(0, 0, -2); wantedDistance = 64; wantedYaw = 0;
            HUD.Refresh();
        }
        void CancelPointerGesture() { dragging = pressStartedInWorld = false; CancelPlanning(); lastClickedUnit = -1; ResetGroupTap(); if (HUD) HUD.DrawSelection(default, default, false); if (OrderPreview) OrderPreview.Hide(); }
        public void TogglePause() { if (InMenu || Sim.Finished) return; Paused = !Paused; CancelPointerGesture(); }
        public void ToggleHelp() { HelpOpen = !HelpOpen; CancelPointerGesture(); }
        public void CycleSpeed() { Speed = Speed == 1 ? 2 : 1; }
        public void Recruit(UnitKind kind)
        {
            if (InMenu || Spectator || Paused || HelpOpen || Sim.Finished) return;
            if (Sim.MissionActive && (kind == UnitKind.Gunner || kind == UnitKind.Artillery))
            { Notify("Bridge Gambit uses Scouts, Rifles and Tanks"); return; }
            if (TutorialActive && TutorialStep < 6 && (TutorialStep != 3 || kind != UnitKind.Rifle))
            { Notify("Follow the marked lesson. Recruitment unlocks at step 4."); return; }
            bool added = Sim.Recruit(0, kind);
            if (added)
            {
                if (TutorialActive && TutorialStep == 3 && kind == UnitKind.Rifle) tutorialRecruited = true;
                World.PlayRecruitCue();
            }
            Notify(added ? BattleSimulation.UnitName(kind) + " deployed at your HQ" : Sim.UnitCount(0) >= BattleSimulation.UnitCap ? "Army at capacity" : Sim.Armies[0].RecruitCooldown > 0 ? "Reinforcements preparing" : "Need " + BattleSimulation.Cost(kind) + " credits");
        }
        public void SelectAll()
        {
            if (!CanCommand) return;
            ReleaseUIFocus();
            CancelPlanning();
            Selected.Clear(); foreach (var u in Sim.Units.Where(u => u.Team == 0 && u.Alive)) Selected.Add(u.Id);
            Notify("All " + Selected.Count + " units selected");
        }
        public void SelectKind(UnitKind kind)
        {
            if (!CanCommand) return;
            ReleaseUIFocus();
            CancelPlanning();
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
                if (kb.escapeKey.wasPressedThisFrame) { if (AttackMoveArmed || MoveArmed) { CancelPlanning(); Notify("Pending order cancelled"); } else if (HelpOpen) ToggleHelp(); else TogglePause(); }
                if (InMenu) return; // EventSystem Submit follows the focused menu button.
                if (kb.spaceKey.wasPressedThisFrame && !HelpOpen && !HasFocusedUI) TogglePause();
                if (Paused || HelpOpen || Sim.Finished) return;
                if (kb.zKey.wasPressedThisFrame) Recruit(UnitKind.Scout);
                if (kb.xKey.wasPressedThisFrame) Recruit(UnitKind.Rifle);
                if (kb.cKey.wasPressedThisFrame) Recruit(UnitKind.Tank);
                if (kb.vKey.wasPressedThisFrame) Recruit(UnitKind.Gunner);
                if (kb.bKey.wasPressedThisFrame) Recruit(UnitKind.Artillery);
                bool assigning = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
                bool adding = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                if (!assigning)
                {
                    if (kb.aKey.wasPressedThisFrame) ToggleAttackMove();
                    if (kb.sKey.wasPressedThisFrame) StopSelected();
                    if (kb.hKey.wasPressedThisFrame) HoldSelected();
                    if (kb.rKey.wasPressedThisFrame) ToggleBridge();
                }
                for (int slot = 0; slot < groups.Length; slot++)
                {
                    var key = (Key)((int)Key.Digit1 + slot);
                    if (kb[key].wasPressedThisFrame) ReadGroupKey(slot, assigning, adding);
                }
                if ((kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) && kb.aKey.wasPressedThisFrame) SelectAll();
                if (kb.homeKey.wasPressedThisFrame) FocusBase();
                if (kb.fKey.wasPressedThisFrame) FocusAlert();
                if (kb.f1Key.wasPressedThisFrame) ToggleHelp();
                float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
                Vector3 right = Quaternion.Euler(0, wantedYaw, 0) * Vector3.right;
                Vector3 forward = Quaternion.Euler(0, wantedYaw, 0) * Vector3.forward;
                float x = (kb.rightArrowKey.isPressed ? 1 : 0) - (kb.leftArrowKey.isPressed ? 1 : 0);
                float z = (kb.upArrowKey.isPressed ? 1 : 0) - (kb.downArrowKey.isPressed ? 1 : 0);
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
            if (mouse.rightButton.wasPressedThisFrame && !HUD.PointerOverUI && Selected.Count > 0)
            {
                if (TryGround(pointer, out var location))
                {
                    int enemy = NearestAt(pointer, false, 24);
                    bool append = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
                    IssueOrder(location, enemy, AttackMoveArmed, append);
                }
                else Notify("Choose a destination on the table");
            }
        }

        /// <summary>Mouse-first context: friendly selects; ground moves; an enemy is an attack order.</summary>
        public bool TryPlanningTarget(Vector2 screen, out Vector2 destination, out int enemyId)
        {
            destination = default; enemyId = -1;
            if (!CanCommand || Selected.Count == 0 || !TryGround(screen, out destination)) return false;
            if (TutorialActive && (TutorialStep == 1 || TutorialStep == 2 || TutorialStep == 5) &&
                Vector2.Distance(destination, TutorialTargetPosition) <= TutorialTargetRadius)
            { destination = TutorialTargetPosition; return true; }
            int friendly = NearestAt(screen, true, 25), enemy = NearestAt(screen, false, 25);
            if (!MoveArmed && !AttackMoveArmed && friendly >= 0)
            {
                var friendlyUnit = Sim.Units.First(u => u.Id == friendly);
                Vector2 friendlyScreen = ViewCamera.WorldToScreenPoint(new Vector3(friendlyUnit.Position.x, .6f, friendlyUnit.Position.y));
                if (enemy < 0) return false;
                var target = Sim.Units.First(u => u.Id == enemy);
                Vector2 enemyScreen = ViewCamera.WorldToScreenPoint(new Vector3(target.Position.x, .6f, target.Position.y));
                if ((friendlyScreen - screen).sqrMagnitude <= (enemyScreen - screen).sqrMagnitude) return false;
            }
            if (enemy >= 0)
            {
                enemyId = enemy;
                destination = Sim.Units.First(u => u.Id == enemy).Position;
            }
            return true;
        }

        public void ClickWorld(Vector2 screen, bool additive = false)
        {
            if (!CanCommand) return;
            ReleaseUIFocus();
            if ((AttackMoveArmed || MoveArmed) && TryGround(screen, out var attackDestination))
            { IssueOrder(attackDestination, NearestAt(screen, false, 25), AttackMoveArmed, additive); return; }
            // In a lesson the marked destination is the explicit action target, even if
            // another friendly already stands there. Do not turn that click into a selection.
            if (TutorialActive && Selected.Count > 0 && (TutorialStep == 1 || TutorialStep == 2 || TutorialStep == 5) &&
                TryGround(screen, out var lessonGround) && Vector2.Distance(lessonGround, TutorialTargetPosition) <= TutorialTargetRadius)
            { IssueOrder(TutorialTargetPosition, -1, false, additive); return; }
            if (TutorialActive && TutorialStep == 4 && Selected.Count > 0 && tutorialEnemy != null && tutorialEnemy.Alive)
            {
                Vector3 targetScreen = ViewCamera.WorldToScreenPoint(new Vector3(tutorialEnemy.Position.x, .6f, tutorialEnemy.Position.y));
                if (targetScreen.z > 0 && ((Vector2)targetScreen - screen).sqrMagnitude <= 25 * 25)
                { IssueOrder(tutorialEnemy.Position, tutorialEnemy.Id, false, additive); return; }
            }
            int friendly = NearestAt(screen, true, 25), enemy = NearestAt(screen, false, 25);
            float DistanceTo(int id)
            {
                var unit = Sim.Units.First(u => u.Id == id);
                return ((Vector2)ViewCamera.WorldToScreenPoint(new Vector3(unit.Position.x, .6f, unit.Position.y)) - screen).sqrMagnitude;
            }
            if (friendly >= 0 && (enemy < 0 || DistanceTo(friendly) <= DistanceTo(enemy)))
            {
                bool doubleClick = friendly == lastClickedUnit && Time.unscaledTime - lastUnitClickTime <= .35f;
                if (doubleClick)
                {
                    UnitKind kind = Sim.Units.First(u => u.Id == friendly).Kind;
                    if (!additive) Selected.Clear();
                    foreach (var unit in Sim.Units.Where(u => u.Team == 0 && u.Alive && u.Kind == kind))
                    {
                        Vector3 visible = ViewCamera.WorldToViewportPoint(new Vector3(unit.Position.x, .6f, unit.Position.y));
                        if (visible.z > 0 && visible.x >= 0 && visible.x <= 1 && visible.y >= 0 && visible.y <= 1) Selected.Add(unit.Id);
                    }
                    Notify("Visible " + BattleSimulation.UnitName(kind, true).ToLowerInvariant() + " selected");
                    lastClickedUnit = -1;
                }
                else { SelectAt(screen, screen, false, additive); lastClickedUnit = friendly; lastUnitClickTime = Time.unscaledTime; }
                return;
            }
            if (Selected.Count == 0) { Notify("Select blue troops or choose a force below first"); return; }
            if (enemy >= 0)
            {
                var target = Sim.Units.First(u => u.Id == enemy);
                IssueOrder(target.Position, enemy, false, additive); return;
            }
            if (TryGround(screen, out var location)) IssueOrder(location, -1, false, additive);
            else Notify("Choose a destination on the table");
        }

        public void SelectAt(Vector2 start, Vector2 end, bool box, bool additive)
        {
            if (!CanCommand) return;
            ReleaseUIFocus();
            CancelPlanning();
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
        int NearestUnitAt(Vector2 position, float radius)
        {
            int friendly = NearestAt(position, true, radius), enemy = NearestAt(position, false, radius);
            if (friendly < 0) return enemy;
            if (enemy < 0) return friendly;
            Vector2 ScreenPosition(int id)
            {
                var unit = Sim.Units.First(u => u.Id == id);
                return ViewCamera.WorldToScreenPoint(new Vector3(unit.Position.x, .6f, unit.Position.y));
            }
            return (ScreenPosition(friendly) - position).sqrMagnitude <= (ScreenPosition(enemy) - position).sqrMagnitude ? friendly : enemy;
        }
        public bool TryGround(Vector2 screen, out Vector2 position)
        {
            if (screen.x < 0 || screen.y < 0 || screen.x > Screen.width || screen.y > Screen.height) { position = default; return false; }
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
            if (!focused && !TablefrontBenchmark.IsRunning && !InMenu && Sim != null && !Sim.Finished) { Paused = true; CancelPointerGesture(); }
        }
        void OnDestroy() { if (commandTone) Destroy(commandTone); }
    }
}
