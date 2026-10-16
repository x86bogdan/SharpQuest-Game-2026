# Wiring

The course's tests need to reach *your* types without knowing their names. You tell them, with one small
class per session in this folder, derived from that session's base class and naming your own type:

| Session | Derive from | Naming |
|---|---|---|
| 2 | `ContentWiring<TItem>` | your content's item type (`Enemy`, `Tower`, `Room`) |
| 4 | `KindsWiring<TBase>` | your abstract base |
| 6 | `EventWiring<TPublisher>` and `FunctionWiring<TItem>` | the class with your event; the items your function filters |

Each base class's comments (in `../contracts/SharpQuest.Game.Contracts/SNN/`) say exactly what the tests expect.
One class per base, a public constructor with no parameters, and every member one line that calls Game.Core.

This folder is yours: `sq update` never touches it. It is not part of your game, and Unity never sees it.
