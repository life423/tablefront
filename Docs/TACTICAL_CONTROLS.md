# Tablefront tactical planning controls

The command flow stays mouse-first: select blue troops, then click a destination or enemy. Planning adds visible routes, formation endpoints and optional queued orders without requiring a new mode.

## Give an order

- **Click a blue troop** to select it. Drag a box to select several. **Shift+click a friendly** adds or removes it; **Shift+drag** adds a box selection.
- **Click ground** with troops selected to move in formation. **Click an enemy** to focus an attack. Right-click is an alternative for either order.
- **Hold Shift while clicking ground or an enemy** to add an order after the existing plan. Release Shift and click to replace the whole plan.
- The optional **Move** button arms a destination click, including ground occupied by a friendly. It is not needed for ordinary movement. **Escape** cancels a pending Move or attack-move action before pausing.

Hovering a destination previews its actual formation endpoints and the routes used by the movement rules. Gold indicates a proposed order; blue shows accepted routes for selected troops. The preview changes to orange while the bridge changes, and waiting routes end at a safe approach with a diamond marker. An unavailable destination has a cross and a brief explanation in the existing command area. Orders never draw a shortcut across water or stone walls.

With Shift held, the new preview begins at each troop's last queued endpoint, or its current destination if no later orders exist. Enemy destinations track live target positions when previewed. Accepted routes remain visible only for the selected living force. Selecting another force changes the context; pausing, opening Help or returning to the title hides the planning view.

## Keep a manageable plan

Each troop can hold **12 pending orders** in addition to its current command. A full plan rejects another append without discarding earlier orders. Release Shift to replace it, or use **Stop** to clear it. The selected queue count reports the greatest pending length among the selected troops, so one waypoint assigned to six troops reads as one queued order rather than six.

Troops finish movement waypoints in sequence. A queued focus attack runs when reached; if its target has already died, the troop skips that attack and continues to the next order. A changing crossing preserves the order, waits at a safe approach and replans when docked. An order into invalid water or a stone wall is rejected rather than appearing successful.

## Optional keys

| Input | Action |
| --- | --- |
| A, then destination | Advance and engage enemies on the route |
| A, then Shift+click destination | Queue an advance-and-engage order after the current plan |
| S | Stop and clear queued orders |
| H | Hold position and clear queued movement |
| Ctrl+1…0 | Save selected troops to a numbered group |
| Shift+1…0 | Add selected troops to that numbered group |
| 1…0 | Recall a group; repeat quickly to focus it |
| Ctrl+A | Select the whole army |
| Arrows / middle drag / wheel | Pan / pan / zoom |
| Escape | Cancel a pending action, otherwise pause |

Recruitment buttons and the existing class selection buttons remain available. Shift on a friendly still changes selection; it does not append an attack against that friendly. The basic tutorial continues to teach the ordinary click flow without requiring queued orders.

## Implementation and verification scope

`TablefrontOrderPreview` owns a fixed pool of 105 route/endpoint renderers, shared materials and reusable geometry buffers. Formation slots and route polylines come from the shared simulation helpers, including obstacle routing and bridge waiting approaches. Surface heights follow the actual board and bridge art. The planning component reads orders and does not advance the simulation or change troop orders.

Current legs and hover previews are recomputed each frame. Unchanged future legs use a per-guide bounded geometry cache, invalidated by simulation/unit identity, kind, bridge revision/angle, current endpoint, queue length and every live queued endpoint. Changing targets and new orders therefore update immediately, without a timer. Renderer vertices are submitted in batches; deck height sampling reuses the rendered bridge orientation. See the [Editor CPU probe](../TestResults/maneuver-preview-cost.json) for steady-plan cost and its limits; the Unity managed-allocation counter failed calibration, so its zeros are not allocation evidence.

`AgentScripts/VerifyTacticalControls.cs` passed [49 Editor-only checks](../TestResults/tactical-controls-runtime.txt) using virtual Input System devices. It covers ordinary and Shift clicks, queue-tail previews, additive selection, armed Move/attack-move, Stop/Hold, full plans, dead queued targets, actual formation endpoints, bridge routes/waiting, invalid destinations, hidden states and a bounded renderer pool. The wall-unavailable cross is verified above the actual obstacle top, with flat vertices; water feedback retains its floor height. No native game input is sent by that runner.

Neural weights, training and GPU quality settings are outside this control change. The game does not train opponents during a match. Human comprehension, perceived clarity and native frame-time impact still need playtesting and measurement after the automated and rendered checks.
