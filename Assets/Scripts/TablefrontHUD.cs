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
        TextMeshProUGUI speedText, recruitStatus, orderText, roleText;
        UnityEngine.UI.Button[] squadButtons = new UnityEngine.UI.Button[3];
        TextMeshProUGUI[] squadText = new TextMeshProUGUI[3];
        UnityEngine.UI.Button assignSquadButton, alertButton, tutorialDone, tutorialSkip;
        TextMeshProUGUI assignSquadText, alertText, tutorialProgress, tutorialTitle, tutorialInstruction;
        RectTransform tutorialCard;
        bool assigningSquad;
        readonly string[] squadNames = { "Alpha", "Bravo", "Charlie" };
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
            BuildTutorial();
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
            Anchor(card, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(48, 0), new Vector2(532, 740));
            var accent = Box("Gold rule", card, TablePalette.Gold); Top(accent, 36, 36, 74, 4);
            Label(card, "MINIATURE ARMIES / REAL-TIME TACTICS", 15, TablePalette.Gold, new Vector2(36, -65), new Vector2(460, 32), FontStyles.Bold, 1.1f);
            Label(card, "TABLE\nFRONT", 76, TablePalette.Text, new Vector2(30, -109), new Vector2(470, 178), FontStyles.Bold, -2f);
            Label(card, "THE WAR ROOM IS YOURS", 16, TablePalette.Muted, new Vector2(36, -293), new Vector2(460, 30), FontStyles.Bold, 1.7f);
            var intro = Label(card, "Command the blue army. Split your forces, seize three beacons, and turn the battle with one good flank.", 22, TablePalette.Text, new Vector2(36, -337), new Vector2(454, 90));
            intro.textWrappingMode = TextWrappingModes.Normal;
            playButton = Button(card, "Play vs AI", () => game.StartBattle(false), true);
            Top(playButton.GetComponent<RectTransform>(), 36, 448, 460, 58);
            var learn = Button(card, "Learn to command", game.StartTutorial);
            Top(learn.GetComponent<RectTransform>(), 36, 518, 460, 52);
            var watch = Button(card, "Watch AI battle", () => game.StartBattle(true));
            Top(watch.GetComponent<RectTransform>(), 36, 582, 460, 48);
            var helpButton = Button(card, "How to play", game.ToggleHelp);
            Top(helpButton.GetComponent<RectTransform>(), 36, 650, 280, 48);
            var quit = Button(card, "Exit", game.ExitGame);
            Top(quit.GetComponent<RectTransform>(), 328, 650, 168, 48);
            var policyTag = NeuralCommander.Available ? "THREE ARMIES  /  NEURAL OPPONENTS  /  OFFLINE" : "NEURAL POLICY UNAVAILABLE";
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
            Anchor(notice.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 256), new Vector2(900, 42));
            notice.alignment = TextAlignmentOptions.Center;
            var noticeShadow = notice.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            noticeShadow.effectColor = TablePalette.Ink;
            noticeShadow.effectDistance = new Vector2(1, -2);

            var bottom = Box("Command deck", hud, WithAlpha(TablePalette.Ink, .97f), true);
            Anchor(bottom, Vector2.zero, new Vector2(1, 0), new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(-40, 226));
            commandSection = Container("Player commands", bottom); Stretch(commandSection);
            var selected = Container("Selection", commandSection); Fraction(selected, 0, .38f, 18, 16);
            selectionText = Label(selected, "", 20, TablePalette.Text, Vector2.zero, new Vector2(0, 30), FontStyles.Bold);
            SpanTop(selectionText.rectTransform, 0, 30);
            orderText = Label(selected, "", 17, TablePalette.Gold, new Vector2(0, -32), new Vector2(0, 25));
            SpanTop(orderText.rectTransform, 32, 25);
            roleText = Label(selected, "", 16, TablePalette.Muted, new Vector2(0, -60), new Vector2(0, 40));
            SpanTop(roleText.rectTransform, 60, 40);
            var squadRow = Container("Squad selection", selected); SpanTop(squadRow, 101, 44);
            var squadLayout = squadRow.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            squadLayout.spacing = 6; squadLayout.childControlWidth = squadLayout.childControlHeight = true;
            squadLayout.childForceExpandWidth = squadLayout.childForceExpandHeight = true;
            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                squadButtons[i] = Button(squadRow, squadNames[i], () => UseSquad(slot));
                squadText[i] = squadButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                squadText[i].fontSize = 18;
                LayoutItem(squadButtons[i].gameObject, 140, 44);
            }
            var selectionActions = Container("Selection actions", selected); SpanTop(selectionActions, 152, 44);
            var actionLayout = selectionActions.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            actionLayout.spacing = 8; actionLayout.childControlWidth = actionLayout.childControlHeight = true;
            actionLayout.childForceExpandWidth = actionLayout.childForceExpandHeight = true;
            var selectButton = Button(selectionActions, "All · Ctrl+A", game.SelectAll);
            selectButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 17;
            LayoutItem(selectButton.gameObject, 140, 44);
            assignSquadButton = Button(selectionActions, "Assign selected…", () => { assigningSquad = !assigningSquad; Refresh(); });
            assignSquadText = assignSquadButton.GetComponentInChildren<TextMeshProUGUI>(); assignSquadText.fontSize = 17;
            LayoutItem(assignSquadButton.gameObject, 180, 44);
            var build = Container("Reinforcements", commandSection); Fraction(build, .395f, .775f, 10, 16);
            recruitStatus = Label(build, "REINFORCEMENTS  /  AT YOUR HQ", 14, TablePalette.Gold, new Vector2(0, 0), new Vector2(580, 24), FontStyles.Bold, 1.2f);
            var recruits = Container("Recruitment row", build);
            Anchor(recruits, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -40), new Vector2(0, 156));
            var recruitsLayout = recruits.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            recruitsLayout.spacing = 10; recruitsLayout.childControlWidth = true; recruitsLayout.childControlHeight = true;
            recruitsLayout.childForceExpandWidth = true; recruitsLayout.childForceExpandHeight = true;
            for (int i = 0; i < 3; i++)
            {
                UnitKind kind = (UnitKind)i;
                recruit[i] = Button(recruits, "", () => game.Recruit(kind));
                recruitText[i] = recruit[i].GetComponentInChildren<TextMeshProUGUI>();
                recruitText[i].fontSize = 21;
                LayoutItem(recruit[i].gameObject, 135, 156);
            }
            var actions = Container("Commander", commandSection); Fraction(actions, .795f, 1f, 14, 16);
            commanderText = Label(actions, "", 14, TablePalette.Muted, new Vector2(0, 0), new Vector2(350, 26), FontStyles.Bold);
            commanderButton = Button(actions, "AI command  ·  Tab", game.ToggleCommander);
            commanderButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 18;
            Anchor(commanderButton.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 60), new Vector2(0, 52));
            var home = Button(actions, "Focus HQ  ·  Home", game.FocusBase);
            home.GetComponentInChildren<TextMeshProUGUI>().fontSize = 18;
            Anchor(home.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1, 0), Vector2.zero, Vector2.zero, new Vector2(0, 52));

            alertButton = Button(hud, "", game.FocusAlert);
            alertButton.gameObject.name = "Contextual battlefield alert";
            var alertColors = alertButton.colors;
            alertColors.normalColor = TablePalette.Ink;
            alertColors.highlightedColor = Color.Lerp(TablePalette.Ink, TablePalette.Gold, .16f);
            alertColors.selectedColor = alertColors.highlightedColor;
            alertColors.pressedColor = Color.Lerp(TablePalette.Ink, TablePalette.Gold, .25f);
            alertButton.colors = alertColors;
            Anchor(alertButton.GetComponent<RectTransform>(), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -136), new Vector2(740, 52));
            alertText = alertButton.GetComponentInChildren<TextMeshProUGUI>(); alertText.fontSize = 20;

            spectatorSection = Container("Spectator commands", bottom); Stretch(spectatorSection);
            Label(spectatorSection, "NEURAL WAR ROOM", 17, TablePalette.Gold, new Vector2(26, -20), new Vector2(300, 28), FontStyles.Bold, 1.2f);
            Label(spectatorSection, "Three commanders. Equal rules.", 20, TablePalette.Text, new Vector2(26, -64), new Vector2(350, 38));
            Label(spectatorSection, "WASD pan  ·  Wheel zoom  ·  Q/E orbit", 15, TablePalette.Muted, new Vector2(26, -121), new Vector2(400, 28));
            spectatorIntents = Label(spectatorSection, "", 20, TablePalette.Text, Vector2.zero, new Vector2(1000, 122));
            Anchor(spectatorIntents.rectTransform, new Vector2(.31f, 0), new Vector2(1, 1), new Vector2(0, .5f), Vector2.zero, Vector2.zero);
            spectatorIntents.rectTransform.offsetMin = new Vector2(16, 16); spectatorIntents.rectTransform.offsetMax = new Vector2(-26, -16);
        }

        void UseSquad(int slot)
        {
            if (assigningSquad) { game.AssignGroup(slot); assigningSquad = false; }
            else game.RecallGroup(slot);
            Refresh();
        }

        void BuildTutorial()
        {
            tutorialCard = Box("Optional field training", hud, WithAlpha(TablePalette.Ink, .97f), true);
            Anchor(tutorialCard, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -202), new Vector2(452, 224));
            tutorialProgress = Label(tutorialCard, "FIELD TRAINING", 14, TablePalette.Gold, new Vector2(20, -17), new Vector2(412, 22), FontStyles.Bold, 1);
            tutorialTitle = Label(tutorialCard, "", 25, TablePalette.Text, new Vector2(20, -48), new Vector2(412, 37), FontStyles.Bold);
            tutorialInstruction = Label(tutorialCard, "", 19, TablePalette.Text, new Vector2(20, -92), new Vector2(412, 70));
            tutorialSkip = Button(tutorialCard, "Skip lesson", game.SkipTutorial);
            Top(tutorialSkip.GetComponent<RectTransform>(), 20, 174, 180, 44);
            tutorialSkip.GetComponentInChildren<TextMeshProUGUI>().fontSize = 18;
            tutorialDone = Button(tutorialCard, "Enter battle", game.CompleteTutorial, true);
            Top(tutorialDone.GetComponent<RectTransform>(), 20, 174, 412, 44);
            tutorialDone.GetComponentInChildren<TextMeshProUGUI>().fontSize = 18;
        }

        void BuildModal()
        {
            modal = Box("Pause and results scrim", canvasRect, WithAlpha(TablePalette.Background, .74f), true); Stretch(modal);
            modalGroup = modal.gameObject.AddComponent<CanvasGroup>();
            var card = Box("Pause card", modal, TablePalette.Ink, true);
            Anchor(card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(680, 730));
            Label(card, "TABLEFRONT / COMMAND ROOM", 15, TablePalette.Gold, new Vector2(36, -30), new Vector2(510, 28), FontStyles.Bold, 1.4f);
            modalTitle = Label(card, "Battle paused", 44, TablePalette.Text, new Vector2(36, -78), new Vector2(608, 62), FontStyles.Bold);
            modalBody = Label(card, "", 20, TablePalette.Text, new Vector2(36, -156), new Vector2(608, 210));
            resumeButton = Button(card, "Resume battle", game.TogglePause, true); Top(resumeButton.GetComponent<RectTransform>(), 36, 388, 608, 54);
            restartButton = Button(card, "Rematch", game.Rematch); Top(restartButton.GetComponent<RectTransform>(), 36, 452, 608, 54);
            var helpButton = Button(card, "Controls and settings", game.ToggleHelp); Top(helpButton.GetComponent<RectTransform>(), 36, 516, 608, 48);
            var menu = Button(card, "Return to title", game.ReturnToMenu); Top(menu.GetComponent<RectTransform>(), 36, 574, 608, 48);
            var exit = Button(card, "Exit game", game.ExitGame); Top(exit.GetComponent<RectTransform>(), 36, 654, 608, 48);
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
            Label(card, "Click / drag    Select blue units\nShift                 Add to selection\nRight-click      Move / focus an enemy\nCtrl+A              Select all\n4 / 5 / 6            Recall Alpha / Bravo / Charlie\nCtrl+4 / 5 / 6   Assign selected to squad\n1 / 2 / 3            Recruit Scout / Rifle / Tank\nTab                   Toggle neural command", 19, TablePalette.Text, new Vector2(36, -311), new Vector2(480, 250));
            Label(card, "CAMERA & BATTLE", 15, TablePalette.Gold, new Vector2(554, -266), new Vector2(420, 28), FontStyles.Bold, 1.4f);
            Label(card, "WASD / arrows   Pan the table\nMouse wheel        Zoom\nQ / E                      Orbit\nMiddle drag           Pan\nHome                     Focus your HQ\nF                              Focus current alert\nSpace / Escape      Pause\nF11                         Fullscreen", 19, TablePalette.Text, new Vector2(554, -311), new Vector2(430, 250));
            Label(card, "VOLUME", 13, TablePalette.Gold, new Vector2(36, -593), new Vector2(110, 24), FontStyles.Bold, 1);
            BuildVolume(card);
            reduced = BuildToggle(card, "Reduced motion", new Vector2(440, -590));
            reduced.onValueChanged.AddListener(value => { if (game != null) game.ReducedMotion = value; });
            helpClose = Button(card, "Back", game.ToggleHelp, true); Top(helpClose.GetComponent<RectTransform>(), 776, 630, 208, 50);
            Label(card, "Scouts: fast capture. Rifles: 35% less damage in cover.\nTanks: +20% damage against tanks. All armies share these rules.", 16, TablePalette.Text, new Vector2(36, -652), new Vector2(715, 48));
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
            if (menuVisible || modalVisible || helpVisible) assigningSquad = false;
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
            Set(orderText, game.SelectedOrderSummary);
            Set(roleText, game.SelectedRoleSummary);
            bool canCommand = !menuVisible && !observer && !game.Paused && !helpVisible && !sim.Finished && army.Alive;
            if (game.Selected.Count == 0) assigningSquad = false;
            for (int i = 0; i < 3; i++)
            {
                int count = game.GroupCount(i);
                Set(squadText[i], assigningSquad ? "Set " + squadNames[i] : $"{i + 4} {squadNames[i]} · {count}");
                squadButtons[i].interactable = canCommand && (assigningSquad ? game.Selected.Count > 0 : count > 0);
            }
            Set(assignSquadText, assigningSquad ? "Cancel assignment" : "Assign selected…");
            assignSquadButton.interactable = canCommand && game.Selected.Count > 0;
            bool showAlert = canCommand && game.TacticalAlertAvailable;
            alertButton.gameObject.SetActive(showAlert);
            if (showAlert) Set(alertText, game.TacticalAlert + "   ·   Focus · F");
            bool showTutorial = canCommand && game.TutorialActive;
            tutorialCard.gameObject.SetActive(showTutorial);
            if (showTutorial)
            {
                Set(tutorialProgress, game.TutorialStep >= 4 ? "FIELD TRAINING / COMPLETE" : $"FIELD TRAINING / {game.TutorialStep + 1} OF 4");
                Set(tutorialTitle, game.TutorialTitle);
                Set(tutorialInstruction, game.TutorialInstruction);
                tutorialSkip.gameObject.SetActive(game.TutorialStep < 4);
                tutorialDone.gameObject.SetActive(game.TutorialStep >= 4);
            }
            Set(notice, game.Notice ?? "");
            Set(speedText, game.Speed + "×");
            Set(commanderText, game.TutorialActive ? "LESSON / OPPONENTS WAIT" : army.IsAI ? "AI COMMANDING YOUR ARMY" : "YOU ARE IN COMMAND");
            commanderButton.GetComponentInChildren<TextMeshProUGUI>().text = army.IsAI ? "Take command  ·  Tab" : "AI command  ·  Tab";
            commanderButton.interactable = canCommand && !game.TutorialActive;
            bool canRecruit = !game.Paused && !helpVisible && !sim.Finished && army.Alive && sim.UnitCount(0) < BattleSimulation.UnitCap;
            Set(recruitStatus, !army.Alive ? "REINFORCEMENTS  /  HQ LOST" : sim.UnitCount(0) >= BattleSimulation.UnitCap ? "REINFORCEMENTS  /  ARMY AT CAPACITY" : army.RecruitCooldown > 0 ? $"REINFORCEMENTS  /  READY IN {Mathf.CeilToInt(army.RecruitCooldown)}s" : "REINFORCEMENTS  /  AT YOUR HQ");
            for (int i = 0; i < 3; i++)
            {
                int cost = BattleSimulation.Cost((UnitKind)i);
                recruit[i].interactable = canRecruit && army.Credits >= cost && army.RecruitCooldown <= 0;
                string name = ((UnitKind)i).ToString();
                string role = i == 0 ? "Fast capture" : i == 1 ? "Use cover" : "Break armor";
                Set(recruitText[i], $"{i + 1}  {name}\n<size=17>{cost} credits</size>\n\n<size=16>{role}</size>");
            }
            Set(spectatorIntents, $"<color=#{ColorUtility.ToHtmlStringRGB(TablePalette.Team(0))}>{sim.Armies[0].Name}</color>   {sim.Armies[0].Intent}\n<color=#{ColorUtility.ToHtmlStringRGB(TablePalette.Team(1))}>{sim.Armies[1].Name}</color>   {sim.Armies[1].Intent}\n<color=#{ColorUtility.ToHtmlStringRGB(TablePalette.Team(2))}>{sim.Armies[2].Name}</color>   {sim.Armies[2].Intent}");
            if (modalVisible)
            {
                bool finished = sim.Finished;
                Set(modalTitle, finished ? (sim.Winner < 0 ? "Battle drawn" : !game.PlayerMatch ? "Battle concluded" : sim.Winner == 0 ? "Victory secured" : "Army defeated") : "Battle paused");
                Set(modalBody, finished ? $"<color=#{ColorUtility.ToHtmlStringRGB(TablePalette.Gold)}>BATTLE RECAP · {mins:00}:{secs:00}</color>\n" + game.BattleRecap : "Your orders can wait.\nThe battlefield is frozen until you resume.");
                resumeButton.gameObject.SetActive(!finished);
                Top(restartButton.GetComponent<RectTransform>(), 36, finished ? 388 : 452, 608, 54);
                Primary(restartButton, finished);
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

        static void Primary(UnityEngine.UI.Button button, bool primary)
        {
            var colors = button.colors;
            colors.normalColor = primary ? TablePalette.Gold : WithAlpha(TablePalette.Text, .10f);
            colors.highlightedColor = primary ? Color.Lerp(TablePalette.Gold, TablePalette.Text, .25f) : WithAlpha(TablePalette.Text, .20f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = primary ? Color.Lerp(TablePalette.Gold, TablePalette.Ink, .20f) : WithAlpha(TablePalette.Gold, .32f);
            button.colors = colors;
            button.GetComponent<TablefrontButtonFeedback>().Configure(button.GetComponent<UnityEngine.UI.Outline>(), button.GetComponentInChildren<TextMeshProUGUI>(), primary);
        }

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
        static void SpanTop(RectTransform rect, float top, float height)
        { Anchor(rect, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -top), new Vector2(0, height)); }
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
