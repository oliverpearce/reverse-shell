# Combat (Reverse: 1999-style)

## Quick start
1. **Tools > Combat > Generate Sample Content**. This creates the demo assets in `Content/` and the scene `Scenes/Battle.unity`.
2. Open `Scenes/Battle.unity` (or use **Tools > Combat > Open Battle Scene**) and press **Play**.

**Controls**
- Click a card to queue it (1 AP).
- Drag a card to another slot to move it (1 AP).
- Click an enemy to make it the target.
- Press **GO** to resolve the round. **UNDO** reverts your last action.

## Rules
- Each round the hand fills to 7 rank-I incantations, drawn from the party.
- Two adjacent copies of the same card at the same rank merge into the next rank (max III), and each merge gives the owner +1 Moxie.
- Playing an incantation gives the owner +1 Moxie.
- At 5 Moxie the owner's **Ritual** card appears in the hand. Queuing it spends all their Moxie.
- After your cards resolve, enemies use the intent they showed at the start of the round. Then status durations tick down.
- **Afflatus:** Beast > Plant > Star > Mineral > Beast, and Spirit and Intellect beat each other. Advantage gives +30% damage and +15% crit (set in `CombatConfig`).

## Making content (no code)
| Want | Do |
|---|---|
| New card | **Create > Combat > Card**. For each rank, click *Choose type* on an effect slot and pick an effect (Damage, Heal, Apply Status, Gain Moxie, Modify AP). |
| New character | **Create > Combat > Character**. Give it 2 incantations and a ritual, then add it to a **Battle Setup**'s party. |
| New enemy | **Create > Combat > Enemy**. Its skills are weighted cards that the AI picks from. |
| Buff / debuff | **Create > Combat > Status**: stat modifiers per stack, DoT/HoT, stun (`preventsAction`). |
| Card animation | **Create > Combat > Cast Sequence**. Chain Lunge / Flash / Pulse / Projectile / ScreenShake / Wait / SpawnVfx steps. |

## Extending (code)
- **New mechanic:** subclass `CardEffect` (`Scripts/Effects`) and mark it `[Serializable]`. It appears in the dropdown automatically. Change state only through `ctx.Battle` (`BattleController`) so the change is recorded as an event and gets animated.
- **New animation step:** subclass `CastStep` (`Scripts/View/CastSteps.cs`).
- **RPG layer:** build a `BattleSetup` at runtime, assign it to `BattleBootstrap.setup`, call `Begin()`, and listen to `Controller.BattleEnded`. `BattleController` doesn't depend on the UI, so you can also drive it headless.

## Layout
- `Scripts/Core`, `Data`, `Effects`, `Battle`: rules and data, with no UI.
- `Scripts/View`: uGUI placeholder presentation. It replays `BattleEvent`s.
- `Editor`: the type-picker drawer and the sample generator.
- `Tests/EditMode`: run them from **Window > General > Test Runner**.
