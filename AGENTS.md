# AGENTS.md

Unity 2D action game (Slafurry Studios). The **repo root is the Unity project root** — open the
folder itself, never a nested project. Editor version is pinned in
`ProjectSettings/ProjectVersion.txt` (`2022.3.62f3`); the deploy workflow reads that file
(`version-file:`), so don't bump it casually.

## Commands

No test suite, linter, or formatter. Verification = open the project and press Play on `Boot`.

```bash
dotnet build Assembly-CSharp.csproj        # gameplay code
dotnet build Assembly-CSharp-Editor.csproj # Assets/Editor
```

- `dotnet build Potkeeter.slnx` **fails** on the installed SDK (8.0.407) — `.slnx` needs a newer
  MSBuild. Build the two `.csproj` directly.
- The `.csproj` files are Unity-generated and gitignored. They exist only after Unity has imported
  the project, and a newly added `.cs` won't be in them until Unity re-imports — **a clean
  `dotnet build` does not prove a new file compiles.** To check one, compile it standalone with
  Roslyn (`dotnet <sdk>/Roslyn/bincore/csc.dll -nostdlib` + `-r:` Unity's `netstandard.dll` and the
  `UnityEngine.*Module.dll` files under `Editor/Data/Managed/UnityEngine/`, plus
  `Library/ScriptAssemblies/UnityEngine.UI.dll` for UGUI). Pull in the abstract bases and the
  `LoadingSystem`/`BootstrapLoader`/`SceneLoader` sources too, since those types live in
  `Assembly-CSharp`. Expect **spurious `CS0649` on every `[SerializeField]`** — inspector
  assignment is invisible to a standalone compile.
- **An incremental build reports `0 Warning(s)` even when warnings exist** — nothing recompiles,
  so nothing is re-diagnosed. Use `dotnet build Assembly-CSharp.csproj -t:Rebuild` to see them.
  Baseline is exactly three: `CS0649 GameFeel.gameFeelEffects`, `CS0414 GameOver.debug`,
  `CS0414 DialogHUD.typeSFX`. Anything else is yours.

Player build (same args CI uses). No Unity editor is on PATH (`which unity` hits an unrelated
`unity` binary) — invoke the Hub install (`~/Unity/Hub/Editor/2022.3.62f3/Editor/…` here):

```bash
<unity-editor-binary> -batchmode -quit -nographics \
  -projectPath . -buildTarget StandaloneWindows64 \
  -executeMethod BuildScript.Build -logFile -
```

`Assets/Editor/BuildScript.cs` **requires** `-buildTarget` (missing → `Exit(1)`), builds **every
enabled scene in `ProjectSettings/EditorBuildSettings.asset`, in list order**, and writes to
`build/<Target>/`. Supported: `StandaloneWindows64`, `StandaloneOSX`, `StandaloneLinux64`,
`WebGL`. All 6 scenes are enabled, in this order: `Boot`, `Main Menu`, `Game`, `Playground`,
`About Menu`, `Settings Menu`. A half-authored scene left enabled fails the whole build — check
that file before blaming a code change.

CI (`unity-itchio-deploy.yml`) activates a **Personal** license via
`buildalon/activate-unity-license@v2`, and the `windows-latest` runner is flaky at it — a job can
die before building with `Unable to retrieve boot drive serial number` / `No licenses were found`
(editor exit `3762504530`) while the macOS and WebGL jobs in the same run succeed. That is runner
infrastructure, not your diff: re-run the failed job before investigating the code.

## Runtime architecture

**Boot flow.** `Boot.unity` is scene 0. The persistent systems are plain GameObjects *in that
scene* (`Loading`, `Audio`, `Pause`, `Localization`, `SceneLoader`, `InputHub`) plus
`BootstrapLoader` on the `===BOOT===` object. There is no prefab holding these; a new cross-scene
system is a new GameObject in `Boot.unity`.

**Two init paths run in parallel, and that's not benign.** `LoadingSystem.Start()` runs the real
`LoadSequence` (one object per frame). `BootstrapLoader` separately walks its own
`systemsToWaitFor` list and calls `lifecycle.Initialize()` + `PostInitialize()` on each **in a
single frame**, then loads the target scene itself. All six systems are `GameSystem<T>`, so they
self-register *and* appear in that list — every one is initialized **twice per boot**, concurrently.
Worse, `BootstrapLoader` does not go through `LoadingSystem` at all, so it **bypasses the 10s
timeout and the exception guard**: an exception there aborts the routine and the target scene is
never loaded. It's also the only thing that loads `Main Menu`, so don't remove it while trying to
de-duplicate.

**Base classes** (`Assets/_Game/00_Scripts/Core/Abstract/`):

| Base | Behaviour |
|---|---|
| `Singleton<T>` | `Awake` is `private` and not virtual (treated as sealed); it self-registers with `LoadingSystem.Instance` and calls abstract `OnSingletonAwake()`. Also declares `OnSingletonDestroyed()` (virtual, undocumented — prefer it over writing teardown). Override the hooks, never `Awake`. |
| `GameSystem<T>` | `Singleton` + `DontDestroyOnLoad`. This is what a cross-scene service extends. |
| `LocalSingleton<T>` | Per-scene singleton, does *not* persist. Used once (`ScreenFlash`). |
| `Manager` | Session coordinator that registers with `GameManager`. **Aspirational** — `GameManager` doesn't exist, nothing extends `Manager`, and `ObjectiveManager` extends `Singleton<ObjectiveManager>` instead. `StoryManager` is a `GameSystem`, not a `Manager`. |

**`IInitializable` contract** — the rule the codebase depends on:
- `Initialize()` = own setup only, **never touch other objects**.
- `PostInitialize()` = wiring, safe to grab references to other objects.
- `Priority` orders both passes, **smallest runs first**. Only `AudioSystem` ever sets it (`0`).
- `LoadingSystem` snapshots registrants on its first frame; anything registering later (e.g. a
  player spawned in a later scene) runs as a "late batch" one frame later, so ordering still holds
  within a batch.
- `Initialize()` gets a 10s per-object timeout and its exceptions are swallowed with a log error —
  a silent "works in editor, does nothing" bug usually means an exception in `Initialize()`.
- Two holes in that guard: `SafeInit` calls `obj.Initialize()` *outside* the `try`, so a
  non-iterator `Initialize()` that throws synchronously kills `LoadSequence` outright (latent
  today — every in-repo `Initialize()` is an iterator); and the timeout sums only **scaled**
  `Time.deltaTime` per `MoveNext`, so one long nested wait under-counts badly.

**Scene changes** go through `SceneSystem.Load(name)` (static wrapper over
`SceneLoader.LoadScene`). `OnBeforeSceneLoad` is `event Action<string, Action>` and is an **async
gate**: the loader spins `while (!ready)`, so a subscriber that never invokes its callback **hangs
the load forever** (sole subscriber: `LoadingScreenUI`). Re-entrant loads are dropped with a
warning. Note `MainMenu.cs` still uses `SceneManager.LoadScene` directly for About/Settings,
bypassing the gate.

**Scene names are a live bug, not just a style nit.** Names are plain strings into
`LoadSceneAsync`, so a wrong one fails *silently*. Real names have spaces: `Game`, `Main Menu`,
`Settings Menu`, `About Menu`. The C# defaults don't match — `MainMenu.cs` uses `GameScene` /
`SettingsScene` / `AboutScene`, and `AboutMenu.cs` / `SettingsMenu.cs` default to `MainMenu`
(real: `Main Menu`). The committed `MainMenu.prefab` still serializes `_gameSceneName: GameScene`
and `GameOver.cs:41` hardcodes `SceneSystem.Load("MainMenu")` — both point at scenes that don't
exist, so **don't "trust the inspector value", verify it against `EditorBuildSettings.asset`**.

**Bridges** (`Core/Bridge/`): `SingletonEventsBridge` does `GetComponents<ISubBridge>()` in `Awake`
into a type→instance map, exposed as `GetBridge<T>()`, so gameplay can reach system behaviour
without a direct reference. Despite the name it relays no events; `AudioBridge` is the only
implementor.

**Input**: new Input System only (`activeInputHandler: 2`). Actions live in
`Assets/_Game/05_Settings/Input/Main Input.inputactions`; read input through the `Controls` static
facade in `InputHub.cs` — that file lives in `01_Objects/Prefabs/Input/`, not under `00_Scripts`.
`Main Input.cs` is `<auto-generated>` by the Input System code generator, so edit the
`.inputactions` asset, never the wrapper. Caveat to the "never legacy `UnityEngine.Input`" rule:
`DialogHUD.cs:48` still calls `Input.GetKeyDown(KeyCode.Space)`, which **throws at runtime** under
`activeInputHandler: 2`.

**Triggers** (`00_Scripts/Game/Triggers/`, all **global** namespace like the rest of `Game/`):
`BaseTrigger` supplies the `playLimit` / `unlimited` gate (`CanTrigger`, `AddTriggerCount`, both
`protected`) and three extend it — `CountTrigger` (`targetCount` → `onReached`), `DelayTrigger`
(`delay` → `onComplete`), `SceneStartTrigger` (`triggerOnStart` → `onTrigger`).
`ChangeSceneTrigger` is standalone and just calls `SceneSystem.Load`. Behaviour is
inspector-authored through `UnityEvent`s, so wiring lives in the scene YAML, not in code.

**Menu UI helpers** (`00_Scripts/Utils/UI/`, namespace `Slafurry.Utils.UI`): `UIFloat` (sine
`anchoredPosition` drift, optional phase), `ButtonHover` (pointer + selection scale/brightness),
`ButtonClickPunch` (press-squash, release-pop, submit support). Both button scripts animate
`target.localScale` **and** `Graphic.color` and each caches the rest value in `Awake`, so on one
transform they overwrite each other — point one `target` at a child GameObject.
`ButtonClickPunch`'s `overshoot` is documented as scaling the overshoot but actually multiplies
the settle *duration* (`releaseDuration * overshoot`); the peak is always
`_restScale * releaseScale`, so the tooltip's "smaller bump" is wrong twice over.

`SpriteAnimator` (frames, `fps`, `loop`, `pingPong`, `playOnEnable`, `useUnscaledTime`,
`restoreSpriteOnStop`; plus `onCycle` / `onFinished`) drives a UGUI `Image`; it is in
`Slafurry.Utils.UI`, **not** the global namespace, and it's wired on `Loading.prefab` and
`CheckpointIndicatorHUD.prefab`. Two traps:
the wait field is typed `object` because `WaitForSeconds` and `WaitForSecondsRealtime` share no
base narrower than `System.Object` (assigning one to a `CustomYieldInstruction` is `CS0029`), and
`OnDisable` stops the coroutine *without* firing `onFinished` (disable isn't completion, and Unity
only auto-stops coroutines on GameObject deactivation, not component deactivation).

**Namespaces** mostly mirror folders (`Slafurry.Core.*`, `Slafurry.System.*`, `Slafurry.Utils.*`),
but all of `Game/` (except `Dialog/`), `Manager/`, `System/Audio`, `System/Health`,
`System/State Machine` and most of `UI/` are **global**. Match whatever the file already does; don't
mass-migrate. Known exceptions to that rule, so don't "fix" them blind: `Game/Dialog/*` →
`Game.Dialog`, `UI/HUD/DialogHUD/*` → `Game.UI.HUD`, `Game/Story/StoryConditionalExecutor.cs` →
`Slafurry.Game.Story`, `UI/Generic/Text/LocalizeText.cs` → `Slafurry.UI.Generic`,
`Utils/GameFeel/GamefeelEffectPlayer.cs` → global. Folder names contain spaces (`Collide Trigger`,
`State Machine`, `Bridges List`) — quote paths.

## Audio and localization

Both are inspector-wired, not `Resources`-loaded — `Assets/Resources` holds only DOTween settings.

- `AudioSystem` (on the `Audio` GameObject in `Boot.unity`) resolves clips from three serialized
  arrays (`musicSounds`, `sfxSounds`, `musicTracks`) by string key. There are three distinct
  warnings: a **name miss** logs `Sound '<name>' tidak ditemukan! (tidak diputar)`; a **null
  array** logs `(daftar sound belum diisi)`; an uncreated source logs
  `AudioSource untuk '<name>' belum dibuat! (pastikan Initialize sudah jalan)`. Dropping a file
  into `03_Audio` changes nothing until an entry is added to an array.
- `PlayMusic(name)` checks **`musicTracks` first**, then falls back to legacy `musicSounds`.
  `musicSounds` still holds only `"Virtual Insanity"` (with `loop: 0`, which is why it never
  repeated before the `MusicTrack` rework).
- A `MusicTrack` is an **optional `intro` that plays once, then a `loop` clip**. The handoff is a
  fade-**in**, not a crossfade: the intro is never faded out, it just runs to the end
  (`WaitUntil(() => !intro.isPlaying)`), then the loop fades up from 0. The loop source forces
  `loop = true` at `Initialize()` *and* again at play time, so a track cannot play once and go
  silent. Registered: `MainTheme` (loop only), `Battlefield`, `FinalBoss`, `SpaceshipTheme`
  (intro + loop each) — clips in `03_Audio/MUSIC/<Name>/`.
- Switching tracks crossfades the outgoing one out in **both** directions (track→track, and
  track↔legacy `Sound`) via `FadeOutOutgoing`/`StopOutgoing`. Re-triggering the track that is
  already playing is a deliberate no-op, so an intro never restarts mid-way. Music fades use
  `Time.deltaTime`, so they stall while the game is paused.
- **Only the Main Menu actually plays music.** `PlayMusic` appears exactly once project-wide:
  `Main Menu.unity` wires `SceneStartTrigger.onTrigger` → `AudioBridge.set_fadeDuration(1)` then
  `PlayMusic("MainTheme")`. `Game.unity`, `Playground.unity` and both other menu scenes have zero.
  `Game.unity`'s `StopMusicWithFade` is in **Float mode (`m_Mode: 4`)**, so it actually calls
  `StopMusicWithFade(1f)` — the `m_StringArgument: Virtual Insanity` beside it is inert leftover
  YAML that Unity never passes. It is **not** a no-op: `AudioSystem` is `DontDestroyOnLoad`, so
  `MainTheme` is still the current track after the transition and this fades the main theme out.
- **Auditing audio calls: grep `m_MethodName:` alongside
  `m_TargetAssemblyTypeName: Slafurry.Core.Bridge.AudioBridge`**, not the string `PlayMusic` — the
  string misses a Float-mode call, and the type name is what scopes it to the bridge. The
  `PersistentListenerMode` encoding when reading raw YAML: `EventDefined=0, Void=1, Object=2,
  Int=3, Float=4, String=5, Bool=6`.
- `MusicPlayer.cs` is a code-driven alternative and is **not attached anywhere** — its GUID is
  never a `m_Script` in any scene or prefab. `Prefabs/System/Audio/MusicPlayer.prefab` is a naming
  trap: its root has only a Transform, and a **child** GameObject named `AudioBridge` holds
  `SingletonEventsBridge` + `AudioBridge`. That prefab *is* instantiated in `Main Menu.unity` and
  `Game.unity`, and the `m_Modifications` override there sets `m_Name: MusicPlayer` — a no-op that
  renames it to the name it already has. So the name matches the object, not a component.
- The mixer is `Assets/_Game/03_Audio/Master.mixer` (no subfolder) and **does** expose
  `MasterVolume`, `MusicVolume`, `SFXVolume`, so the Settings sliders are live. `Update*Volume`
  still null-guards the `AudioMixerGroup` lookup and still writes `PlayerPrefs`, so renaming or
  un-exposing a parameter degrades to a warning instead of an NRE — it does not throw, which makes
  it easy to miss. The `SettingsMenu` component lives on `Settings.prefab`, not the scene.
- `SettingsMenu.Start()` registers the slider listeners **before** assigning `.value`, on purpose:
  assigning `.value` fires the listener, so the restored value reaches both the slider and the
  mixer. Don't "optimise" it back to `SetValueWithoutNotify` — that moves the slider only and the
  mixer silently stays at its own default until the player drags something.
- **SFX registration is a small hole, wider than one caller.** `sfxSounds` holds only
  `"ParrySFX"`, so these all miss and no-op: `ObjectiveManager`'s `PlaySFX("Objective")` and
  `PlaySFX("ObjectiveComplete")`, `DialogHUD`'s `sfxCategory: "UI"` (typing SFX), and
  `UIButtonSFX`'s `"Click"`/`"Close"` on ~10 buttons across the menu prefabs. An `AudioClip` in
  scene/prefab YAML is `{fileID: 8300000, guid: <32 hex>, type: 3}`.
- `LocalizationSystem` is scaffolded but **not wired**: no `LocalizationTable` asset exists in the
  repo, the component's `table` field is `{fileID: 0}`, and `GetText` is an unguarded
  `table.GetText(...)`, so `Localize.Text(key)` will NRE. The only caller, `LocalizedText`, is
  also unusable — its class name doesn't match `LocalizeText.cs`, so Unity won't let you attach
  it, and nothing references it. Fix the table and the filename before building on localization.

## Assets are Drive-owned

- `Assets/_Game/02_Art/Sprite` and `Assets/_Game/03_Audio` are synced from Google Drive by
  `retrieve.yml` (manual, with a `force_update_all` option) and `track.yml` (nightly `0 23 * * *`
  UTC). Don't hand-add, rename, or move files there — the retriever keys on Drive file id +
  relative path and will re-create whatever it owns. Both push to a disposable `chore/asset`
  branch (bot "Utazumi Sakurako") and open/update a PR into `main`; asset commits end
  `[skip ci]`.
- `state/*.json` manifests are bot-owned. Never edit by hand.
- Hand-added art goes in `Assets/_Game/02_Art/Debug/`, deliberately *outside* the two Drive-owned
  folders so the retriever can't reclaim it. That's where `Circle.png` / `FillBox.png` live.
  (`FillBox.png` is a 1×1 PNG — fine as a `Filled` background template, wrong as a bar.)
- The sync downloads **media only, no `.meta`**. After a sync lands, open the project in Unity
  and commit the `.meta` files it generates — otherwise GUIDs churn and prefab/scene references
  silently break. Nothing validates import settings, so a bad one committed once stays bad.

## Unity asset rules (easy to get wrong here)

- **A `.meta` is part of the change.** Commit the `.meta` Unity generates for every new/renamed
  file. Never hand-edit a `guid:` in `.unity`/`.prefab`/`.asset` YAML — a typo silently orphans
  every reference, and a hand-copied GUID collides.
- **Never hand-write a `.meta` for a file Unity has already imported.** The trap that actually
  bites: Unity generated `.meta` with GUID `X`, a component was attached pointing at `X`, then a
  hand-written `.meta` with a *different* GUID was committed and clobbered it. Every reference
  still says `X`, which now resolves to nothing — the component silently becomes *Missing (Mono
  Script)* with all its serialized fields dropped, and the build still passes. If you must fix a
  GUID, make the committed `.meta` match what the YAML already references. Verify with a sweep
  that includes `Library/PackageCache` (packages hold real scripts; an `Assets/**/*.meta`-only
  sweep reports ~26 false positives here):
  ```bash
  # expect exactly 2: the URP camera-data components in Boot.unity + Dev/Boot For Playground.unity
  python3 -c "
  import re,glob
  h=set()
  for p in ('Assets/**/*.meta','Library/PackageCache/**/*.meta','Packages/**/*.meta'):
   for m in glob.glob(p,recursive=True):
    g=re.search(r'guid: ([0-9a-f]{32})',open(m,encoding='utf-8-sig',errors='ignore').read())
    h.add(g.group(1)) if g else None
  s={g for f in glob.glob('Assets/_Game/**/*.unity',recursive=True)+glob.glob('Assets/_Game/**/*.prefab',recursive=True)
   for g in re.findall(r'm_Script: \{fileID: \d+, guid: ([0-9a-f]{32})',open(f,encoding='utf-8-sig',errors='ignore').read())}
  print('unresolved:',sorted(s-h))"
  ```
  Those 2 come from a URP package no longer in `manifest.json`; they serialize nothing and are
  harmless. *Missing (Mono Script)* in any other case means the referenced `.cs` isn't in the repo
  — find it by grepping the GUID from the YAML, and repair in the editor by re-assigning the
  component, never by hand-editing GUIDs. TMP's *Examples & Extras* adds 2 more, but only inside
  its own demo scenes/prefabs.
- **Moving or deleting a `.cs` or prefab breaks every YAML reference to it** (scenes *and*
  prefabs both store bare GUIDs). Grep for the `.meta` GUID before you move anything, and prefer
  Unity's `Move`/`Delete` so references are rewritten.
- **Dangling sprite/font slots are not baseline.** A Drive sync can replace art with new GUIDs
  while prefabs keep pointing at the old ones. Inside `_Game` there are currently **zero**
  `m_Sprite: {fileID: 0}` and zero `m_fontFile: {fileID: 0}` — treat any as a fresh regression.
  (Scope the check to `_Game`: six `m_Sprite: {fileID: 0}` legitimately survive inside TMP's own
  *Examples & Extras* demo scenes.) `MainMenu.prefab` points at `Abaddon Bold`; `Abaddon
  Light.asset` was deleted, only its `.ttf` survives.
- **Sprite import settings live in `.meta`, so they are diffable but must be made in the editor.**
  `textureType: 8` = Sprite, `spriteMode: 1` = Single, `2` = Multiple; a Multiple sheet with an
  empty `spriteSheet.sprites` list yields **no usable sprite at all** — it must be sliced before it
  can be dragged into anything. Sheets are sliced by rectangle, so a `-Sheet` filename is not
  evidence it is a sheet: verify `spriteSheet.sprites` and count the `- name:` entries. All 51
  `02_Art` textures are already `textureType: 8`. `spritePixelsToUnits` differs per asset (49 are
  100, the 2 placeholder bayonets are 256), so a wrong PPU silently rescales a sprite rather than
  breaking the reference.
- **A Drive sync will not restore `.meta`.** The retriever pulls media only, so import settings
  (Sprite type, slicing, PPU, filter mode) are safe to commit and survive the next sync — but so
  does a bad setting, since nothing re-validates them. If a reimport looks wrong, diff the `.meta`
  before assuming the art changed.
- **Music loop import settings are a red herring.** All 7 `03_Audio/MUSIC/*/*.ogg` ship with
  identical `loadType: 0` + `compressionFormat: 1` (Vorbis) and **no `loop` field at all** in their
  `.meta`. Ticking Loop in the inspector would not change playback: `AudioSystem` passes
  `loop: true` into `CreateSource` for every `MusicTrack.loop` clip, and `AudioSource.loop` is what
  governs repetition. The `-intro`/`-loop` filename pair is the only signal for which clip loops.
- **There are two TextMesh Pro resource trees**: `Assets/_Vendor/TextMesh Pro` (fonts, no `.cs`)
  and `Assets/TextMesh Pro` (the full *Examples & Extras* import, 11 MB / 131 GUIDs). They share
  **no GUIDs**, so nothing collides, but the 34 *Examples & Extras* scripts really do compile into
  `Assembly-CSharp` — benchmarks, vertex shake and other unused demo code. It is inert but shipped;
  the first candidate if the repo needs slimming.
- **Hand-authoring scene YAML is a last resort** and Unity will not clean it up for you. A
  manually pasted `--- !u!1` GameObject block that omits `m_LocalScale` lands at `{0, 0, 0}`.
  Copy a real `RectTransform` block rather than hand-rolling one. A zero-scale *root Canvas* is
  a special case and is **correct**: at `m_RenderMode: 0` (Screen Space - Overlay) with
  `m_UiScaleMode: 1` (Scale With Screen Size) the canvas derives its own scale from
  `m_ReferenceResolution`, so the root `m_LocalScale` is inert — the `HealthHUD` /
  `ParryMeterHUD` / `CheckpointIndicatorHUD` prefabs all sit at `{0, 0, 0}` for that reason, and
  `Players.prefab` overrides them to `0` too. Don't "fix" those. A `m_RenderMode: 2` (Screen
  Space - Camera) canvas *does* use the RectTransform scale, which is why the HUD canvas in
  `Game.unity` needs an explicit `0.82734376` override. Check `m_RenderMode` before assuming a
  zero scale is a bug.
- Wiring is inspector-authored, not code-authored: new UI screens are their own scenes under
  `04_Scenes/` and must be added to `ProjectSettings/EditorBuildSettings.asset` to ship.
- `README.md` is now accurate (it documents `Assets/_Game/` and points here); its ARCHITECTURE
  section is a summary, so this file wins on any conflict.

## Git

- Tracked despite looking generated: `Packages/manifest.json`, `Packages/packages-lock.json`,
  `Potkeeter.slnx`, all of `ProjectSettings/`.
- `Potkeeter.slnx` is **tracked** and lists only the two `Assembly-CSharp*` projects — trimmed on
  purpose in the loading-screen PR, so its IDE project list is far smaller than the ~44 Unity
  generates. `dotnet build` works off the `.csproj` files directly, so this is only an
  IntelliSense-completeness issue. The other generated `.csproj` files are gitignored (`*.csproj`,
  `*.sln`; note `.slnx` is *not* ignored). Expect Unity to regenerate the full list locally and
  show that as a diff — don't "fix" it by committing the regenerated file unless asked.
- Ignored: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `build/`, `*.csproj`, `*.sln`.
- Commit style: `feat(scope):`, `fix:`, `chore(assets):`, `ci(retrieve):`, `sync:`; bot asset
  commits end with `[skip ci]`.
- Feature work goes on `feature/*` / `fix/*` / `ci/*` branches, PR into `main`. Releases are
  manual `workflow_dispatch` on the itch.io deploy workflow (`platforms`: All / Windows / macOS /
  WebGL, optional `release_tag` to also cut a GitHub Release). Nothing builds on push to `main`.
