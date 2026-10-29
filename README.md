# RoadSafety

Road-safety awareness game for the **ONSR** (Observatoire National de la Sécurité Routière).
ISAMM, L3 Développement des jeux vidéo, *Atelier Intégration IA dans les jeux vidéo*, Équipe 01.

**Team:** Ranim Chabbah · Fakhreddine Chammakhi

## Getting started

1. Install **Unity 6000.0.59f2** (Unity 6 LTS) from Unity Hub. Use this exact version so the project doesn't upgrade on one computer and break on the other.
2. Clone the repo, then in Unity Hub: **Add → Add project from disk** → pick the `RoadSafety` folder.
3. Open `Assets/_Project/Scenes/Main.unity` and press **Play**.

The first open takes a few minutes while Unity rebuilds the `Library` folder (it is not in Git).

## Project layout

```
Assets/
  _Project/            <- all our own work goes here
    Scenes/Main.unity
    Scripts/
      Core/GameManager.cs         game state: Health, Stamina, EnemyNumber
      Core/ProgressionManager.cs  stars, hearts, good actions / mistakes of the level
      Player/PlayerActions.cs     player behaviour tree (Selector)
      Player/DriverControls.cs    every action of the child (Go, Stop, steer, seatbelt, horn)
      AI/BehaviorTree.cs          Node, Selector, Sequence, Condition, ActionNode
      UI/                         HUD (built in code), touch buttons, Arabic text, sounds
      Editor/                     menu RoadSafety > UI > Create HUD prefab
    UI/Resources/HUD/             HUD sprites      UI/Resources/Fonts/  Almarai (Arabic, OFL)
    Materials/  Prefabs/  Models/  Audio/
  Settings/            <- URP settings from the template (leave as is)
```

## Scene layout (Main.unity)

| Object | What it is |
|---|---|
| `Main Camera` | Chase camera behind the player car |
| `Lighting` | Directional Light + Global Volume (URP post-processing) |
| `_Managers/GameManager` | The `GameManager` component |
| `Environment` | Ground, 2-lane road, sidewalks, lane dashes, crosswalk with stop line, `TrafficLight_01` placeholder |
| `Player/PlayerCar` | Placeholder car (blue) with `PlayerActions` |
| `Traffic` | `NPC_Car_01` placeholder (red), `SpawnPoints`, `Waypoints` (empty, for traffic AI) |
| `Pedestrians` | Spawn points on both sides of the crosswalk |
| `UI` | Empty in the scene: the HUD is added here at Play (see below) |

The road runs along the Z axis; the player drives north (+Z) in the right lane.

## HUD and driver controls (Day 1)

Press **Play** in `Main.unity`: `GameplayBootstrap` adds `_Managers/ProgressionManager`,
`_Managers/DriverControls` and `UI/HUD` by itself, so **nobody has to edit the scene for the HUD**.

| Story | On screen | Code |
|---|---|---|
| US 1.1 | Green **Go** (hold to drive) and red **Stop** under it | `DriverControls.Throttle`, `Braking` |
| US 1.2 | Big left / right arrows, steering ramps up while held | `DriverControls.Steer` (-1..1) |
| US 1.3 | Seatbelt button + avatar with / without belt. Go without the belt: reminder bubble + voice, the car can't start | `SeatbeltOn`, `CanDrive`, `SeatbeltReminder` |
| US 1.4 | Yellow horn button, short friendly "tuut-tuut" | `DriverControls.Honked` |
| US 3.1 | Star counter: a star pops, flies into the counter, joyful sound | `ProgressionManager.AddStar` |
| US 3.4 | 3 hearts; at 0 a calm "لا بأس! لنحاول مرة أخرى" screen, then the level restarts | `ProgressionManager.LoseHeart` |

**For the car / world scripts** (read the controls, report behaviours, never touch the HUD):

```csharp
using RoadSafety.Core;
using RoadSafety.Player;

// car controller, in FixedUpdate
DriverControls input = DriverControls.Instance;
float throttle = input.Throttle;   // 0..1, stays 0 until the seatbelt is on
bool  braking  = input.Braking;
float steer    = input.Steer;      // -1 left .. +1 right

// rules (traffic light, crosswalk, collisions...)
ProgressionManager.Instance.AddStar(GoodAction.StoppedAtRedLight);
ProgressionManager.Instance.RemoveStar(Mistake.Collision);
ProgressionManager.Instance.LoseHeart(Mistake.RanRedLight);
```

The instances are created when the scene loads, so read them in `Start`/`Update`, not `Awake`.

**Test keys (Game view):** arrows / WASD drive, Space brake, **B** seatbelt, **K** horn,
**1** / **2** add / remove a star, **3** lose a heart. The current values are shown under the behaviour-tree box.

**Arabic text:** Unity draws Arabic letters unconnected and left-to-right, so always pass texts
through `ArabicText.Fix("...")` and use the Almarai font (`Resources/Fonts`). Texts are Modern Standard Arabic.

**Sounds:** the HUD makes small cartoon sounds in code. To replace one, drag a clip onto the
`HUDController` component. The seatbelt voice line ("ضع حزام الأمان") still has to be recorded:
save it as `Assets/_Project/Audio/Resources/Audio/VO_Seatbelt_ar.wav` and it is used automatically.

**Changing the layout:** edit `HUDBuilder.cs`, or use **RoadSafety > UI > Create HUD prefab**,
put the prefab under `UI` in the scene and move things by hand (the prefab is then used instead).

## Behaviour tree

`PlayerActions` builds a **Selector** that reads the variables kept by `GameManager`.
The first branch that succeeds wins:

```
Selector
├─ Sequence: Health low?   → Repair
├─ Sequence: Stamina low?  → Rest
├─ Sequence: Enemies near? → Avoid
└─ Drive
```

The actions are **stubs**: they log the decision and change the values a little so you can test the tree.
In Play mode, change Health / Stamina / Enemy Number on `GameManager` in the Inspector, or use
**H** (damage), **E** / **R** (add / remove an enemy) in the Game view. The current action is shown at the top left.

## Working together with Git

- `main` must always open and run. Work on a branch: `feature/<your-name>-<thing>`, e.g. `feature/ranim-hud`.
- **Pull before you start, commit small, push often.**
- **Don't edit `Main.unity` at the same time.** Unity scenes merge badly. Tell each other who's in the scene, or build your part as a **prefab** and drop it in at the end.
- Always commit the `.meta` file together with its asset.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` (already in `.gitignore`).

### Suggested split (change it to what you agree on)

| Area | Owner |
|---|---|
| HUD, driver controls, seatbelt, horn, collision feedback, stars, badges, end screen, hearts | Ranim |
| Car controller, traffic lights, pedestrians (behaviour tree), anticipation event | Fakhreddine |
| Environment / level art | both |
