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
        RectTransform title, hud, modal, help, selection, helpBasics, helpAdvanced;
        TextMeshProUGUI credits, selectionText, notice, clock, commanderText, modalTitle, modalBody;
        TextMeshProUGUI[] scoreText = new TextMeshProUGUI[3];
        TextMeshProUGUI[] scoreStatus = new TextMeshProUGUI[3];
        RectTransform[] scoreFill = new RectTransform[3];
        UnityEngine.UI.Button[] recruit = new UnityEngine.UI.Button[5];
        TextMeshProUGUI[] recruitText = new TextMeshProUGUI[5];
        UnityEngine.UI.Button commanderButton, resumeButton, restartButton, playButton, tutorialButton, helpClose, allArmyButton, showTargetButton;
        TextMeshProUGUI speedText, recruitStatus, orderText, roleText;
        UnityEngine.UI.Button[] squadButtons = new UnityEngine.UI.Button[5];
        TextMeshProUGUI[] squadText = new TextMeshProUGUI[5];
        UnityEngine.UI.Button moveButton, stopButton, holdButton, attackMoveButton, nextBattlefieldButton;
        UnityEngine.UI.Button bridgeMissionButton, bridgeButton;
        RectTransform bridgePanel;
        TextMeshProUGUI bridgeGoal, bridgeState, bridgeDetail, bridgeBudget, bridgeButtonText, helpFooter, helpArmyShortcuts;
        TextMeshProUGUI battlefieldName, battlefieldDescription, battlefieldIndex;
        UnityEngine.UI.Button alertButton, tutorialDone, tutorialSkip, tutorialRecenter, shortcutsButton;
        TextMeshProUGUI alertText, tutorialProgress, tutorialTitle, tutorialInstruction, tutorialFeedback, practiceHeader, helpHeading, helpIntro;
        RectTransform tutorialCard;
        bool advancedShortcuts;
        readonly RectTransform[] scoreCards = new RectTransform[3];
        UnityEngine.UI.Slider volume;
        UnityEngine.UI.Toggle reduced;
        RectTransform commandSection, spectatorSection;
        TextMeshProUGUI spectatorIntents;
        bool wasMenu, wasModal, wasHelp;
        bool built;
        BattleSimulation feedbackSimulation;
        float feedbackStamp = -1, feedbackUntil;
        int feedbackUnit = -1;
        string hitFeedback = "";
        CanvasGroup titleGroup, modalGroup, helpGroup;
        TMP_FontAsset font;
        static readonly string[] RecruitKeys = { "Z", "X", "C", "V", "B" };
        static readonly string[] RecruitRoles = { "Fast capture", "Uses cover", "Anti-armor", "Anti-infantry", "Keep distance" };

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
            BuildBridgePanel();
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
            Anchor(card, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(40, 0), new Vector2(560, 740));
            var accent = Box("Gold rule", card, TablePalette.Gold); Top(accent, 32, 28, 72, 4);
            Label(card, "MINIATURE ARMIES / REAL-TIME TACTICS", 15, TablePalette.Gold, new Vector2(32, -52), new Vector2(496, 28), FontStyles.Bold, 1.1f);
            Label(card, "TABLE\nFRONT", 60, TablePalette.Text, new Vector2(26, -84), new Vector2(502, 140), FontStyles.Bold, -2f);
            Label(card, "THE WAR ROOM IS YOURS", 16, TablePalette.Muted, new Vector2(32, -230), new Vector2(496, 26), FontStyles.Bold, 1.7f);
            Label(card, "Command the blue army. Split your forces, seize three beacons, and turn the battle with one good flank.", 19, TablePalette.Text, new Vector2(32, -264), new Vector2(496, 60));
            var battlefield = Box("Battlefield selection", card, WithAlpha(TablePalette.Text, .04f));
            Top(battlefield, 32, 332, 496, 128);
            battlefieldIndex = Label(battlefield, "BATTLEFIELD", 13, TablePalette.Gold, new Vector2(12, -8), new Vector2(472, 20), FontStyles.Bold, .8f);
            var previous = Button(battlefield, "Previous", () => CycleBattlefield(-1));
            Top(previous.GetComponent<RectTransform>(), 12, 34, 92, 40);
            previous.GetComponentInChildren<TextMeshProUGUI>().fontSize = 16;
            battlefieldName = Label(battlefield, "", 21, TablePalette.Text, new Vector2(112, -37), new Vector2(272, 32), FontStyles.Bold);
            battlefieldName.alignment = TextAlignmentOptions.Center;
            var next = Button(battlefield, "Next", () => CycleBattlefield(1));
            Top(next.GetComponent<RectTransform>(), 392, 34, 92, 40);
            next.GetComponentInChildren<TextMeshProUGUI>().fontSize = 16;
            battlefieldDescription = Label(card, "", 16, TablePalette.Muted, new Vector2(44, -416), new Vector2(472, 40));
            bridgeMissionButton = Button(card, "Bridge Gambit · Try mission", game.StartBridgeMission, true);
            Top(bridgeMissionButton.GetComponent<RectTransform>(), 32, 476, 496, 48);
            bridgeMissionButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 22;
            Label(card, "Turn the bridge. Bypass the defended approach.", 16, TablePalette.Muted, new Vector2(32, -532), new Vector2(496, 28));
            tutorialButton = Button(card, "Tutorial", game.StartTutorial);
            Top(tutorialButton.GetComponent<RectTransform>(), 32, 568, 240, 44);
            tutorialButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 20;
            playButton = Button(card, "Play vs AI", () => game.StartBattle(false));
            Top(playButton.GetComponent<RectTransform>(), 288, 568, 240, 44);
            playButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 20;
            var watch = Button(card, "Watch AI battle", () => game.StartBattle(true));
            Top(watch.GetComponent<RectTransform>(), 32, 624, 496, 40);
            var helpButton = Button(card, "How to play", game.ToggleHelp);
            Top(helpButton.GetComponent<RectTransform>(), 32, 680, 304, 40);
            var quit = Button(card, "Exit", game.ExitGame);
            Top(quit.GetComponent<RectTransform>(), 348, 680, 180, 40);
            var policyTag = NeuralCommander.Available ? "THREE ARMIES  /  NEURAL OPPONENTS  /  OFFLINE" : "NEURAL POLICY UNAVAILABLE";
            var tag = Label(title, policyTag, 14, TablePalette.Text, Vector2.zero, new Vector2(850, 28), FontStyles.Bold, 1.2f);
            Anchor(tag.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 28), new Vector2(850, 28));
            tag.alignment = TextAlignmentOptions.Right;
        }

        void CycleBattlefield(int direction)
        {
            var battlefields = TablefrontBattlefields.All;
            int index = Array.FindIndex(battlefields, item => item.Id == game.SelectedBattlefield);
            index = (index + direction + battlefields.Length) % battlefields.Length;
            game.SetBattlefield(battlefields[index].Id);
            Refresh();
        }

        void BuildHud()
        {
            hud = Container("Battle HUD", canvasRect); Stretch(hud);
            var top = Box("Command header", hud, WithAlpha(TablePalette.Ink, .96f), true);
            Anchor(top, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, -16), new Vector2(-32, 84));
            var economy = Container("Economy", top); Fraction(economy, 0, .21f, 12, 8);
            credits = Label(economy, "", 22, TablePalette.Text, new Vector2(4, -2), new Vector2(310, 30), FontStyles.Bold);
            clock = Label(economy, "", 13, TablePalette.Muted, new Vector2(4, -36), new Vector2(320, 30));
            SpanTop(credits.rectTransform, 2, 30);
            SpanTop(clock.rectTransform, 36, 30);
            for (int i = 0; i < 3; i++)
            {
                var team = Container("Faction " + i, top); Fraction(team, .225f + i * .155f, .37f + i * .155f, 8, 8);
                scoreCards[i] = team;
                scoreText[i] = Label(team, "", 21, TablePalette.Team(i), new Vector2(0, -2), new Vector2(240, 28), FontStyles.Bold);
                scoreStatus[i] = Label(team, "", 13, TablePalette.Text, new Vector2(0, -32), new Vector2(240, 22));
                SpanTop(scoreText[i].rectTransform, 2, 28);
                SpanTop(scoreStatus[i].rectTransform, 32, 22);
                var line = Box("Score track", team, WithAlpha(TablePalette.Text, .13f));
                Anchor(line, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 5), new Vector2(0, 4));
                scoreFill[i] = Box("Score progress", line, TablePalette.Team(i));
                Anchor(scoreFill[i], Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero, Vector2.zero);
            }
            practiceHeader = Label(top, "TUTORIAL  /  PRACTICE TABLE", 22, TablePalette.Gold, new Vector2(0, 0), new Vector2(0, 55), FontStyles.Bold);
            Anchor(practiceHeader.rectTransform, new Vector2(.225f, 0), new Vector2(.70f, 1), new Vector2(0, .5f), Vector2.zero, Vector2.zero);
            practiceHeader.rectTransform.offsetMin = new Vector2(8, 12); practiceHeader.rectTransform.offsetMax = new Vector2(-8, -12);
            practiceHeader.alignment = TextAlignmentOptions.MidlineLeft;
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

            notice = Label(hud, "", 20, TablePalette.Text, Vector2.zero, new Vector2(900, 36), FontStyles.Bold);
            Anchor(notice.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 220), new Vector2(900, 36));
            notice.alignment = TextAlignmentOptions.Center;
            var noticeShadow = notice.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            noticeShadow.effectColor = TablePalette.Ink;
            noticeShadow.effectDistance = new Vector2(1, -2);

            var bottom = Box("Command deck", hud, WithAlpha(TablePalette.Ink, .97f), true);
            Anchor(bottom, Vector2.zero, new Vector2(1, 0), new Vector2(.5f, 0), new Vector2(0, 16), new Vector2(-32, 192));
            commandSection = Container("Player commands", bottom); Stretch(commandSection);
            var selected = Container("Selection", commandSection); Fraction(selected, 0, .44f, 16, 12);
            selectionText = Label(selected, "", 18, TablePalette.Text, Vector2.zero, new Vector2(0, 26), FontStyles.Bold);
            SpanTop(selectionText.rectTransform, 0, 26);
            orderText = Label(selected, "", 16, TablePalette.Gold, new Vector2(0, -29), new Vector2(0, 24));
            SpanTop(orderText.rectTransform, 29, 24);
            roleText = Label(selected, "", 14, TablePalette.Muted, new Vector2(0, -54), new Vector2(0, 24));
            SpanTop(roleText.rectTransform, 54, 24);
            var squadRow = Container("Choose a force", selected); SpanTop(squadRow, 82, 36);
            var squadLayout = squadRow.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            squadLayout.spacing = 6; squadLayout.childControlWidth = squadLayout.childControlHeight = true;
            squadLayout.childForceExpandWidth = squadLayout.childForceExpandHeight = true;
            for (int i = 0; i < squadButtons.Length; i++)
            {
                UnitKind kind = (UnitKind)i;
                squadButtons[i] = Button(squadRow, BattleSimulation.UnitName(kind, true), () => game.SelectKind(kind));
                squadText[i] = squadButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                squadText[i].fontSize = 15;
                LayoutItem(squadButtons[i].gameObject, 100, 36);
            }
            var selectionActions = Container("Selection actions", selected); SpanTop(selectionActions, 126, 42);
            var actionLayout = selectionActions.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            actionLayout.spacing = 8; actionLayout.childControlWidth = actionLayout.childControlHeight = true;
            actionLayout.childForceExpandWidth = actionLayout.childForceExpandHeight = true;
            allArmyButton = Button(selectionActions, "Select whole army", game.SelectAll);
            allArmyButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 15;
            LayoutItem(allArmyButton.gameObject, 144, 42);
            moveButton = Button(selectionActions, "Move", game.ArmMove);
            moveButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 15;
            LayoutItem(moveButton.gameObject, 72, 42);
            stopButton = Button(selectionActions, "Stop · S", game.StopSelected);
            stopButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 15;
            LayoutItem(stopButton.gameObject, 70, 42);
            holdButton = Button(selectionActions, "Hold · H", game.HoldSelected);
            holdButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 15;
            LayoutItem(holdButton.gameObject, 70, 42);
            attackMoveButton = Button(selectionActions, "Attack-move · A", game.ToggleAttackMove);
            attackMoveButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 14;
            LayoutItem(attackMoveButton.gameObject, 134, 42);
            var build = Container("Reinforcements", commandSection); Fraction(build, .455f, 1f, 14, 12);
            recruitStatus = Label(build, "REINFORCEMENTS  /  AT YOUR HQ", 13, TablePalette.Gold, new Vector2(0, 0), new Vector2(0, 22), FontStyles.Bold, .8f);
            SpanTop(recruitStatus.rectTransform, 0, 22);
            var recruits = Container("Recruitment row", build);
            Anchor(recruits, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -28), new Vector2(0, 96));
            var recruitsLayout = recruits.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            recruitsLayout.spacing = 6; recruitsLayout.childControlWidth = true; recruitsLayout.childControlHeight = true;
            recruitsLayout.childForceExpandWidth = true; recruitsLayout.childForceExpandHeight = true;
            for (int i = 0; i < recruit.Length; i++)
            {
                UnitKind kind = (UnitKind)i;
                recruit[i] = Button(recruits, "", () => game.Recruit(kind));
                recruit[i].gameObject.name = "Recruit " + BattleSimulation.UnitName(kind, kind == UnitKind.Rifle);
                recruitText[i] = recruit[i].GetComponentInChildren<TextMeshProUGUI>();
                recruitText[i].fontSize = 17;
                LayoutItem(recruit[i].gameObject, 128, 96);
            }
            var actions = Container("Commander", build); SpanTop(actions, 132, 36);
            commanderText = Label(actions, "", 13, TablePalette.Muted, Vector2.zero, new Vector2(0, 32), FontStyles.Bold);
            Anchor(commanderText.rectTransform, Vector2.zero, new Vector2(.36f, 1), Vector2.zero, Vector2.zero, Vector2.zero);
            commanderText.alignment = TextAlignmentOptions.MidlineLeft;
            commanderButton = Button(actions, "Let AI play", game.ToggleCommander);
            commanderButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 15;
            Anchor(commanderButton.GetComponent<RectTransform>(), new Vector2(.38f, 0), new Vector2(.68f, 1), Vector2.zero, Vector2.zero, Vector2.zero);
            showTargetButton = Button(actions, "Restart tutorial", game.StartTutorial);
            showTargetButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 15;
            Anchor(showTargetButton.GetComponent<RectTransform>(), new Vector2(.38f, 0), new Vector2(.68f, 1), Vector2.zero, Vector2.zero, Vector2.zero);
            var home = Button(actions, "Find my HQ", game.FocusBase);
            home.GetComponentInChildren<TextMeshProUGUI>().fontSize = 15;
            Anchor(home.GetComponent<RectTransform>(), new Vector2(.70f, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero, Vector2.zero);

            alertButton = Button(hud, "", game.FocusAlert);
            alertButton.gameObject.name = "Contextual battlefield alert";
            var alertColors = alertButton.colors;
            alertColors.normalColor = TablePalette.Ink;
            alertColors.highlightedColor = Color.Lerp(TablePalette.Ink, TablePalette.Gold, .16f);
            alertColors.selectedColor = alertColors.highlightedColor;
            alertColors.pressedColor = Color.Lerp(TablePalette.Ink, TablePalette.Gold, .25f);
            alertButton.colors = alertColors;
            Anchor(alertButton.GetComponent<RectTransform>(), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -116), new Vector2(740, 48));
            alertText = alertButton.GetComponentInChildren<TextMeshProUGUI>(); alertText.fontSize = 20;

            spectatorSection = Container("Spectator commands", bottom); Stretch(spectatorSection);
            Label(spectatorSection, "NEURAL WAR ROOM", 17, TablePalette.Gold, new Vector2(26, -20), new Vector2(300, 28), FontStyles.Bold, 1.2f);
            Label(spectatorSection, "Three commanders. Equal rules.", 20, TablePalette.Text, new Vector2(26, -64), new Vector2(350, 38));
            Label(spectatorSection, "Arrows pan  ·  Wheel zoom  ·  Q/E orbit", 15, TablePalette.Muted, new Vector2(26, -121), new Vector2(400, 28));
            spectatorIntents = Label(spectatorSection, "", 20, TablePalette.Text, Vector2.zero, new Vector2(1000, 122));
            Anchor(spectatorIntents.rectTransform, new Vector2(.31f, 0), new Vector2(1, 1), new Vector2(0, .5f), Vector2.zero, Vector2.zero);
            spectatorIntents.rectTransform.offsetMin = new Vector2(16, 16); spectatorIntents.rectTransform.offsetMax = new Vector2(-26, -16);
        }

        void BuildTutorial()
        {
            tutorialCard = Box("Tutorial coach", hud, TablePalette.Ink, true);
            Anchor(tutorialCard, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -116), new Vector2(400, 292));
            tutorialProgress = Label(tutorialCard, "TUTORIAL", 13, TablePalette.Gold, new Vector2(20, -16), new Vector2(360, 22), FontStyles.Bold, .8f);
            tutorialTitle = Label(tutorialCard, "", 24, TablePalette.Text, new Vector2(20, -48), new Vector2(360, 34), FontStyles.Bold);
            tutorialInstruction = Label(tutorialCard, "", 18, TablePalette.Text, new Vector2(20, -92), new Vector2(360, 104));
            tutorialFeedback = Label(tutorialCard, "", 15, TablePalette.Gold, new Vector2(20, -204), new Vector2(360, 30));
            tutorialRecenter = Button(tutorialCard, "Show target", game.RecenterTutorial);
            Top(tutorialRecenter.GetComponent<RectTransform>(), 20, 240, 172, 40);
            tutorialRecenter.GetComponentInChildren<TextMeshProUGUI>().fontSize = 17;
            tutorialSkip = Button(tutorialCard, "Exit tutorial", game.SkipTutorial);
            Top(tutorialSkip.GetComponent<RectTransform>(), 200, 240, 180, 40);
            tutorialSkip.GetComponentInChildren<TextMeshProUGUI>().fontSize = 17;
            tutorialDone = Button(tutorialCard, "Play a battle", game.CompleteTutorial, true);
            Top(tutorialDone.GetComponent<RectTransform>(), 20, 240, 172, 40);
            tutorialDone.GetComponentInChildren<TextMeshProUGUI>().fontSize = 17;
        }

        void BuildBridgePanel()
        {
            bridgePanel = Box("Bridge mission commands", hud, WithAlpha(TablePalette.Ink, .97f), true);
            Anchor(bridgePanel, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, -112), new Vector2(-32, 104));
            var goal = Container("Mission objective", bridgePanel); Fraction(goal, 0, .31f, 14, 10);
            var heading = Label(goal, "BRIDGE GAMBIT", 14, TablePalette.Gold, Vector2.zero, new Vector2(0, 22), FontStyles.Bold, .8f);
            SpanTop(heading.rectTransform, 0, 22);
            bridgeGoal = Label(goal, "Hold the enemy Depot uncontested for 45s, or destroy its HQ.", 17, TablePalette.Text, new Vector2(0, -28), new Vector2(0, 50));
            SpanTop(bridgeGoal.rectTransform, 28, 50);
            var state = Container("Bridge route status", bridgePanel); Fraction(state, .32f, .67f, 12, 10);
            bridgeState = Label(state, "", 18, TablePalette.Text, Vector2.zero, new Vector2(0, 28), FontStyles.Bold);
            SpanTop(bridgeState.rectTransform, 0, 28);
            bridgeDetail = Label(state, "3s warning. Riders travel with the bridge.", 15, TablePalette.Muted, new Vector2(0, -34), new Vector2(0, 46));
            SpanTop(bridgeDetail.rectTransform, 34, 46);
            var control = Container("Bridge control", bridgePanel); Fraction(control, .68f, 1, 12, 10);
            bridgeButton = Button(control, "", () => game.RequestBridge(game.Sim.Bridge.CurrentRoute == BridgeRoute.North ? BridgeRoute.South : BridgeRoute.North), true);
            SpanTop(bridgeButton.GetComponent<RectTransform>(), 0, 46);
            bridgeButton.gameObject.name = "Change bridge crossing";
            bridgeButtonText = bridgeButton.GetComponentInChildren<TextMeshProUGUI>();
            bridgeButtonText.fontSize = 18;
            bridgeButton.GetComponent<TablefrontButtonFeedback>().KeepDisabledTextReadable = true;
            bridgeBudget = Label(control, "", 14, TablePalette.Muted, new Vector2(0, -50), new Vector2(0, 38));
            SpanTop(bridgeBudget.rectTransform, 50, 38);
            bridgePanel.gameObject.SetActive(false);
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
            nextBattlefieldButton = Button(card, "Next battlefield", game.NextBattlefield);
            Top(nextBattlefieldButton.GetComponent<RectTransform>(), 348, 388, 296, 54);
            nextBattlefieldButton.gameObject.SetActive(false);
            var helpButton = Button(card, "Controls", game.ToggleHelp); Top(helpButton.GetComponent<RectTransform>(), 36, 516, 296, 48);
            var tutorial = Button(card, "Tutorial", game.StartTutorial); Top(tutorial.GetComponent<RectTransform>(), 348, 516, 296, 48);
            var menu = Button(card, "Return to title", game.ReturnToMenu); Top(menu.GetComponent<RectTransform>(), 36, 574, 608, 48);
            var exit = Button(card, "Exit game", game.ExitGame); Top(exit.GetComponent<RectTransform>(), 36, 654, 608, 48);
        }

        void BuildHelp()
        {
            help = Box("Help scrim", canvasRect, WithAlpha(TablePalette.Background, .85f), true); Stretch(help);
            helpGroup = help.gameObject.AddComponent<CanvasGroup>();
            var card = Box("Controls and settings", help, TablePalette.Ink, true);
            Anchor(card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1020, 716));
            Label(card, "HOW TO PLAY", 15, TablePalette.Gold, new Vector2(36, -28), new Vector2(800, 24), FontStyles.Bold, 2);
            helpHeading = Label(card, "Play with your mouse", 42, TablePalette.Text, new Vector2(36, -68), new Vector2(948, 64), FontStyles.Bold);
            helpIntro = Label(card, "Capture beacons to earn credits and victory points. Reach 350 points, or be the last HQ standing.", 21, TablePalette.Text, new Vector2(36, -145), new Vector2(948, 65));
            var rule = Box("Controls rule", card, WithAlpha(TablePalette.Gold, .4f)); Top(rule, 36, 222, 948, 2);
            helpBasics = Container("Mouse controls", card); Top(helpBasics, 36, 244, 948, 250);
            HelpStep(helpBasics, "1", "Choose your troops", "Click a blue unit, drag a box, or use a unit-class button. Double-click a unit to select its visible class.", 0);
            HelpStep(helpBasics, "2", "Plan your route", "Click ground to move or an enemy to attack. Hold Shift while clicking to queue orders; the route and formation show your plan.", 82);
            HelpStep(helpBasics, "3", "Add reinforcements", "Choose a unit under Recruit at your HQ. Scouts capture, rifles use cover, and support units need escorts.", 164);
            helpAdvanced = Container("Optional keyboard shortcuts", card); Top(helpAdvanced, 36, 244, 948, 250);
            helpArmyShortcuts = Label(helpAdvanced, "", 18, TablePalette.Text, Vector2.zero, new Vector2(482, 244));
            Label(helpAdvanced, "Arrows               Pan the table\nMouse wheel       Zoom\nMiddle drag         Pan\nQ / E                    Turn the table\nHome / F              Find HQ / current alert\nSpace                    Pause\nEscape                  Cancel order / pause\nTab / Shift+Tab    Move between buttons\nEnter / F11          Press button / fullscreen", 18, TablePalette.Text, new Vector2(516, 0), new Vector2(432, 244));
            shortcutsButton = Button(card, "Keyboard shortcuts", () => { advancedShortcuts = !advancedShortcuts; Refresh(); });
            Top(shortcutsButton.GetComponent<RectTransform>(), 36, 516, 272, 48);
            var tutorial = Button(card, "Start tutorial", game.StartTutorial, true);
            Top(tutorial.GetComponent<RectTransform>(), 324, 516, 660, 48);
            Label(card, "VOLUME", 13, TablePalette.Gold, new Vector2(36, -603), new Vector2(110, 24), FontStyles.Bold, 1);
            BuildVolume(card);
            reduced = BuildToggle(card, "Reduced motion", new Vector2(440, -590));
            reduced.onValueChanged.AddListener(value => { if (game != null) game.ReducedMotion = value; });
            helpClose = Button(card, "Back", game.ToggleHelp); Top(helpClose.GetComponent<RectTransform>(), 776, 590, 208, 50);
            helpFooter = Label(card, "", 16, TablePalette.Muted, new Vector2(36, -658), new Vector2(948, 44));
        }

        void HelpStep(Transform parent, string number, string heading, string body, float top)
        {
            Label(parent, number, 30, TablePalette.Gold, new Vector2(0, -top), new Vector2(40, 44), FontStyles.Bold);
            Label(parent, heading, 24, TablePalette.Text, new Vector2(58, -top), new Vector2(890, 34), FontStyles.Bold);
            Label(parent, body, 19, TablePalette.Text, new Vector2(58, -top - 36), new Vector2(890, 46));
        }

        void BuildVolume(RectTransform parent)
        {
            var sliderRoot = Container("Master volume", parent);
            Top(sliderRoot, 150, 592, 240, 44);
            var hitArea = sliderRoot.gameObject.AddComponent<UnityEngine.UI.Image>();
            hitArea.color = Color.clear; hitArea.raycastTarget = true;
            volume = sliderRoot.gameObject.AddComponent<UnityEngine.UI.Slider>();
            volume.minValue = 0; volume.maxValue = 1;
            var background = Box("Volume track", sliderRoot, WithAlpha(TablePalette.Text, .18f));
            Anchor(background, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(-20, 6));
            var fillArea = Container("Volume fill area", sliderRoot);
            Anchor(fillArea, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(-20, 6));
            var fillRect = Box("Volume fill", fillArea, TablePalette.Gold);
            Stretch(fillRect);
            var handleArea = Container("Volume handle area", sliderRoot);
            Anchor(handleArea, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(-20, 28));
            var handle = Box("Volume handle", handleArea, TablePalette.Text, true);
            Anchor(handle, Vector2.zero, new Vector2(0, 1), new Vector2(.5f, .5f), Vector2.zero, new Vector2(18, 0));
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
            bool mission = sim != null && sim.MissionActive && !game.InMenu;
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
            if (helpVisible && !wasHelp) advancedShortcuts = false;
            helpBasics.gameObject.SetActive(!advancedShortcuts);
            helpAdvanced.gameObject.SetActive(advancedShortcuts);
            Set(helpHeading, advancedShortcuts ? "Optional shortcuts" : "Play with your mouse");
            Set(helpIntro, advancedShortcuts ? "Every essential command has a labeled button. Use these shortcuts when you are ready." : mission ? "Hold the enemy Depot uncontested for 45 seconds, or destroy its HQ. Enemy capture attempts reset the timer. Change the bridge crossing to open a different route." : "Capture beacons to earn credits and victory points. Reach 350 points, or be the last HQ standing.");
            Set(helpArmyShortcuts, "Ctrl+0–9         Save / replace a group\nShift+0–9       Add selected to a group\n0–9 / double-tap   Select / center group\nA / S / H         Attack-move / Stop / Hold\n" + (mission ? "R                       Change bridge crossing\nZ / X / C         Recruit Scout / Rifle / Tank\n" : "Z / X / C         Recruit Scout / Rifle / Tank\nV / B               Recruit Gunner / Artillery\n") + "Ctrl+A              Select whole army\nShift+unit click  Add or remove a unit\nShift+ground     Queue the next order\nRight-click       Alternative move / attack");
            Set(helpFooter, mission ? "Water blocks movement; stone screens block shots. Flank marked cover. Bridge moves warn riders for 3s.\nHold ROTARY uncontested for 20s: +1 bridge command, up to three. Riders travel with the bridge." : "Scouts capture. Rifles use cover. Tanks break armor. Gunners counter infantry. Artillery cannot fire up close.\nMouse controls are enough to play; hold Shift while giving an order to queue it.");
            Set(shortcutsButton.GetComponentInChildren<TextMeshProUGUI>(), advancedShortcuts ? "Mouse basics" : "Keyboard shortcuts");
            Set(tutorialButton.GetComponentInChildren<TextMeshProUGUI>(), game.TutorialCompleted ? "Tutorial" : "Tutorial  ·  Start here");
            var chosenBattlefield = TablefrontBattlefields.Get(game.SelectedBattlefield);
            Set(battlefieldName, chosenBattlefield.Name);
            Set(battlefieldDescription, chosenBattlefield.Description);
            int chosenIndex = Array.FindIndex(TablefrontBattlefields.All, item => item.Id == game.SelectedBattlefield);
            Set(battlefieldIndex, "BATTLEFIELD " + (chosenIndex + 1) + " OF " + TablefrontBattlefields.All.Length);
            Primary(tutorialButton, false);
            Primary(playButton, false);
            // Restore focus only on real panel transitions, allowing normal keyboard navigation.
            if (helpVisible && !wasHelp)
            {
                volume.SetValueWithoutNotify(game.MasterVolume);
                reduced.SetIsOnWithoutNotify(game.ReducedMotion);
                Focus(helpClose);
            }
            else if (!helpVisible && wasHelp)
                Focus(menuVisible ? (game.TutorialCompleted ? playButton : tutorialButton) : modalVisible ? (ended ? restartButton : resumeButton) : null);
            else if (menuVisible && !wasMenu && !helpVisible) Focus(game.TutorialCompleted ? playButton : tutorialButton);
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
            Set(clock, game.TutorialActive ? "SAFE PRACTICE  /  GO AT YOUR OWN PACE" : mission ? $"{mins:00}:{secs:00}   /   BRIDGE GAMBIT" : $"{mins:00}:{secs:00}   /   FIRST TO {(int)BattleSimulation.VictoryScore} POINTS");
            practiceHeader.gameObject.SetActive(game.TutorialActive);
            for (int i = 0; i < 3; i++)
            {
                scoreCards[i].gameObject.SetActive(!game.TutorialActive && (!mission || i < 2));
                if (mission && i < 2) Fraction(scoreCards[i], .225f + i * .225f, .44f + i * .225f, 8, 8);
                else Fraction(scoreCards[i], .225f + i * .155f, .37f + i * .155f, 8, 8);
                Army team = sim.Armies[i];
                float missionHold = mission ? sim.MissionHoldSeconds(i) : 0;
                Set(scoreText[i], mission ? $"{team.Name}  {Mathf.FloorToInt(missionHold)}/{(int)BattleSimulation.MissionHoldRequired}s" : $"{team.Name}  {Mathf.FloorToInt(team.Score)}");
                Set(scoreStatus[i], team.Alive ? mission ? $"Enemy Depot hold  ·  HQ {Mathf.CeilToInt(team.BaseHealth)}" : $"{sim.OwnedPoints(i)} beacons  ·  HQ {Mathf.CeilToInt(team.BaseHealth)}" : "ELIMINATED");
                scoreFill[i].anchorMax = new Vector2(Mathf.Clamp01(mission ? missionHold / BattleSimulation.MissionHoldRequired : team.Score / BattleSimulation.VictoryScore), 1);
            }
            Set(selectionText, CompactSelectionSummary());
            Set(orderText, string.IsNullOrEmpty(game.PlanningHint) ? game.CommandHint : game.PlanningHint);
            int protectedCount = 0;
            foreach (var troop in sim.Units) if (troop.Alive && game.Selected.Contains(troop.Id) && sim.IsProtected(troop)) protectedCount++;
            bool artillerySelected = sim.Units.Exists(troop => troop.Alive && game.Selected.Contains(troop.Id) && troop.Kind == UnitKind.Artillery);
            string tacticalFeedback = TacticalFeedback(sim, mission);
            Set(roleText, tacticalFeedback.Length > 0 ? tacticalFeedback : game.QueuedCount > 0 ? (game.QueuedCount == 1 ? "1 order queued" : "Up to " + game.QueuedCount + " orders queued") + " · Stop clears the plan" : game.Selected.Count == 0 ? "Double-click a unit to select its visible class" : artillerySelected ? "Artillery cannot fire at close targets" : protectedCount > 0 ? protectedCount + (mission ? protectedCount == 1 ? " rifle in directional cover · flank to bypass" : " rifles in directional cover · flank to bypass" : protectedCount == 1 ? " rifle protected by cover" : " rifles protected by cover") : game.SelectedOrderSummary);
            roleText.color = tacticalFeedback.Length > 0 ? TablePalette.Gold : TablePalette.Muted;
            bool canCommand = !menuVisible && !observer && !game.Paused && !helpVisible && !sim.Finished && army.Alive;
            for (int i = 0; i < squadButtons.Length; i++)
            {
                int count = 0;
                foreach (var troop in sim.Units) if (troop.Alive && troop.Team == 0 && (int)troop.Kind == i) count++;
                squadButtons[i].gameObject.SetActive((!game.TutorialActive && !mission) || i < 3);
                Set(squadText[i], $"{BattleSimulation.UnitName((UnitKind)i, true)} · {count}");
                squadButtons[i].interactable = canCommand && count > 0;
                Primary(squadButtons[i], game.TutorialActive && game.TutorialStep == 5 && i == 1);
            }
            allArmyButton.interactable = canCommand && sim.UnitCount(0) > 0;
            Primary(allArmyButton, game.TutorialActive && game.TutorialStep == 0);
            bool selected = canCommand && game.Selected.Count > 0;
            moveButton.interactable = stopButton.interactable = holdButton.interactable = attackMoveButton.interactable = selected;
            Primary(moveButton, game.MoveArmed);
            Primary(attackMoveButton, game.AttackMoveArmed);
            int selectedLive = 0, holding = 0;
            foreach (var troop in sim.Units)
                if (troop.Alive && troop.Team == 0 && game.Selected.Contains(troop.Id))
                { selectedLive++; if (troop.HoldPosition) holding++; }
            Primary(holdButton, selectedLive > 0 && holding == selectedLive);
            bool showAlert = canCommand && game.TacticalAlertAvailable && !mission;
            alertButton.gameObject.SetActive(showAlert);
            if (showAlert) Set(alertText, game.TacticalAlert + "   ·   Focus · F");
            bool showTutorial = canCommand && game.TutorialActive;
            tutorialCard.gameObject.SetActive(showTutorial);
            if (showTutorial)
            {
                Set(tutorialProgress, game.TutorialStep >= 6 ? "TUTORIAL COMPLETE" : $"TUTORIAL — STEP {game.TutorialStep + 1} OF 6");
                Set(tutorialTitle, game.TutorialTitle);
                Set(tutorialInstruction, game.TutorialInstruction);
                Set(tutorialFeedback, game.TutorialFeedback);
                Set(tutorialSkip.GetComponentInChildren<TextMeshProUGUI>(), game.TutorialStep >= 6 ? "Return to menu" : "Exit tutorial");
                tutorialRecenter.gameObject.SetActive(game.TutorialStep < 6);
                tutorialDone.gameObject.SetActive(game.TutorialStep >= 6);
            }
            Set(notice, game.Notice ?? "");
            Set(speedText, game.Speed + "×");
            Set(commanderText, game.TutorialActive ? "Tutorial · safe practice" : mission ? "Bridge Gambit · your army" : army.IsAI ? "AI is playing" : "You are in control");
            commanderButton.gameObject.SetActive(!game.TutorialActive && !mission);
            showTargetButton.gameObject.SetActive(game.TutorialActive);
            showTargetButton.interactable = canCommand;
            commanderButton.GetComponentInChildren<TextMeshProUGUI>().text = army.IsAI ? "Take control" : "Let AI play";
            commanderButton.interactable = canCommand && !game.TutorialActive;
            bool canRecruit = !game.Paused && !helpVisible && !sim.Finished && army.Alive && sim.UnitCount(0) < BattleSimulation.UnitCap;
            Set(recruitStatus, !army.Alive ? "RECRUITMENT  /  HQ LOST" : sim.UnitCount(0) >= BattleSimulation.UnitCap ? "RECRUITMENT  /  ARMY AT CAPACITY" : army.RecruitCooldown > 0 ? $"RECRUIT AT YOUR HQ  /  READY IN {Mathf.CeilToInt(army.RecruitCooldown)}s" : "RECRUIT AT YOUR HQ");
            for (int i = 0; i < recruit.Length; i++)
            {
                recruit[i].gameObject.SetActive((!game.TutorialActive && !mission) || i < 3);
                int cost = BattleSimulation.Cost((UnitKind)i);
                bool lessonAllowsRecruit = !game.TutorialActive || game.TutorialStep >= 6 || (game.TutorialStep == 3 && i == 1);
                recruit[i].interactable = canRecruit && lessonAllowsRecruit && army.Credits >= cost && army.RecruitCooldown <= 0;
                string name = BattleSimulation.UnitName((UnitKind)i, i == 1);
                Set(recruitText[i], $"{name} · {RecruitKeys[i]}\n<size=15>{cost} credits</size>\n<size=14>{RecruitRoles[i]}</size>");
                Primary(recruit[i], game.TutorialActive && game.TutorialStep == 3 && i == 1);
            }
            Set(spectatorIntents, $"<color=#{ColorUtility.ToHtmlStringRGB(TablePalette.Team(0))}>{sim.Armies[0].Name}</color>   {sim.Armies[0].Intent}\n<color=#{ColorUtility.ToHtmlStringRGB(TablePalette.Team(1))}>{sim.Armies[1].Name}</color>   {sim.Armies[1].Intent}\n<color=#{ColorUtility.ToHtmlStringRGB(TablePalette.Team(2))}>{sim.Armies[2].Name}</color>   {sim.Armies[2].Intent}");
            if (modalVisible)
            {
                bool finished = sim.Finished;
                Set(modalTitle, finished ? (sim.Winner < 0 ? "Battle drawn" : !game.PlayerMatch ? "Battle concluded" : sim.Winner == 0 ? "Victory secured" : "Army defeated") : game.TutorialActive ? "Tutorial paused" : "Battle paused");
                Set(modalBody, finished ? $"<color=#{ColorUtility.ToHtmlStringRGB(TablePalette.Gold)}>BATTLE RECAP · {mins:00}:{secs:00}</color>\n" + game.BattleRecap : game.TutorialActive ? "Take your time.\nResume the lesson, or restart it from the beginning." : "Your orders can wait.\nThe battlefield is frozen until you resume.");
                Set(resumeButton.GetComponentInChildren<TextMeshProUGUI>(), game.TutorialActive ? "Resume tutorial" : "Resume battle");
                Set(restartButton.GetComponentInChildren<TextMeshProUGUI>(), game.TutorialActive ? "Restart tutorial" : "Rematch");
                resumeButton.gameObject.SetActive(!finished);
                Top(restartButton.GetComponent<RectTransform>(), 36, finished ? 388 : 452, finished ? 296 : 608, 54);
                nextBattlefieldButton.gameObject.SetActive(finished && !game.TutorialActive && !mission);
                if (mission)
                {
                    Set(modalTitle, finished ? sim.Winner < 0 ? "Mission drawn" : sim.Winner == 0 ? "Mission accomplished" : "Mission lost" : "Mission paused");
                    Set(modalBody, finished ? game.BattleRecap : "Your orders and the bridge are paused.\nResume to continue Bridge Gambit.");
                    Set(restartButton.GetComponentInChildren<TextMeshProUGUI>(), finished ? "Retry Bridge Gambit" : "Restart mission");
                    Top(restartButton.GetComponent<RectTransform>(), 36, finished ? 388 : 452, 608, 54);
                }
                Primary(restartButton, finished);
            }
            RefreshBridge(mission, canCommand);
        }

        string TacticalFeedback(BattleSimulation sim, bool mission)
        {
            if (feedbackSimulation != sim)
            { feedbackSimulation = sim; feedbackStamp = -1; feedbackUntil = 0; feedbackUnit = -1; hitFeedback = ""; }
            if (!mission || game.InMenu || game.Paused || game.HelpOpen || sim.Finished) return "";
            if (feedbackStamp != sim.Elapsed)
            {
                feedbackStamp = sim.Elapsed;
                foreach (var hit in sim.Events)
                {
                    if (!hit.ProtectedHit && !hit.FlankedHit) continue;
                    bool outgoing = hit.Team == 0 && game.Selected.Contains(hit.ShooterId);
                    bool incoming = hit.Team != 0 && game.Selected.Contains(hit.TargetId);
                    if (!outgoing && !incoming) continue;
                    feedbackUnit = outgoing ? hit.ShooterId : hit.TargetId;
                    hitFeedback = outgoing ? hit.FlankedHit ? "Flank hit · enemy cover bypassed" : "Frontal cover reduced damage · try a flank"
                        : hit.FlankedHit ? "Your rifle was flanked · cover bypassed" : "Frontal cover protected your rifle";
                    feedbackUntil = Time.unscaledTime + 1.4f;
                }
            }
            return game.Selected.Contains(feedbackUnit) && Time.unscaledTime < feedbackUntil ? hitFeedback : "";
        }

        void RefreshBridge(bool mission, bool canCommand)
        {
            bridgePanel.gameObject.SetActive(mission);
            if (!mission || game.Sim.Bridge == null) { if (game.World) game.World.SetBridgePlanning(false); return; }
            var bridge = game.Sim.Bridge;
            bool stable = bridge.Phase == BridgePhase.Stable;
            string current = bridge.CurrentRoute == BridgeRoute.North ? "North" : "South";
            string target = (stable ? bridge.CurrentRoute == BridgeRoute.North ? BridgeRoute.South : BridgeRoute.North : bridge.TargetRoute) == BridgeRoute.North ? "North" : "South";
            string actor = bridge.ActorTeam == 0 ? "YOU" : "EMBER";
            Set(bridgeGoal, "Hold the enemy Depot uncontested for 45s, or destroy its HQ.");
            Set(bridgeState, stable ? current + " crossing open" : bridge.Phase == BridgePhase.Warning ? actor + " · " + target + " opens in " + Mathf.CeilToInt(bridge.TimeToChange) + "s" : actor + " · bridge moving · " + Mathf.CeilToInt(bridge.TimeToChange) + "s");
            bridgeState.color = stable ? TablePalette.Text : TablePalette.Team(Mathf.Clamp(bridge.ActorTeam, 0, 1));
            Set(bridgeDetail, stable ? "North: defended approach. South: flank.\n3s warning; riders travel with the bridge." : bridge.Phase == BridgePhase.Warning ? "New troops wait. Riders stay aboard as it turns." : "Riders travel safely. Orders resume when docked.");
            int remaining = bridge.CommandsRemaining(0), enemy = bridge.CommandsRemaining(1);
            float cooldown = bridge.CooldownRemaining(0);
            var rotary = game.Sim.Points[2];
            bool changingOwner = rotary.Owner >= 0 && rotary.CapturingTeam >= 0 && rotary.Owner != rotary.CapturingTeam;
            string recharge = rotary.Contested || changingOwner ? "ROTARY contested · recharge resets" : rotary.Owner < 0 ? "Hold ROTARY: +1 command / 20s" : rotary.Owner == 0
                ? remaining >= TablefrontBridge.CommandsPerTeam ? "ROTARY held · commands full" : "ROTARY: +1 in " + Mathf.CeilToInt(BattleSimulation.BridgeRechargeRequired - game.Sim.BridgeRechargeSeconds(0)) + "s"
                : enemy >= TablefrontBridge.CommandsPerTeam ? "Enemy holds ROTARY · commands full" : "Enemy recharge: " + Mathf.CeilToInt(game.Sim.BridgeRechargeSeconds(1)) + "/20s";
            Set(bridgeBudget, "Your commands " + remaining + " · Enemy " + enemy + (cooldown > 0 ? " · " + Mathf.CeilToInt(cooldown) + "s" : "") + "\n" + recharge);
            Set(bridgeButtonText, stable ? remaining <= 0 ? "Hold ROTARY to recharge" : "Open " + target + " crossing · R" : "Opening " + target + " crossing");
            bridgeButton.interactable = canCommand && stable && remaining > 0 && cooldown <= 0;
            var pointer = UnityEngine.InputSystem.Mouse.current;
            bool preview = bridgeButton.interactable && ((pointer != null && RectTransformUtility.RectangleContainsScreenPoint(bridgeButton.GetComponent<RectTransform>(), pointer.position.ReadValue(), null)) ||
                EventSystem.current != null && EventSystem.current.currentSelectedGameObject == bridgeButton.gameObject);
            if (game.World) game.World.SetBridgePlanning(preview);
            if (preview)
            {
                Set(bridgeState, current + " open · preview " + target);
                bridgeState.color = TablePalette.Team(0);
                Set(bridgeDetail, "Ghost shows the new landing.\nRiders travel with the bridge.");
            }
        }

        string CompactSelectionSummary()
        {
            int count = 0, kinds = 0;
            UnitKind onlyKind = UnitKind.Scout;
            foreach (var kind in BattleSimulation.UnitKinds)
            {
                int ofKind = 0;
                foreach (var troop in game.Sim.Units)
                    if (troop.Alive && troop.Team == 0 && troop.Kind == kind && game.Selected.Contains(troop.Id)) ofKind++;
                if (ofKind == 0) continue;
                count += ofKind; kinds++; onlyKind = kind;
            }
            if (count == 0) return "No units selected";
            if (kinds == 1)
                return count + " " + (onlyKind == UnitKind.Artillery ? "artillery " + (count == 1 ? "unit" : "units") : BattleSimulation.UnitName(onlyKind, count != 1)) + " selected";
            return count + " selected · " + kinds + " unit types";
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

        public void FocusNextControl(bool reverse)
        {
            if (!built || EventSystem.current == null) return;
            RectTransform scope = game.HelpOpen ? help : game.InMenu ? title : game.Paused || game.Sim.Finished ? modal : hud;
            var controls = new List<UnityEngine.UI.Selectable>();
            foreach (var control in scope.GetComponentsInChildren<UnityEngine.UI.Selectable>())
                if (control.IsActive() && control.IsInteractable()) controls.Add(control);
            if (controls.Count == 0) return;
            GameObject current = EventSystem.current.currentSelectedGameObject;
            int index = controls.FindIndex(control => control.gameObject == current);
            int next = index < 0 ? (reverse ? controls.Count - 1 : 0) : (index + (reverse ? -1 : 1) + controls.Count) % controls.Count;
            Focus(controls[next]);
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
        public bool KeepDisabledTextReadable { get; set; }
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
            if (!button.interactable)
            {
                if (KeepDisabledTextReadable) color = TablePalette.Muted;
                else color.a = .40f;
            }
            text.color = color;
        }
    }
}
