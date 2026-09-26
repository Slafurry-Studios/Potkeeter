# AGENTS.md

Unity 2D action game (Slafurry Studios). The **repo root is the Unity project root** — open the
folder itself, never a nested project. Editor version is pinned in
`ProjectSettings/ProjectVersion.txt` (`2022.3.62f3`); the deploy workflow reads that file, so
don't bump it casually.

## Commands

There is no test suite, linter, or formatter. Verification = open the project and press Play on
the `Boot` scene.

```bash
# fast compile check, no Unity needed
dotnet build Assembly-CSharp.csproj        # gameplay code
dotnet build Assembly-CSharp-Editor.csproj # Assets/Editor
```

- `dotnet build Potkeeter.slnx` **fails** with the installed SDK (8.0.407) — `.slnx` needs a
  newer MSBuild. Build the two `.csproj` directly.
- The `.csproj` files are Unity-generated and gitignored. They only exist after Unity has
  imported the project, and a newly added `.cs` won't be in them until Unity re-imports — so a
  clean `dotnet build` does not prove a new file compiles.
- To check a file Unity hasn't imported yet, compile it standalone with Roslyn
  (`dotnet <sdk>/Roslyn/bincore/csc.dll -nostdlib` + `-r:` Unity's `netstandard.dll` and the
  `UnityEngine.*Module.dll` files under `Editor/Data/Managed/UnityEngine/`, plus
  `Library/ScriptAssemblies/UnityEngine.UI.dll` for UGUI types). You have to pull in the
  abstract bases and the `LoadingSystem`/`BootstrapLoader`/`SceneLoader` sources too, since those
  types live in `Assembly-CSharp`. Expect **spurious `CS0649` on every `[SerializeField]`** —
  inspector assignment is invisible to a standalone compile, so only the `-t:Rebuild` baseline
  above is authoritative.
- **An incremental build reports `0 Warning(s)` even when warnings exist** — nothing recompiles,
  so nothing is re-diagnosed. Use `dotnet build Assembly-CSharp.csproj -t:Rebuild` to see them.
  Baseline is exactly three: `CS0649 GameFeel.gameFeelEffects`, `CS0414 GameOver.debug`,
  `CS0414 DialogHUD.typeSFX`. Anything else is yours.

Player build (same args CI uses, via `unity-itchio-deploy.yml`). No Unity editor is on PATH
(`which unity` hits an unrelated `unity` binary) — invoke the editor Unity Hub installed
(`~/Unity/Hub/Editor/2022.3.62f3/Editor/…` here):

```bash
<unity-editor-binary> -batchmode -quit -nographics \
  -projectPath . -buildTarget StandaloneWindows64 \
  -executeMethod BuildScript.Build -logFile -
```

`Assets/Editor/BuildScript.cs` **requires** the `-buildTarget` argument, builds **every enabled
scene in `ProjectSettings/EditorBuildSettings.asset`, in list order**, and writes to
`build/<Target>/`. Missing arg → `Exit(1)`. Supported targets: `StandaloneWindows64`,
`StandaloneOSX`, `StandaloneLinux64`, `WebGL`. A half-authored scene left enabled in Build
Settings fails the whole build — check that file before blaming a code change.

## Runtime architecture

**Boot flow.** `Boot.unity` is scene 0 in Build Settings. The persistent systems are plain
GameObjects *in that scene* (`Loading`, `Audio`, `Pause`, `Localization`, `SceneLoader`,
`InputHub`) plus `BootstrapLoader` on the `===BOOT===` object. `LoadingSystem.Start()` runs the
real init sequence. `BootstrapLoader` is a second, parallel path — don't register a new system in
both. There is no prefab holding these; a new cross-scene system is a new GameObject in
`Boot.unity`.

**Base classes** (`Assets/_Game/00_Scripts/Core/Abstract/`):

| Base | Behaviour |
|---|---|
| `Singleton<T>` | `Awake` is **sealed**; it self-registers with `LoadingSystem.Instance` and calls the abstract `OnSingletonAwake()`. Override the hooks, never `Awake`/`PostInitialize` plumbing away. |
| `GameSystem<T>` | `Singleton` + `DontDestroyOnLoad`. This is what a cross-scene service extends. |
| `LocalSingleton<T>` | Per-scene singleton (does *not* persist). |
| `Manager` | Session coordinator that registers with `GameManager`. **Aspirational** — `GameManager` doesn't exist and the only "Manager" (`ObjectiveManager`) extends `Singleton<ObjectiveManager>` instead. |

**`IInitializable` contract** — the rule the whole codebase depends on:
- `Initialize()` = own setup only, **never touch other objects**.
- `PostInitialize()` = wiring, safe to grab references to other objects.
- `Priority` orders both passes, **smallest runs first**.
- `LoadingSystem` snapshots registrants on its first frame; anything registering later (e.g. a
  player spawned in a later scene) is run as a "late batch" one frame later, so ordering still
  holds within a batch.
- `Initialize()` gets a 10s per-object timeout and its exceptions are swallowed with a log error
  — a silent "works in editor, does nothing" bug usually means an exception in `Initialize()`.

**Scene changes** always go through `SceneSystem.Load(name)` (static wrapper over
`SceneLoader.LoadScene`). Subscribe to `OnBeforeSceneLoad` for the async save/flush gate. Names
are plain strings passed to `LoadSceneAsync` — a wrong name fails silently at runtime. The menu
scripts' `_gameSceneName` / `_settingsSceneName` / `_aboutSceneName` defaults in C# do **not**
match the real scene filenames (`Game`, `Main Menu`, `Settings Menu`, `About Menu`), so trust
the inspector values, never the field default.

**Bridges** (`Core/Bridge/`): `SingletonEventsBridge` on a GameObject discovers `ISubBridge`
components (`AudioBridge`) so gameplay can trigger system behaviour without a direct reference.

**Input**: new Input System only (`activeInputHandler: 2`). Actions live in
`Assets/_Game/05_Settings/Input/Main Input.inputactions`; read input through the `Controls` static
facade in `InputHub.cs` — that file lives in `01_Objects/Prefabs/Input/`, not under `00_Scripts`.
Never legacy `UnityEngine.Input`. `Main Input.cs` is `<auto-generated>` by the Input System code
generator, so edit the `.inputactions` asset, never the wrapper — hand edits are overwritten on
the next regeneration.

**Audio and localization are inspector-wired, not `Resources`-loaded** (there is no `Resources`
folder for either — `Assets/Resources` only holds DOTween settings):
- `AudioSystem` (on the `Audio` GameObject in `Boot.unity`) resolves clips from three serialized
  arrays. Names are string keys matched against each entry's `name`; a miss logs
  `Sound '<name>' tidak ditemukan!` and no-ops. Dropping a file into `Assets/_Game/03_Audio`
  changes nothing until an entry is added to an array.
- `PlayMusic(name)` checks **`musicTracks` first**, then falls back to the legacy `musicSounds`
  array. New music goes in `musicTracks`; `musicSounds` only still holds `"Virtual Insanity"`.
- A `MusicTrack` is an **optional `intro` that plays once, then a `loop` clip**. The handoff
  crossfades, and the loop source forces `loop = true` at `Initialize()` *and* again at play time,
  so a track cannot play once and go silent — which is exactly how `"Virtual Insanity"` behaved
  with `loop: 0`. Registered: `MainTheme` (loop only), `Battlefield`, `FinalBoss`,
  `SpaceshipTheme` (intro + loop each). Clips live in `03_Audio/MUSIC/<Name>/`.
- Switching tracks crossfades the outgoing one out in **both** directions (track→track, and
  track↔legacy `Sound`) via `FadeOutOutgoing`/`StopOutgoing`. Re-triggering the track that is
  already playing is a deliberate no-op, so an intro never restarts mid-way. Music fades use
  `Time.deltaTime`, so they stall while the game is paused.
- **Only the Main Menu has music.** `Main Menu.unity` wires `SceneStartTrigger.onTrigger` to
  `AudioBridge.set_fadeDuration(1)` then `AudioBridge.PlayMusic("MainTheme")`. `Game.unity`,
  `Playground.unity` and both other menu scenes have **zero** `PlayMusic` calls.
- `MusicPlayer.cs` is a code-driven alternative and is **not attached anywhere**.
  `Prefabs/System/Audio/MusicPlayer.prefab` is a naming trap: despite the name its root has only
  a Transform, and the child `AudioBridge` GameObject holds `SingletonEventsBridge` +
  `AudioBridge`. There is no `MusicPlayer` component in it.
- The mixer volume sliders are **dead**: `Master.mixer` has `m_ExposedParameters: []`, so
  `MasterVolume`/`MusicVolume`/`SFXVolume` don't exist. `Update*Volume` no longer NREs — it
  null-guards, still writes `PlayerPrefs`, and warns once — but the Settings sliders do nothing
  until the three parameters are exposed in the mixer inspector.
- SFX still uses `sfxSounds`, where only `"ParrySFX"` is registered, so `ObjectiveManager`'s
  `PlaySFX("Objective")` and `PlaySFX("ObjectiveComplete")` fail. An `AudioClip` in scene/prefab
  YAML is `{fileID: 8300000, guid: <32 hex>, type: 3}`.
- `LocalizationSystem` is scaffolded but **not wired**: no `LocalizationTable` asset exists in the
  repo and the component's `table` field is `{fileID: 0}`, so `GetText` has no null guard and
  `Localize.Text(key)` will NRE. The only caller, `LocalizedText`, is also unusable — its class
  name doesn't match `LocalizeText.cs`, so Unity won't let you attach it, and nothing references
  it. Fix the table and the filename before building on localization.

**Triggers** (`00_Scripts/Game/Triggers/`, all in the **global** namespace like the rest of
`Game/`): `BaseTrigger` supplies the `playLimit` / `unlimited` gate (`CanTrigger`,
`AddTriggerCount`) and the other four extend it — `CountTrigger` (`targetCount` → `onReached`),
`DelayTrigger` (`delay` → `onComplete`), `SceneStartTrigger` (`triggerOnStart` → `onTrigger`).
`ChangeSceneTrigger` is standalone and just calls `SceneSystem.Load`. Behaviour is
inspector-authored through `UnityEvent`s, so wiring lives in the scene YAML, not in code.

**Menu UI helpers** (`00_Scripts/Utils/UI/`, namespace `Slafurry.Utils.UI`): `UIFloat` (sine
`anchoredPosition` drift, optional phase), `ButtonHover` (pointer + selection scale/brightness),
`ButtonClickPunch` (press-squash, release-pop, submit support). Both button scripts animate
`target.localScale` **and** `Graphic.color` and each caches the rest value in `Awake`, so on one
transform they overwrite each other — point one `target` at a child GameObject.
`ButtonClickPunch`'s `overshoot` is documented as scaling the overshoot but actually multiplies
the settle *duration* (`releaseDuration * overshoot`).

**Namespaces** mostly mirror folders (`Slafurry.Core.*`, `Slafurry.System.*`,
`Slafurry.Utils.*`), but all of `Game/`, `Manager/`, `System/Audio`, `System/Health` and most of
`UI/` are in the **global** namespace. Match whatever the file already does; don't mass-migrate.
Folder names contain spaces (`Collide Trigger`, `State Machine`, `Bridges List`) — quote paths.

## Assets are Drive-owned

- `Assets/_Game/02_Art/Sprite` and `Assets/_Game/03_Audio` are synced from Google Drive by
  `retrieve.yml` (manual) and `track.yml` (nightly 23:00 UTC). Don't hand-add, rename, or move
  files there — the retriever keys on Drive file id + relative path and will re-create whatever
  it owns. PRs arrive from the disposable `chore/asset` branch (bot "Utazumi Sakurako").
- `state/*.json` manifests are bot-owned. Never edit by hand.
- The sync downloads **media only, no `.meta`**. After a sync lands, open the project in Unity
  and commit the `.meta` files it generates — otherwise GUIDs churn and prefab/scene references
  silently break. Nothing validates import settings, so a bad one committed once stays bad.

## Unity asset rules (easy to get wrong here)

- **A `.meta` is part of the change.** Commit the `.meta` Unity generates for every new/renamed
  file. Never hand-edit a `guid:` in `.unity`/`.prefab`/`.asset` YAML — a typo silently orphans
  every reference, and a hand-copied GUID collides.
- **Moving or deleting a `.cs` or prefab breaks every YAML reference to it** (scenes *and*
  prefabs both store bare GUIDs). Grep for the `.meta` GUID before you move anything, and prefer
  Unity's `Move`/`Delete` so references are rewritten.
- *Missing (Mono Script)* means the referenced `.cs` isn't in the repo — find it by grepping the
  GUID from the YAML. Repair in the editor by re-assigning the component — never by hand-editing
  GUIDs. In `_Game` the only unresolved script refs are two URP camera-data components in
  `Boot.unity` and `Dev/Boot For Playground.unity`, from a URP package no longer in
  `manifest.json`; they serialize nothing and are harmless. The imported TMP *Examples & Extras*
  adds two more, but only inside its own demo scenes/prefabs.
- **Dangling sprite/font slots are no longer baseline.** The Drive sync could replace art with new
  GUIDs while prefabs kept pointing at the old ones, which used to leave `m_Sprite: {fileID: 0}` on
  the menu/pause `Background`, every button, `Title Text`, the Settings slider
  `Fill`/`Handle`/`Checkmark`, the Dialog `Dialog Box`, and one dead TMP font in `Settings.prefab`.
  All of those were re-assigned in the main-menu work: a `m_Sprite: {fileID: 0}` or
  `m_fontFile: {fileID: 0}` anywhere today is **not** baseline — treat it as a fresh Drive-sync
  regression. `MainMenu.prefab` now points at `Abaddon Bold` (`02_Art/Sprite/Fonts`), and
  `Abaddon Light.asset` was deleted (only its `.ttf` survives).
- **Sprite import settings live in `.meta`, so they are diffable but must be made in the editor.**
  `textureType: 8` = Sprite, `spriteMode: 1` = Single, `2` = Multiple; a Multiple sheet with an
  empty `spriteSheet.sprites` list yields **no usable sprite at all** — it must be sliced before
  it can be dragged into anything. Sheets are sliced by rectangle, so a `-Sheet` filename is not
  evidence it is a sheet: verify `spriteSheet.sprites` and count the `- name:` entries.
  `spritePixelsToUnits` differs per asset (100 for menu art, 256 for the placeholder bayonet), so
  a wrong PPU silently rescales a sprite rather than breaking the reference.
- **A Drive sync will not restore `.meta`.** The retriever pulls media only, so import settings
  (Sprite type, slicing, PPU, filter mode) are safe to commit and will survive the next sync — but
  so will a bad setting, since nothing re-validates them. If a reimport looks wrong, diff the
  `.meta` before assuming the art changed.
- **Music loop clips need their own import settings**, for the same reason sprites do. All the
  `03_Audio/MUSIC/*/*.ogg` ship as `loadType: 0` + `compressionFormat: 1` (Vorbis) with no loop
  flag. Vorbis looping can click or hiccup at the seam, so each `*_loop.ogg` wants **Decompress On
  Load** + the **Loop** box ticked in the inspector. The `.ogg` extension is not evidence a file
  is a loop — check `loadType`/`compressionFormat` and count the `-intro`/`-loop` filename pair.
- **There are two TextMesh Pro resource trees**: `Assets/_Vendor/TextMesh Pro` (fonts, no `.cs`)
  and `Assets/TextMesh Pro` (the full *Examples & Extras* import, 11 MB / 250 files / 131 GUIDs).
  They share **no GUIDs**, so nothing collides, but the 34 `Examples & Extras` scripts really do
  compile into `Assembly-CSharp` (37 csproj entries) — benchmarks, vertex shake and other unused
  demo code. It is inert but shipped; the first candidate if the repo needs slimming.
- Wiring is inspector-authored, not code-authored: new UI screens are their own scenes under
  `04_Scenes/` and must be added to `ProjectSettings/EditorBuildSettings.asset` to ship.
- `README.md`'s ARCHITECTURE section is wrong (paths are under `Assets/_Game/`, and the
  `ARCHITECTURE.md` it links to does not exist). Trust this file over the README.

## Git

- Tracked despite looking generated: `Packages/manifest.json`, `Packages/packages-lock.json`,
  `Potkeeter.slnx`, all of `ProjectSettings/`.
- `Potkeeter.slnx` lists all 44 generated csproj files, **and all but the two
  `Assembly-CSharp*` ones are gitignored** — so a local "trim the solution so `dotnet build
  Potkeeter.slnx` works" edit deletes 42 entries and quietly kills IDE completion for TMP, the
  Input System and Cinemachine. Build the two `.csproj` directly instead of slimming this file.
- Ignored: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `build/`.
- Commit style: `feat(scope):`, `fix:`, `chore(assets):`, `ci(retrieve):`, `sync:`; bot asset
  commits end with `[skip ci]`.
- Feature work goes on `feature/*` / `fix/*` / `ci/*` branches, PR into `main`. Releases are
  manual `workflow_dispatch` on the itch.io deploy workflow (choose platform, optional
  `release_tag` to also cut a GitHub Release). Nothing builds on push to `main`.

