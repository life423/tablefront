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
                    Sim.Step(.05f); World.PlayEvents(Sim.Events); accumulator -= .05f;
                    if (Sim.Finished) { accumulator = 0; Notify(Sim.Armies[Sim.Winner].Name + " wins the battle"); break; }
                }
            }
            Selected.RemoveWhere(id => !Sim.Units.Any(u => u.Id == id && u.Alive));
            if (!InMenu && !Spectator && !Sim.Armies[0].Alive && !Sim.Finished)
            {
                Spectator = true; Selected.Clear(); Notify("Your HQ fell. Watching the remaining armies; Escape opens the menu.");
            }
            World.Render(Sim, Selected);
            HUD.Refresh();
        }

        void LateUpdate() { UpdateCamera(Time.unscaledDeltaTime); }

        public void StartBattle(bool spectator)
        {
            World.ClearBattle();
            Sim = new BattleSimulation(spectator, 42 + matchIndex++);
            Spectator = spectator; InMenu = false; Paused = false; HelpOpen = false;
            Speed = 1; accumulator = 0; Selected.Clear();
            wantedFocus = spectator ? new Vector3(0, 0, 0) : new Vector3(0, 0, -10);
            wantedDistance = spectator ? 68 : 53; wantedYaw = 0;
            if (!spectator) foreach (var u in Sim.Units.Where(u => u.Team == 0 && u.Alive)) Selected.Add(u.Id);
            Notify(spectator ? "Three commanders. One battlefield." : "Your army is selected. Right-click a command post to advance.");
            World.Render(Sim, Selected); HUD.Refresh();
        }
        public void ReturnToMenu()
        {
            InMenu = true; HelpOpen = false; Paused = false; Selected.Clear(); accumulator = 0;
            World.ClearBattle(); Sim = new BattleSimulation(false);
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
            Notify(added ? kind + " deployed at your HQ" : Sim.UnitCount(0) >= BattleSimulation.UnitCap ? "Army at capacity" : Sim.Armies[0].RecruitCooldown > 0 ? "Reinforcements preparing" : "Need " + BattleSimulation.Cost(kind) + " supplies");
        }
        public void SelectAll()
        {
            if (Spectator || InMenu) return;
            Selected.Clear(); foreach (var u in Sim.Units.Where(u => u.Team == 0 && u.Alive)) Selected.Add(u.Id);
            Notify("All " + Selected.Count + " units selected");
        }
        public void FocusBase() { var p = Sim.Armies[0].BasePosition; wantedFocus = new Vector3(p.x, 0, p.y + 7); wantedDistance = 42; }
        public void ToggleCommander()
        {
            if (InMenu || Spectator || Sim.Finished) return;
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
                if ((kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) && kb.aKey.wasPressedThisFrame) SelectAll();
                if (kb.homeKey.wasPressedThisFrame) FocusBase();
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
                if (enemy >= 0) Sim.OrderAttack(Selected, enemy);
                else Sim.OrderMove(Selected, location);
                World.ShowOrder(location); Notify(enemy >= 0 ? "Focus fire" : "Advance to position");
            }
        }

        public void SelectAt(Vector2 start, Vector2 end, bool box, bool additive)
        {
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
