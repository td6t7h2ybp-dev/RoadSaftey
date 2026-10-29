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
      Core/GameManager.cs       game state: Health, Stamina, EnemyNumber
      Player/PlayerActions.cs   player behaviour tree (Selector)
      AI/BehaviorTree.cs        Node, Selector, Sequence, Condition, ActionNode
    Materials/  Prefabs/  Models/  Audio/  UI/
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
| `UI` | Empty, for the HUD / menus |

The road runs along the Z axis; the player drives north (+Z) in the right lane.

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
| GameManager, rules (infractions, score), UI/HUD | |
| PlayerActions / behaviour tree, car controller | |
| Traffic AI (NPC cars, traffic lights), pedestrians | |
| Environment / level art | |
