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
`StandaloneOSX`, `StandaloneLinux64`, `WebGL`. All 6 scenes are currently enabled, in this order:
`Boot`, `Main Menu`, `Game`, `Playground`, `About Menu`, `Settings Menu`. A half-authored scene
left enabled in Build Settings fails the whole build — check that file before blaming a code change.

CI (`unity-itchio-deploy.yml`) activates a **Personal** license via `buildalon/activate-unity-license@v2`,
and the `windows-latest` runner is flaky at it — a job can die before building with
`Unable to retrieve boot drive serial number` / `No licenses were found` (editor exit `3762504530`)
while the macOS and WebGL jobs in the same run succeed. That is runner infrastructure, not your
diff: re-run the failed job before investigating the code.

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
  `Sound '<name>' tidak ditemukan! (daftar sound belum diisi)` and no-ops. Dropping a file into
  `Assets/_Game/03_Audio` changes nothing until an entry is added to an array.
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
- **Only the Main Menu actually plays music.** `Main Menu.unity` wires
  `SceneStartTrigger.onTrigger` to `AudioBridge.set_fadeDuration(1)` then
  `AudioBridge.PlayMusic("MainTheme")`. `Game.unity`, `Playground.unity` and both other menu
  scenes have **zero** `PlayMusic` calls. `Game.unity` does call
  `AudioBridge.StopMusicWithFade("Virtual Insanity")`, which is a *stop*, not a play — and since
  nothing ever plays that legacy track, that call is currently a no-op. Grep for
  `m_MethodName:` on the `AudioBridge` type, not just the string `PlayMusic`, when auditing.
- `MusicPlayer.cs` is a code-driven alternative and is **not attached anywhere** — its GUID is
  never a `m_Script` in any scene or prefab. `Prefabs/System/Audio/MusicPlayer.prefab` is a naming
  trap: its root has only a Transform, and the child `AudioBridge` GameObject holds
  `SingletonEventsBridge` + `AudioBridge`. That prefab *is* instantiated in `Main Menu.unity` and
  `Game.unity` via a `m_Modifications` override that renames it, so the child is where the real
  music calls live — the name matches the object, not a component.
- The mixer **does** expose `MasterVolume`, `MusicVolume`, `SFXVolume` (`Master.mixer` in
  `Boot.unity`), so the Settings sliders are live. `Update*Volume` still null-guards the
  `AudioMixerGroup` lookup and still writes `PlayerPrefs`, so renaming or un-exposing a parameter
  degrades to a warning instead of an NRE — it does not throw, which makes it easy to miss.
- `SettingsMenu.Start()` registers the slider listeners **before** assigning `.value`, on purpose:
  assigning `.value` fires the listener, so the restored value reaches both the slider and the
  mixer. Don't "optimise" it back to `SetValueWithoutNotify` — that moves the slider only and the
  mixer silently stays at its own default until the player drags something.
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

`SpriteAnimator` (frames, `fps`, `loop`, `pingPong`, `playOnEnable`, `useUnscaledTime`,
`restoreSpriteOnStop`) drives a UGUI `Image`; wired on `Loading.prefab` only. Two traps if you
touch it: the wait field is typed `object` because `WaitForSeconds` and `WaitForSecondsRealtime`
share no base narrower than `System.Object`, and `OnDisable` stops the coroutine *without* firing
`onFinished` (disable isn't completion, and Unity only auto-stops coroutines on GameObject
deactivation, not component deactivation).

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
- Hand-added art goes in `Assets/_Game/02_Art/Debug/`, which is deliberately *outside* the two
  Drive-owned folders, so the retriever can't reclaim it. That's where `Circle.png` / `FillBox.png`
  live. (Note `FillBox.png` is a 1×1 PNG — fine as a `Filled` background template, wrong as a bar.)
- The sync downloads **media only, no `.meta`**. After a sync lands, open the project in Unity
  and commit the `.meta` files it generates — otherwise GUIDs churn and prefab/scene references
  silently break. Nothing validates import settings, so a bad one committed once stays bad.

## Unity asset rules (easy to get wrong here)

- **A `.meta` is part of the change.** Commit the `.meta` Unity generates for every new/renamed
  file. Never hand-edit a `guid:` in `.unity`/`.prefab`/`.asset` YAML — a typo silently orphans
  every reference, and a hand-copied GUID collides.
- **Never hand-write a `.meta` for a file Unity has already imported.** This is the trap that
  actually bites: Unity generated `.meta` with GUID `X`, the component was attached to a prefab
  pointing at `X`, then a hand-written `.meta` with a *different* GUID got committed and clobbered
  it. Every reference still says `X`, which now resolves to nothing — the component silently
  becomes *Missing (Mono Script)* with all its serialized fields dropped, and the build still
  passes. If you must fix a GUID, make the committed `.meta` match what the YAML already
  references. Check with a GUID sweep that includes `Library/PackageCache` (packages hold real
  scripts; a sweep over `Assets/**/*.meta` alone reports ~26 false positives here):
  ```bash
  # expect exactly 2: the URP camera-data components in Boot.unity + Boot For Playground.unity
  python3 -c "
  import re,glob,os
  h=set()
  for p in ('Assets/**/*.meta','Library/PackageCache/**/*.meta','Packages/**/*.meta'):
   for m in glob.glob(p,recursive=True):
    g=re.search(r'guid: ([0-9a-f]{32})',open(m,encoding='utf-8-sig',errors='ignore').read())
    h.add(g.group(1)) if g else None
  s={g for f in glob.glob('Assets/_Game/**/*.unity',recursive=True)+glob.glob('Assets/_Game/**/*.prefab',recursive=True)
   for g in re.findall(r'm_Script: \{fileID: \d+, guid: ([0-9a-f]{32})',open(f,encoding='utf-8-sig',errors='ignore').read())}
  print('unresolved:',sorted(s-h))"
  ```
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
  All of those were re-assigned in the main-menu work: inside `_Game`, a `m_Sprite: {fileID: 0}` or
  `m_fontFile: {fileID: 0}` is **not** baseline — treat it as a fresh Drive-sync regression.
  (Scope the check to `_Game`: six `m_Sprite: {fileID: 0}` legitimately survive inside TMP's own
  `Examples & Extras` demo scenes.) `MainMenu.prefab` now points at `Abaddon Bold`
  (`02_Art/Sprite/Fonts`), and `Abaddon Light.asset` was deleted (only its `.ttf` survives).
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
- **Music loop import settings are a red herring.** All 7 `03_Audio/MUSIC/*/*.ogg` ship with
  identical `loadType: 0` + `compressionFormat: 1` (Vorbis) and **no `loop` field at all** in their
  `.meta`. Ticking Loop in the inspector would not change playback: `AudioSystem` passes
  `loop: true` into `CreateSource` for every `MusicTrack.loop` clip, and `AudioSource.loop` is what
  actually governs repetition. The `-intro`/`-loop` filename pair is the only real signal for which
  clip is the looping one — don't infer it from the extension or from import settings.
- **There are two TextMesh Pro resource trees**: `Assets/_Vendor/TextMesh Pro` (fonts, no `.cs`)
  and `Assets/TextMesh Pro` (the full *Examples & Extras* import, 11 MB / 250 files / 131 GUIDs).
  They share **no GUIDs**, so nothing collides, but the 34 `Examples & Extras` scripts really do
  compile into `Assembly-CSharp` — benchmarks, vertex shake and other unused demo code. It is inert
  but shipped; the first candidate if the repo needs slimming.
- Wiring is inspector-authored, not code-authored: new UI screens are their own scenes under
  `04_Scenes/` and must be added to `ProjectSettings/EditorBuildSettings.asset` to ship.
- `README.md`'s ARCHITECTURE section is wrong (paths are under `Assets/_Game/`, and the
  `ARCHITECTURE.md` it links to does not exist). Trust this file over the README.

## Git

- Tracked despite looking generated: `Packages/manifest.json`, `Packages/packages-lock.json`,
  `Potkeeter.slnx`, all of `ProjectSettings/`.
- `Potkeeter.slnx` is **tracked** and currently lists only the two `Assembly-CSharp*` projects —
  it was trimmed on purpose in the loading-screen PR, so its IDE project list is much smaller than
  the ~44 Unity generates. `dotnet build` still works off the `.csproj` files directly, so this is
  only an IntelliSense-completeness issue. The other generated `.csproj` files are gitignored
  (`*.csproj`, `*.sln` in `.gitignore`; note `.slnx` is *not* ignored). Expect Unity to regenerate
  the full list locally and show that as a diff — don't "fix" it by committing the regenerated file
  unless asked.
- Ignored: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `build/`.
- Commit style: `feat(scope):`, `fix:`, `chore(assets):`, `ci(retrieve):`, `sync:`; bot asset
  commits end with `[skip ci]`.
- Feature work goes on `feature/*` / `fix/*` / `ci/*` branches, PR into `main`. Releases are
  manual `workflow_dispatch` on the itch.io deploy workflow (choose platform, optional
  `release_tag` to also cut a GitHub Release). Nothing builds on push to `main`.

