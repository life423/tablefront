# Expanded skirmish interface

The expanded roster uses the existing runtime-built uGUI interface, shared ink/ivory/brass palette and TextMeshPro font. The interface keeps the playable table central while making the new content discoverable without learning keyboard shortcuts.

## Battlefield selection

The title shows one battlefield name and tactical description at a time. **Previous** and **Next** cycle through all four maps, with a visible **Battlefield 1 of 4** position. **Play vs AI** and **Watch AI battle** use that choice. The six-step tutorial retains its original Greenfield teaching table and returns to the chosen map when exited. Battle results offer **Rematch** and **Next battlefield**; this is a sequence of skirmish maps, without campaign unlocks or authored missions.

## Command deck

The header is 84 reference units high, reduced from 100. The deck is 192 high, reduced from 226. The title card remains 740 high so it can fit the existing CanvasScaler's ultrawide layout. These are layout dimensions, not native screen pixels.

The left side contains selection status, contextual order feedback, five labeled class-selection buttons and **Select whole army**, **Stop**, **Hold**, and **Attack-move**. The armed attack-move action and an entirely held selection receive the existing gold active state. Mixed selections show their unit count and number of classes rather than a long roster that can truncate.

The right side groups five recruitment actions under **Recruit at your HQ**. Each shows its unit, optional shortcut, credit cost and short role. The new buttons expose **Gunner · V** and **Artillery · B** alongside Scout, Rifle and Tank. Commander takeover and HQ focus remain visible in a small footer. The original three classes remain visible in the tutorial; the two new support classes appear in ordinary matches.

**Stop / S** cancels movement. **Hold / H** keeps a selected force in place while it fires at targets in range. **Attack-move / A**, followed by a destination, fights along the route. An ordinary ground click moves to the destination. The Help page explains arrow-key and middle-drag camera movement so S consistently means Stop; double-click selection and all ten StarCraft-style groups remain documented.

The tutorial coach is 400×292 reference units, reduced from 440×312, with the corrected step wording and explicit completion action preserved. The help text remains concise and divides mouse basics from optional shortcuts.

## Verification and limits

The [23 passing HUD checks](../TestResults/expanded-hud-runtime.txt) exercise actual visible callbacks for battlefield cycling, map-specific play, paid recruitment of both support classes, class selection, all-army selection, Hold/Stop/Attack-move, disabled commands while paused, tutorial disclosure and results-to-next-map progression. They also check the Artillery minimum-range hint and singular protected-rifle grammar. The [36 controls checks](../TestResults/expanded-controls-runtime.txt) cover actual virtual input, all four opening cameras, normal move versus attack-move and support-unit groups. Helpers preserve player preferences, stay outside Assets and send no native input.

The [24-state render record](../TestResults/expanded-layout.txt) covers title, expanded battle, Help, shortcuts and tutorial attack/completion across 1024×768, 1280×720, 2560×1440 and 3440×1440 with empty TMP overflow probes. All six minimum-size and native-1440p states were individually reviewed; ultrawide battle/attack and additional minimum-size Pause/results/Artillery selection were also reviewed. One ultrawide capture artifact was recaptured cleanly in a fresh Play session. The smallest secondary text remains a concern for adjustable text sizing and human readability. Global UI scaling is deferred; this does not implement touch or establish tablet support.

Conservative internal review heuristics: usability 8/10, hierarchy 8, accessibility 7, consistency 8, responsiveness 8, feedback 8, performance 7. The [30-second native sample](../TestResults/expanded-native-benchmark.json) averaged 143.7 FPS at 2560×1440 on the user's RTX 5070, but does not establish larger-army or device-wide performance. Priorities are human replay, adjustable text size and contrast/keyboard access, wide-view recognition, then longer performance samples on more devices. The revised artillery wording, singular cover feedback and River Crossing opening camera addressed concrete defects found in this pass.

The implementation follows the `ui` routing and `ui-ugui` skills: preserve the existing Canvas hierarchy and framework, use layout groups for the variable unit rows, keep noninteractive text out of raycasts, and retain visible button hover/focus/disabled feedback. Accessibility contrast, text enlargement and full keyboard world orders require their own review; no WCAG compliance claim is made.
