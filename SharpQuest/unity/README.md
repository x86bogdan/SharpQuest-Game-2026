# Files sq init puts in your Unity project

MANAGED FOLDER: `sq update` replaces it. `sq init` copies these into your Unity project:

| From here | Into your project | |
|---|---|---|
| `Core/Game.Core.asmdef` | `Assets/Game/Core/` | Makes your rules their own assembly, **with no access to UnityEngine** (`noEngineReferences`). Created if missing, never overwritten |
| `Core/csc.rsp` | `Assets/Game/Core/` | Turns on nullable reference types in Unity, as in the labs. Created if missing |
| `Core/SharpQuest/IsExternalInit.cs` | `Assets/Game/Core/SharpQuest/` | One line that lets records compile in Unity. **Always refreshed**: don't edit it |
| `Unity/Game.Unity.asmdef` | `Assets/Game/Unity/` | Your MonoBehaviours, which may use both UnityEngine and Game.Core. Created if missing |
| `Tests/Core/Game.Core.Tests.asmdef`, `csc.rsp` | `Assets/Game/Tests/Core/` | Your tests, an EditMode test assembly. Created if missing |
| `Content/README.md` | `Assets/Game/Content/` | Where your JSON content goes. Created if missing |
