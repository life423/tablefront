using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Tablefront
{
    /// <summary>Runtime-built tabletop command interface. The game owns all state.</summary>
    public sealed class TablefrontHUD : MonoBehaviour
    {
        TablefrontGame game;
        Camera view;
        Canvas canvas;
        RectTransform canvasRect;
        RectTransform title, hud, modal, help, selection;
        TextMeshProUGUI credits, selectionText, notice, clock, commanderText, modalTitle, modalBody;
        TextMeshProUGUI[] scoreText = new TextMeshProUGUI[3];
        TextMeshProUGUI[] scoreStatus = new TextMeshProUGUI[3];
        RectTransform[] scoreFill = new RectTransform[3];
        UnityEngine.UI.Button[] recruit = new UnityEngine.UI.Button[3];
        TextMeshProUGUI[] recruitText = new TextMeshProUGUI[3];
        UnityEngine.UI.Button commanderButton, resumeButton, restartButton, playButton, helpClose;
        TextMeshProUGUI speedText, recruitStatus;
        UnityEngine.UI.Slider volume;
        UnityEngine.UI.Toggle reduced;
        RectTransform commandSection, spectatorSection;
        TextMeshProUGUI spectatorIntents;
        bool wasMenu, wasModal, wasHelp;
        bool built;
        CanvasGroup titleGroup, modalGroup, helpGroup;
        TMP_FontAsset font;

        public bool PointerOverUI => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        public void Build(TablefrontGame owner, Camera camera)
        {
            if (built) return;
            built = true;
            game = owner;
            view = camera;
            font = TMP_Settings.defaultFontAsset;
            if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            var root = new GameObject("Tablefront Interface", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 100;
            canvasRect = root.GetComponent<RectTransform>();
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            if (EventSystem.current == null)
            {
                var system = new GameObject("Tablefront Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                system.transform.SetParent(transform, false);
                system.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            BuildTitle();
            BuildHud();
            BuildModal();
            BuildHelp();
            selection = Box("Selection rectangle", canvasRect, WithAlpha(TablePalette.Team(0), .12f), false);
            selection.pivot = Vector2.zero;
            var outline = selection.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = WithAlpha(TablePalette.Team(0), .95f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            selection.gameObject.SetActive(false);
            wasMenu = wasModal = wasHelp = false;
            Refresh();
        }

        void BuildTitle()
        {
            title = Container("Title", canvasRect); Stretch(title);
            titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            var card = Box("Title card", title, WithAlpha(TablePalette.Ink, .96f), true);
            Anchor(card, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(48, 0), new Vector2(532, 696));
            var accent = Box("Gold rule", card, TablePalette.Gold); Top(accent, 36, 36, 74, 4);
            Label(card, "NEURAL COMMANDERS  /  PPO", 15, TablePalette.Gold, new Vector2(36, -65), new Vector2(460, 32), FontStyles.Bold, 1.6f);
            Label(card, "TABLE\nFRONT", 76, TablePalette.Text, new Vector2(30, -109), new Vector2(470, 178), FontStyles.Bold, -2f);
            Label(card, "THE WAR ROOM IS YOURS", 16, TablePalette.Muted, new Vector2(36, -293), new Vector2(460, 30), FontStyles.Bold, 1.7f);
            var intro = Label(card, "Command the blue army. Capture the beacons, grow your force, and outthink two neural commanders trained through self-play.", 22, TablePalette.Text, new Vector2(36, -345), new Vector2(454, 112));
            intro.textWrappingMode = TextWrappingModes.Normal;
            playButton = Button(card, "Play vs AI", () => game.StartBattle(false), true);
            Top(playButton.GetComponent<RectTransform>(), 36, 478, 460, 60);
            var watch = Button(card, "Watch AI battle", () => game.StartBattle(true));
            Top(watch.GetComponent<RectTransform>(), 36, 550, 460, 54);
            var helpButton = Button(card, "How to play", game.ToggleHelp);
            Top(helpButton.GetComponent<RectTransform>(), 36, 620, 280, 44);
            var quit = Button(card, "Exit", game.ExitGame);
            Top(quit.GetComponent<RectTransform>(), 328, 620, 168, 44);
            var policyTag = NeuralCommander.Available ? "PPO SELF-PLAY  /  " + NeuralCommander.TrainingEpisodes.ToString("N0") + " TRAINING MATCHES  /  OFFLINE" : "NEURAL POLICY UNAVAILABLE";
            var tag = Label(title, policyTag, 14, TablePalette.Text, Vector2.zero, new Vector2(850, 28), FontStyles.Bold, 1.2f);
            Anchor(tag.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 28), new Vector2(850, 28));
            tag.alignment = TextAlignmentOptions.Right;
        }

        void BuildHud()
        {
            hud = Container("Battle HUD", canvasRect); Stretch(hud);
            var top = Box("Command header", hud, WithAlpha(TablePalette.Ink, .96f), true);
            Anchor(top, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, -20), new Vector2(-40, 100));
            var economy = Container("Economy", top); Fraction(economy, 0, .21f, 14, 10);
            Label(economy, "TABLEFRONT", 15, TablePalette.Gold, new Vector2(4, -2), new Vector2(280, 24), FontStyles.Bold, 1.7f);
            credits = Label(economy, "", 24, TablePalette.Text, new Vector2(4, -31), new Vector2(310, 34), FontStyles.Bold);
            clock = Label(economy, "", 14, TablePalette.Muted, new Vector2(4, -66), new Vector2(320, 22));
            for (int i = 0; i < 3; i++)
            {
                var team = Container("Faction " + i, top); Fraction(team, .225f + i * .155f, .37f + i * .155f, 8, 10);
                scoreText[i] = Label(team, "", 23, TablePalette.Team(i), new Vector2(0, -2), new Vector2(240, 30), FontStyles.Bold);
                scoreStatus[i] = Label(team, "", 14, TablePalette.Text, new Vector2(0, -39), new Vector2(240, 23));
                var line = Box("Score track", team, WithAlpha(TablePalette.Text, .13f));
                Anchor(line, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 5), new Vector2(0, 4));
                scoreFill[i] = Box("Score progress", line, TablePalette.Team(i));
                Anchor(scoreFill[i], Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero, Vector2.zero);
            }
            var utilities = Container("Battle tools", top); Fraction(utilities, .71f, 1f, 14, 12);
            var row = Container("Tools row", utilities); Stretch(row);
            var layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            var speed = Button(row, "1×", game.CycleSpeed); LayoutItem(speed.gameObject, 54, 52);
            speedText = speed.GetComponentInChildren<TextMeshProUGUI>();
            var pause = Button(row, "Pause", game.TogglePause); LayoutItem(pause.gameObject, 94, 52);
            var tips = Button(row, "Help", game.ToggleHelp); LayoutItem(tips.gameObject, 82, 52);

            notice = Label(hud, "", 21, TablePalette.Text, Vector2.zero, new Vector2(900, 42), FontStyles.Bold);
            Anchor(notice.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -139), new Vector2(900, 42));
            notice.alignment = TextAlignmentOptions.Center;
            var noticeShadow = notice.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            noticeShadow.effectColor = TablePalette.Ink;
            noticeShadow.effectDistance = new Vector2(1, -2);

            var bottom = Box("Command deck", hud, WithAlpha(TablePalette.Ink, .97f), true);
            Anchor(bottom, Vector2.zero, new Vector2(1, 0), new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(-40, 170));
            commandSection = Container("Player commands", bottom); Stretch(commandSection);
            var selected = Container("Selection", commandSection); Fraction(selected, 0, .285f, 22, 18);
            Label(selected, "SELECTED FORCE", 14, TablePalette.Gold, new Vector2(0, 0), new Vector2(350, 24), FontStyles.Bold, 1.2f);
            selectionText = Label(selected, "", 21, TablePalette.Text, new Vector2(0, -31), new Vector2(370, 52));
            var selectButton = Button(selected, "Select all  ·  Ctrl+A", game.SelectAll);
            Anchor(selectButton.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1, 0), Vector2.zero, Vector2.zero, new Vector2(0, 40));
            var build = Container("Reinforcements", commandSection); Fraction(build, .30f, .755f, 10, 18);
            recruitStatus = Label(build, "REINFORCEMENTS  /  AT YOUR HQ", 14, TablePalette.Gold, new Vector2(0, 0), new Vector2(580, 24), FontStyles.Bold, 1.2f);
            var recruits = Container("Recruitment row", build);
            Anchor(recruits, Vector2.zero, new Vector2(1, 0), Vector2.zero, Vector2.zero, new Vector2(0, 94));
            var recruitsLayout = recruits.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            recruitsLayout.spacing = 10; recruitsLayout.childControlWidth = true; recruitsLayout.childControlHeight = true;
            recruitsLayout.childForceExpandWidth = true; recruitsLayout.childForceExpandHeight = true;
            for (int i = 0; i < 3; i++)
            {
                UnitKind kind = (UnitKind)i;
                recruit[i] = Button(recruits, "", () => game.Recruit(kind));
                recruitText[i] = recruit[i].GetComponentInChildren<TextMeshProUGUI>();
                recruitText[i].fontSize = 20;
                LayoutItem(recruit[i].gameObject, 135, 94);
            }
            var actions = Container("Commander", commandSection); Fraction(actions, .775f, 1f, 18, 18);
            commanderText = Label(actions, "", 14, TablePalette.Muted, new Vector2(0, 0), new Vector2(350, 26), FontStyles.Bold);
            commanderButton = Button(actions, "AI command  ·  Tab", game.ToggleCommander);
            Anchor(commanderButton.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 51), new Vector2(0, 43));
            var home = Button(actions, "Focus HQ  ·  Home", game.FocusBase);
            Anchor(home.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1, 0), Vector2.zero, Vector2.zero, new Vector2(0, 43));

            spectatorSection = Container("Spectator commands", bottom); Stretch(spectatorSection);
            Label(spectatorSection, "NEURAL WAR ROOM", 17, TablePalette.Gold, new Vector2(26, -20), new Vector2(300, 28), FontStyles.Bold, 1.2f);
            Label(spectatorSection, "Three commanders. Equal rules.", 20, TablePalette.Text, new Vector2(26, -64), new Vector2(350, 38));
            Label(spectatorSection, "WASD pan  ·  Wheel zoom  ·  Q/E orbit", 15, TablePalette.Muted, new Vector2(26, -121), new Vector2(400, 28));
            spectatorIntents = Label(spectatorSection, "", 20, TablePalette.Text, Vector2.zero, new Vector2(1000, 122));
            Anchor(spectatorIntents.rectTransform, new Vector2(.31f, 0), new Vector2(1, 1), new Vector2(0, .5f), Vector2.zero, Vector2.zero);
            spectatorIntents.rectTransform.offsetMin = new Vector2(16, 16); spectatorIntents.rectTransform.offsetMax = new Vector2(-26, -16);
        }

        void BuildModal()
        {
            modal = Box("Pause and results scrim", canvasRect, WithAlpha(TablePalette.Background, .74f), true); Stretch(modal);
            modalGroup = modal.gameObject.AddComponent<CanvasGroup>();
            var card = Box("Pause card", modal, TablePalette.Ink, true);
            Anchor(card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(580, 658));
            Label(card, "TABLEFRONT / COMMAND ROOM", 15, TablePalette.Gold, new Vector2(36, -30), new Vector2(510, 28), FontStyles.Bold, 1.4f);
            modalTitle = Label(card, "Battle paused", 44, TablePalette.Text, new Vector2(36, -85), new Vector2(510, 68), FontStyles.Bold);
            modalBody = Label(card, "", 21, TablePalette.Muted, new Vector2(36, -171), new Vector2(508, 100));
            resumeButton = Button(card, "Resume battle", game.TogglePause, true); Top(resumeButton.GetComponent<RectTransform>(), 36, 285, 508, 56);
            restartButton = Button(card, "New battle", () => game.StartBattle(game.Spectator)); Top(restartButton.GetComponent<RectTransform>(), 36, 355, 508, 52);
            var helpButton = Button(card, "Controls and settings", game.ToggleHelp); Top(helpButton.GetComponent<RectTransform>(), 36, 421, 508, 52);
            var menu = Button(card, "Return to title", game.ReturnToMenu); Top(menu.GetComponent<RectTransform>(), 36, 487, 508, 52);
            var exit = Button(card, "Exit game", game.ExitGame); Top(exit.GetComponent<RectTransform>(), 36, 575, 508, 46);
        }

        void BuildHelp()
        {
            help = Box("Help scrim", canvasRect, WithAlpha(TablePalette.Background, .85f), true); Stretch(help);
            helpGroup = help.gameObject.AddComponent<CanvasGroup>();
            var card = Box("Field manual", help, TablePalette.Ink, true);
            Anchor(card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1020, 716));
            Label(card, "FIELD MANUAL", 15, TablePalette.Gold, new Vector2(36, -28), new Vector2(800, 24), FontStyles.Bold, 2);
            Label(card, "Win the tabletop", 42, TablePalette.Text, new Vector2(36, -68), new Vector2(870, 64), FontStyles.Bold);
            Label(card, "Hold beacons to earn victory points and extra credits. Reach 350 points or eliminate both enemy headquarters.", 22, TablePalette.Text, new Vector2(36, -145), new Vector2(948, 76));
            var rule = Box("Manual rule", card, WithAlpha(TablePalette.Gold, .4f)); Top(rule, 36, 236, 948, 2);
            Label(card, "COMMAND", 15, TablePalette.Gold, new Vector2(36, -266), new Vector2(450, 28), FontStyles.Bold, 1.4f);
            Label(card, "Click / drag     Select blue units\nShift                  Add to selection\nRight-click       Move and attack nearby\nEnemy click      Right-click to focus fire\nCtrl+A               Select your whole army\n1 / 2 / 3             Recruit Scout / Rifle / Tank\nTab                    Let AI command your army", 20, TablePalette.Text, new Vector2(36, -311), new Vector2(480, 250));
            Label(card, "CAMERA & BATTLE", 15, TablePalette.Gold, new Vector2(554, -266), new Vector2(420, 28), FontStyles.Bold, 1.4f);
            Label(card, "WASD / arrows   Pan the table\nMouse wheel        Zoom\nQ / E                      Orbit\nMiddle drag           Pan\nHome                     Focus your HQ\nSpace / Escape      Pause\nF11                         Fullscreen", 20, TablePalette.Text, new Vector2(554, -311), new Vector2(430, 250));
            Label(card, "VOLUME", 13, TablePalette.Gold, new Vector2(36, -593), new Vector2(110, 24), FontStyles.Bold, 1);
            BuildVolume(card);
            reduced = BuildToggle(card, "Reduced motion", new Vector2(440, -590));
            reduced.onValueChanged.AddListener(value => { if (game != null) game.ReducedMotion = value; });
            helpClose = Button(card, "Back", game.ToggleHelp, true); Top(helpClose.GetComponent<RectTransform>(), 776, 630, 208, 50);
            Label(card, "Scouts capture quickly. Rifles hold ground. Tanks break concentrated forces.", 16, TablePalette.Muted, new Vector2(36, -661), new Vector2(715, 30));
        }

        void BuildVolume(RectTransform parent)
        {
            var sliderRoot = Container("Master volume", parent);
            Top(sliderRoot, 150, 593, 240, 34);
            volume = sliderRoot.gameObject.AddComponent<UnityEngine.UI.Slider>();
            volume.minValue = 0; volume.maxValue = 1;
            var background = Box("Volume track", sliderRoot, WithAlpha(TablePalette.Text, .18f), true);
            Anchor(background, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 5));
            var fillArea = Container("Volume fill area", sliderRoot); Stretch(fillArea); fillArea.offsetMin = new Vector2(8, 0); fillArea.offsetMax = new Vector2(-8, 0);
            var fillRect = Box("Volume fill", fillArea, TablePalette.Gold);
            Anchor(fillRect, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 5));
            var handleArea = Container("Volume handle area", sliderRoot);
            Anchor(handleArea, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(-20, 0));
            var handle = Box("Volume handle", handleArea, TablePalette.Text, true);
            Anchor(handle, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(18, 28));
            volume.fillRect = fillRect; volume.handleRect = handle;
            volume.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
            volume.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            volume.onValueChanged.AddListener(value => { if (game != null) game.MasterVolume = value; });
            var colors = volume.colors; colors.normalColor = TablePalette.Text; colors.highlightedColor = TablePalette.Gold; colors.selectedColor = TablePalette.Gold; volume.colors = colors;
        }

        UnityEngine.UI.Toggle BuildToggle(RectTransform parent, string text, Vector2 position)
        {
            var root = Container(text, parent); Top(root, position.x, -position.y, 290, 44);
            var bg = Box("Toggle box", root, WithAlpha(TablePalette.Text, .12f), true);
            Anchor(bg, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2(34, 34));
            var check = Box("Enabled", bg, TablePalette.Gold);
            Stretch(check); check.offsetMin = new Vector2(8, 8); check.offsetMax = new Vector2(-8, -8);
            var label = Label(root, text, 19, TablePalette.Text, new Vector2(48, -8), new Vector2(230, 34));
            var toggle = root.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            toggle.targetGraphic = bg.GetComponent<UnityEngine.UI.Image>(); toggle.graphic = check.GetComponent<UnityEngine.UI.Image>();
            return toggle;
        }

        public void Refresh()
        {
            if (!built || game == null) return;
            var sim = game.Sim;
            bool menuVisible = game.InMenu;
            bool helpVisible = game.HelpOpen;
            bool modalVisible = !menuVisible && (game.Paused || (sim != null && sim.Finished));
            if (menuVisible && !wasMenu) titleGroup.alpha = game.ReducedMotion ? 1 : 0;
            if (modalVisible && !wasModal) modalGroup.alpha = game.ReducedMotion ? 1 : 0;
            if (helpVisible && !wasHelp) helpGroup.alpha = game.ReducedMotion ? 1 : 0;
            title.gameObject.SetActive(menuVisible);
            hud.gameObject.SetActive(!menuVisible);
            modal.gameObject.SetActive(modalVisible);
            help.gameObject.SetActive(helpVisible);
            bool ended = sim != null && sim.Finished;
            resumeButton.gameObject.SetActive(!ended);
            // Restore focus only on real panel transitions, allowing normal keyboard navigation.
            if (helpVisible && !wasHelp)
            {
                volume.SetValueWithoutNotify(game.MasterVolume);
                reduced.SetIsOnWithoutNotify(game.ReducedMotion);
                Focus(helpClose);
            }
            else if (!helpVisible && wasHelp)
                Focus(menuVisible ? playButton : modalVisible ? (ended ? restartButton : resumeButton) : null);
            else if (menuVisible && !wasMenu && !helpVisible) Focus(playButton);
            else if (modalVisible && !wasModal && !helpVisible) Focus(ended ? restartButton : resumeButton);
            if ((!menuVisible && wasMenu) || (!modalVisible && wasModal))
                if (!helpVisible) Focus(null);
            wasMenu = menuVisible; wasModal = modalVisible; wasHelp = helpVisible;
            if (sim == null) return;
            var army = sim.Armies[0];
            bool observer = game.Spectator;
            commandSection.gameObject.SetActive(!observer);
            spectatorSection.gameObject.SetActive(observer);
            Set(credits, observer ? "Neural simulation" : $"{Mathf.FloorToInt(army.Credits)} credits  ·  {sim.UnitCount(0)}/{BattleSimulation.UnitCap}");
            int mins = Mathf.FloorToInt(sim.Elapsed / 60);
            int secs = Mathf.FloorToInt(sim.Elapsed) % 60;
            Set(clock, $"{mins:00}:{secs:00}   /   FIRST TO {(int)BattleSimulation.VictoryScore} POINTS");
            for (int i = 0; i < 3; i++)
            {
                Army team = sim.Armies[i];
                Set(scoreText[i], $"{team.Name}  {Mathf.FloorToInt(team.Score)}");
                Set(scoreStatus[i], team.Alive ? $"{sim.OwnedPoints(i)} beacons  ·  HQ {Mathf.CeilToInt(team.BaseHealth)}" : "ELIMINATED");
                scoreFill[i].anchorMax = new Vector2(Mathf.Clamp01(team.Score / BattleSimulation.VictoryScore), 1);
            }
            Set(selectionText, game.SelectionSummary);
            Set(notice, game.Notice ?? "");
            Set(speedText, game.Speed + "×");
            Set(commanderText, army.IsAI ? "AI COMMANDING YOUR ARMY" : "YOU ARE IN COMMAND");
            commanderButton.GetComponentInChildren<TextMeshProUGUI>().text = army.IsAI ? "Take command  ·  Tab" : "AI command  ·  Tab";
            bool canRecruit = !game.Paused && !helpVisible && !sim.Finished && army.Alive && sim.UnitCount(0) < BattleSimulation.UnitCap;
            Set(recruitStatus, !army.Alive ? "REINFORCEMENTS  /  HQ LOST" : sim.UnitCount(0) >= BattleSimulation.UnitCap ? "REINFORCEMENTS  /  ARMY AT CAPACITY" : army.RecruitCooldown > 0 ? $"REINFORCEMENTS  /  READY IN {Mathf.CeilToInt(army.RecruitCooldown)}s" : "REINFORCEMENTS  /  AT YOUR HQ");
            for (int i = 0; i < 3; i++)
            {
                int cost = BattleSimulation.Cost((UnitKind)i);
                recruit[i].interactable = canRecruit && army.Credits >= cost && army.RecruitCooldown <= 0;
                string name = ((UnitKind)i).ToString();
                Set(recruitText[i], $"{i + 1}  {name}\n<size=15>{cost} credits</size>");
            }
            Set(spectatorIntents, $"<color=#{ColorUtility.ToHtmlStringRGB(TablePalette.Team(0))}>{sim.Armies[0].Name}</color>   {sim.Armies[0].Intent}\n<color=#{ColorUtility.ToHtmlStringRGB(TablePalette.Team(1))}>{sim.Armies[1].Name}</color>   {sim.Armies[1].Intent}\n<color=#{ColorUtility.ToHtmlStringRGB(TablePalette.Team(2))}>{sim.Armies[2].Name}</color>   {sim.Armies[2].Intent}");
            if (modalVisible)
            {
                bool finished = sim.Finished;
                Set(modalTitle, finished ? (observer ? "Battle concluded" : sim.Winner == 0 ? "Victory secured" : "Army defeated") : "Battle paused");
                Set(modalBody, finished ? $"{(sim.Winner >= 0 ? sim.Armies[sim.Winner].Name : "No army")} wins the tabletop.\nBattle time {mins:00}:{secs:00}. Start again with a fresh force." : "Your orders can wait.\nThe battlefield is frozen until you resume.");
                resumeButton.gameObject.SetActive(!finished);
            }
        }

        void Update()
        {
            if (!built) return;
            float step = game.ReducedMotion ? 1 : Time.unscaledDeltaTime * 8f;
            titleGroup.alpha = Mathf.MoveTowards(titleGroup.alpha, 1, step);
            modalGroup.alpha = Mathf.MoveTowards(modalGroup.alpha, 1, step);
            helpGroup.alpha = Mathf.MoveTowards(helpGroup.alpha, 1, step);
        }

        public void DrawSelection(Vector2 start, Vector2 end, bool visible)
        {
            if (!built) return;
            selection.gameObject.SetActive(visible);
            if (!visible) return;
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : view;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, start, eventCamera, out Vector2 first);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, end, eventCamera, out Vector2 last);
            selection.anchorMin = selection.anchorMax = new Vector2(.5f, .5f);
            selection.anchoredPosition = Vector2.Min(first, last);
            selection.sizeDelta = new Vector2(Mathf.Abs(first.x - last.x), Mathf.Abs(first.y - last.y));
        }

        void Focus(UnityEngine.UI.Selectable selectable)
        {
            if (EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(selectable != null && selectable.gameObject.activeInHierarchy ? selectable.gameObject : null);
        }

        static void Set(TextMeshProUGUI label, string value) { if (label != null && label.text != value) label.text = value; }
        static Color WithAlpha(Color value, float alpha) { value.a = alpha; return value; }

        RectTransform Container(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go.GetComponent<RectTransform>();
        }
        RectTransform Box(string name, Transform parent, Color color, bool blocks = false)
        {
            var rect = Container(name, parent);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = blocks;
            return rect;
        }
        TextMeshProUGUI Label(Transform parent, string text, float size, Color color, Vector2 position, Vector2 dimensions, FontStyles style = FontStyles.Normal, float spacing = 0)
        {
            var rect = Container(text.Length > 0 ? text.Split('\n')[0] : "Dynamic label", parent);
            Top(rect, position.x, -position.y, dimensions.x, dimensions.y);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = text; label.fontSize = size; label.fontStyle = style; label.color = color; label.characterSpacing = spacing;
            label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Truncate; label.alignment = TextAlignmentOptions.TopLeft;
            label.margin = Vector4.zero;
            return label;
        }
        UnityEngine.UI.Button Button(Transform parent, string text, UnityEngine.Events.UnityAction action, bool primary = false)
        {
            var rect = Box(text, parent, primary ? TablePalette.Gold : WithAlpha(TablePalette.Text, .08f), true);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            // The white sprite mask deliberately preserves the authored palette ColorBlock tints.
            rect.GetComponent<UnityEngine.UI.Image>().color = new Color(1, 1, 1, 1);
            var colors = button.colors;
            colors.normalColor = primary ? TablePalette.Gold : WithAlpha(TablePalette.Text, .10f);
            colors.highlightedColor = primary ? Color.Lerp(TablePalette.Gold, TablePalette.Text, .25f) : WithAlpha(TablePalette.Text, .20f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = primary ? Color.Lerp(TablePalette.Gold, TablePalette.Ink, .20f) : WithAlpha(TablePalette.Gold, .32f);
            colors.disabledColor = WithAlpha(TablePalette.Text, .04f);
            colors.colorMultiplier = 1; colors.fadeDuration = .12f; button.colors = colors;
            button.onClick.AddListener(action);
            var label = Label(rect, text, 21, primary ? TablePalette.Ink : TablePalette.Text, Vector2.zero, Vector2.zero, FontStyles.Bold);
            Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(8, 2); label.rectTransform.offsetMax = new Vector2(-8, -2); label.alignment = TextAlignmentOptions.Center;
            var outline = rect.gameObject.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor = TablePalette.Gold; outline.effectDistance = new Vector2(2, -2); outline.enabled = false;
            var feedback = rect.gameObject.AddComponent<TablefrontButtonFeedback>(); feedback.Configure(outline, label, primary);
            return button;
        }

        static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
        { rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; }
        static void Stretch(RectTransform rect)
        { Anchor(rect, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero); }
        static void Top(RectTransform rect, float left, float top, float width, float height)
        { Anchor(rect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(left, -top), new Vector2(width, height)); }
        static void Fraction(RectTransform rect, float minX, float maxX, float horizontalInset, float verticalInset)
        { Stretch(rect); rect.anchorMin = new Vector2(minX, 0); rect.anchorMax = new Vector2(maxX, 1); rect.offsetMin = new Vector2(horizontalInset, verticalInset); rect.offsetMax = new Vector2(-horizontalInset, -verticalInset); }
        static void LayoutItem(GameObject go, float width, float height)
        { var item = go.AddComponent<UnityEngine.UI.LayoutElement>(); item.preferredWidth = width; item.preferredHeight = height; item.flexibleWidth = 1; }
    }

    public sealed class TablefrontButtonFeedback : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        UnityEngine.UI.Outline outline;
        TextMeshProUGUI text;
        UnityEngine.UI.Button button;
        bool primary, selected, hovered;
        public void Configure(UnityEngine.UI.Outline focusOutline, TextMeshProUGUI label, bool isPrimary)
        { outline = focusOutline; text = label; primary = isPrimary; button = GetComponent<UnityEngine.UI.Button>(); }
        public void OnSelect(BaseEventData data) { selected = true; Apply(); }
        public void OnDeselect(BaseEventData data) { selected = false; Apply(); }
        public void OnPointerEnter(PointerEventData data) { hovered = true; Apply(); }
        public void OnPointerExit(PointerEventData data) { hovered = false; Apply(); }
        void OnEnable() { selected = hovered = false; Apply(); }
        void Update() { Apply(); }
        void Apply()
        {
            if (button == null || text == null || outline == null) return;
            outline.enabled = button.interactable && (selected || hovered);
            Color color = primary ? TablePalette.Ink : TablePalette.Text;
            if (!button.interactable) color.a = .40f;
            text.color = color;
        }
    }
}
