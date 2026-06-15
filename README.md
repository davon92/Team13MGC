# Team13MGC

Unity/C# rhythm and visual novel prototype built as part of Microsoft Game Camp 2025 Team 13.

This repository is useful as portfolio evidence because it contains a real Unity project with gameplay timing, chart parsing, scoring, scene flow, save slots, menu systems, input glyph support, and visual-novel integration. The project combines rhythm gameplay with narrative flow and frontend UI screens rather than being a single isolated mechanic demo.

## Portfolio Focus

This project shows Unity gameplay systems and production workflow skills:

- sample-accurate rhythm timing and lead-in handling
- chart loading from Koreographer tracks
- button-lane and knob-lane gameplay controllers
- normalized score/combo/judgement tracking
- scene routing between frontend, rhythm gameplay, results, and VN scenes
- save slots and lightweight persistent player state
- input glyph switching for keyboard/mouse and controllers
- menu, gallery, settings, credits, and result screen systems
- Yarn Spinner visual novel bootstrap and return flow

## Current Project Shape

| Area | Status | Evidence |
|---|---:|---|
| Rhythm timing | Implemented | `RhythmConductor` tracks sample time, lead-in, offsets, pause/resume, and song-end detection. |
| Chart parsing | Implemented | `KoreographyChartLoader` parses Koreographer button and knob tracks into runtime note/span data. |
| Button lane | Implemented | `ButtonLaneController` handles spawning, movement, judgement windows, input actions, and note recycling. |
| Knob lane | Implemented/In progress | `KnobLaneController` supports mouse/gamepad input, trace scoring, lock-on/magnet assist, and debug readouts. |
| Scoring | Implemented | `ScoreTracker` normalizes points toward a target score and tracks combo, counts, and result data. |
| Scene flow | Implemented | `SceneFlow` routes song select, rhythm gameplay, result screens, and VN return paths. |
| Saves | Implemented | `SaveSystem` manages autosave/manual slots and lightweight unlock/state persistence. |
| VN integration | Implemented/In progress | `VNBootstrap` resolves Yarn start nodes, load slots, and rhythm-return nodes. |
| UI/frontend | Implemented/In progress | screen controllers, settings, gallery, credits, song select, result screens, and glyph hints are present. |

## Key Systems

### Rhythm Timing

`RhythmConductor` is the clock source for rhythm gameplay. It exposes separate visual and hit timing values, supports millisecond input/visual offsets, handles preroll/lead-in, and freezes time when the song ends so gameplay objects do not restart or drift.

Important file:

- `Assets/Scripts/RhythmConductor.cs`

### Chart Loading

`KoreographyChartLoader` reads Koreographer tracks and converts them into runtime button notes and knob spans. Button events support tap/hold timing, while knob spans generate target sampling functions from curve or float payloads.

Important file:

- `Assets/Scripts/KoreographyChartLoader.cs`

### Button and Knob Lanes

The rhythm gameplay is split into lane controllers:

- `ButtonLaneController` handles face-button notes, hit windows, note spawning, visual movement, judgement, and recycling.
- `KnobLaneController` handles Sound Voltex-style knob tracing with mouse/gamepad input, grace windows, trace scoring, magnet/lock-on assist, and debug telemetry.

Important files:

- `Assets/Scripts/ButtonLaneController.cs`
- `Assets/Scripts/KnobLaneController.cs`
- `Assets/Scripts/NoteObject.cs`
- `Assets/Scripts/KnobPathRenderer.cs`

### Score and Results

`ScoreTracker` receives judgement events, accumulates raw units, normalizes scoring so perfect play can reach the target max score, tracks combo/counts, and builds a result payload for scene transitions.

Important files:

- `Assets/Scripts/ScoreTracker.cs`
- `Assets/Scripts/ResultsScreen.cs`
- `Assets/Scripts/ScoreOdometer.cs`

### Scene and Save Flow

`SceneFlow` acts as the central router between frontend screens, rhythm gameplay, results, and VN playback. `SaveSystem` supports autosave/manual slots and lightweight persistent flags.

Important files:

- `Assets/Scripts/SceneFlow.cs`
- `Assets/Scripts/SaveSystem.cs`
- `Assets/Scripts/SaveSlotsScreen.cs`
- `Assets/Scripts/SaveSlotView.cs`

### Visual Novel Integration

`VNBootstrap` starts Yarn conversations from the right node based on explicit overrides, rhythm returns, queued save slots, or fallback chapter data.

Important files:

- `Assets/Scripts/VNBootstrap.cs`
- `Assets/Scripts/VNYarnCommands.cs`
- `Assets/Scripts/VNReturnInjector.cs`

## Scenes

| Scene | Purpose |
|---|---|
| `Assets/Scenes/BootstrapScene.unity` | Startup/bootstrap scene. |
| `Assets/Scenes/FrontEndScene.unity` | Main frontend/menu flow. |
| `Assets/Scenes/RhythmGamePlayScene.unity` | Rhythm gameplay scene. |
| `Assets/Scenes/ResultsScene.unity` | Post-song results scene. |
| `Assets/Scenes/VNGamePlayScene.unity` | Visual novel gameplay scene. |
| `Assets/Scenes/TestScene.unity` | Testing/prototype scene. |

## Tech Stack

- Unity
- C#
- Universal Render Pipeline
- Unity Input System
- Koreographer
- Yarn Spinner / YarnGraph
- DOTween
- TextMesh Pro

## Running The Project

1. Clone the repository.
2. Open the folder in Unity.
3. Let Unity restore packages and import assets.
4. Open `Assets/Scenes/BootstrapScene.unity` or `Assets/Scenes/FrontEndScene.unity`.
5. Use the frontend flow to reach song select, rhythm gameplay, results, and VN scenes.

Note: this is a Game Camp/team prototype repository, so package restore/import time may be longer than a small solo sample project.

## Portfolio Roadmap

To make this repository stronger for recruiters and hiring teams:

- Add screenshots or a short gameplay clip.
- Add a short "My Contributions" section once contribution boundaries are documented.
- Add a system diagram for frontend -> rhythm -> results -> VN return flow.
- Add a short note for known prototype limitations.
- Remove private IDE metadata from version control.

## About Me

I am Davon Allen, a gameplay systems engineer focused on Unity, Unreal, UEFN/Verse, production tooling, and data-driven gameplay workflows.

- Portfolio: https://www.davonallen.com/
- LinkedIn: https://www.linkedin.com/in/davonaallen/
- GitHub: https://github.com/davon92
