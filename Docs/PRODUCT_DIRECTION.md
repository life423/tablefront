# Tablefront product direction

November 23, 2026. The user chose **Windows PC first; tablet later**. The commercial direction is a **paid Windows v1 with a free demo**. The price is undecided. This records product decisions; it does not mean a storefront, payment flow, finished demo edition or tablet build has been delivered.

## Windows PC first

The current game is designed around a mouse, keyboard, a freely controlled tabletop camera and saved control groups. The first release should make commanding the army clear and responsive on a desktop display while preserving the painted miniature-tabletop identity.

Primary interaction stays visible: click or drag to select, click ground to move, click an opponent to attack. Visible class, recruitment and command buttons support playing without memorizing shortcuts. Optional shortcuts use A for attack-move, S for stop, H for hold, and the number row for saved groups. Arrow keys and middle drag pan the camera so army commands do not compete with camera movement.

The current content pass adds four selectable skirmish battlefields and five unit classes. These are replayable battle layouts under shared rules, not a linear story campaign. The title lets a player choose a map directly; the results screen's **Next battlefield** starts a fresh match on the next map. Tutorial practice stays on the original Greenfield layout and returns to the selected map for normal play.

## What makes this release worth playing

- Distinct physical silhouettes and base marks help identify classes when zoomed into a fight. Recognition at the widest overview remains a human playtest question.
- Selection, hover, orders, incoming damage and health changes communicate what the player's army is doing without covering the table in permanent labels.
- Scouts, Rifles, Tanks, Gunners and Artillery have useful strengths and counters. New content should create decisions, rather than duplicate a stronger version of the same unit.
- Distinct objective spacing and approaches give each battlefield a tactical reason to exist.
- A full tutorial, normal battle, result and rematch must work reliably before the release is described as finished.

Human replay remains necessary to judge whether these changes are understandable and fun. Automated victories and conservative design-review scores do not establish that outcome.

## Bridge Gambit: testing the distinctive mechanic

The user approved building one movable-bridge mission after challenging whether the game offers a compelling reason to play instead of an established RTS. The working hypothesis is that a battlefield the player can reshape creates tactical plans, counterplay and memorable reversals that give the tabletop identity a gameplay purpose.

The 0.6.0 experiment is **Bridge Gambit**: one arena, two armies, three familiar unit classes, and a shared bridge that pivots between two crossings. Both sides receive the same limited command budget, warning, movement time and docking grace. Water is a real movement boundary; troops use navigation through the docked crossing, wait when it changes, and ride it safely if already aboard. Capturing and continuously holding the enemy depot for 45 seconds, or eliminating its HQ, completes the mission. Normal four-map skirmishes and their five-unit roster remain separate.

This makes the proposed mechanic playable, rather than establishing that it is distinctive enough, intuitive or fun. Human playtests should look for deliberate route changes, understandable enemy responses, recoverable mistakes and voluntary replays with a different plan. The next decision is whether to develop this mechanic further, simplify it or replace it based on those observations. A controlled static-versus-movable comparison has not yet been delivered or run.

The neural policy is still frozen. The prototype enemy uses an explicit route-cost bridge controller under the same command rules; it has not learned bridge strategy. Adaptive rivals and resumed reinforcement learning remain future decisions, with training and CudaRL integration paused.

## Paid v1 and a free demo

The current GitHub playtest remains free to download. A future paid Windows v1 and free demo are the approved product direction. The paid content scope, demo boundary, price, release date and store choice remain undecided. No purchases, subscriptions, ads, online account requirement or storefront publication are implemented by this update.

A concrete demo boundary should be chosen after observing players finish the tutorial and voluntarily replay battles. The demonstration should let someone understand the controls and experience a satisfying complete match. The exact maps, units and progression included in that edition have not been finalized.

## Tablet later

A tablet edition is a later design and engineering task. It needs its own touch interaction model for selection, box/group selection, camera movement, zoom, orders and recovery from accidental input. It also needs a UI composed for smaller screens, readable text, reachable targets, safe-area support, and measured device-specific rendering/memory/thermal budgets.

The Windows release is not described as touch-ready simply because Unity can target another platform. Tablet operating system, minimum device, portrait/landscape policy, distribution method and port schedule remain open decisions. No Android or iPadOS build, certification or tablet-store publication is included in the current work.

## Neural training remains paused

The existing neural commanders remain frozen during gameplay. Current expanded skirmishes use the same trained strategic policy with an explicit recruitment-role compatibility adapter for Gunners and Artillery. The new maps and counters have not been newly learned. Training and CudaRL integration remain paused until the user authorizes that separate work. See [expanded rules](EXPANDED_SKIRMISH_RULES.md) for the adapter and test evidence.

## Next release gates

1. Observe the fixed tutorial and two complete human battles, including split-force orders, mistakes and rematches.
2. Check unit recognition and control clarity at representative Windows resolutions; follow up on text scaling, contrast and keyboard access.
3. Measure native frame times, memory and input behavior on the target PC. An opt-in local benchmark may support this; it must not be presented as server analytics or proof of tablet performance.
4. Use those results to tune pacing and difficulty, then define the free demo and paid-v1 content boundary.
5. Choose a store and price only after the playable product and its limitations can be reviewed concretely.
