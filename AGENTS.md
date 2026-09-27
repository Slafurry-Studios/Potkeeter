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
  `dotnet build` does not prove a new file compiles.** Use the standalone check below instead.
- **An incremental build reports `0 Warning(s)` even when warnings exist** — nothing recompiles,
  so nothing is re-diagnosed. Use `dotnet build Assembly-CSharp.csproj -t:Rebuild` to see them.
  Baseline is exactly **two**: `CS0649 GameFeel.gameFeelEffects` and `CS0414 DialogHUD.typeSFX`.
  Anything else is yours.

**Standalone compile — the only way to check a brand-new `.cs`.** Everything except
`Assembly-CSharp*` comes from references, so this catches new files too. Glob sources rather than
listing them, or you'll silently skip the file you just added:

```bash
cat > /tmp/check.sh <<'EOF'
set -uo pipefail
cd "/home/zaini/Projects/Slafurry Studios/Potkeeter"
# dotnet may be a snap install, so DON'T hardcode /usr/share/dotnet: a wrong
# glob yields an empty $CSC, and `dotnet ""` just prints its usage text.
CSC=$(ls -d /snap/dotnet-sdk/*/sdk/*/Roslyn/bincore/csc.dll \
          /usr/share/dotnet/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | head -1)
[ -n "$CSC" ] || { echo "FATAL: csc.dll not found - is a .NET SDK installed?"; exit 1; }
ED="$HOME/Unity/Hub/Editor/2022.3.62f3/Editor/Data/Managed"
skip() { case "$1" in *Editor.dll|*TestFramework.dll|*DocCodeSamples.dll) return 0;; esac; return 1; }
{
  for f in -nostdlib "-target:library" -langversion:9.0 -nowarn:CS0649 -out:/tmp/check_out.dll \
    "-r:$ED/../NetStandard/ref/2.1.0/netstandard.dll"; do printf '%s\n' "$f"; done
  for d in "$ED"/UnityEngine/UnityEngine.*.dll Library/ScriptAssemblies/*.dll; do
    b=$(basename "$d"); skip "$b" && continue
    case "$b" in Assembly-CSharp*.dll) continue;; esac
    # $ED is absolute, ScriptAssemblies is relative - prefix only the latter,
    # or you get Potkeeter//home/... and every reference "could not be found".
    case "$d" in /*) ref="$d";; *) ref="$PWD/$d";; esac
    printf -- '-r:"%s"\n' "$ref"; done
  find Assets/_Game/00_Scripts "Assets/_Game/01_Objects/Prefabs/Input" -name '*.cs' -print0 \
    | while IFS= read -r -d '' f; do printf '"%s"\n' "$PWD/$f"; done
} > /tmp/check.rsp
rm -f /tmp/check_out.dll
echo "refs+sources: $(grep -c . /tmp/check.rsp)"
# Never judge success by `grep -c 'error CS'` alone: a malformed invocation
# emits no such lines and reads as 0 = "clean". Trust the exit code, and
# confirm the assembly was actually written (rm'd above, so it can't be stale).
if dotnet "$CSC" @/tmp/check.rsp; then
  [ -f /tmp/check_out.dll ] || { echo "FATAL: csc exited 0 but wrote no assembly"; exit 1; }
  echo "COMPILE OK"
else
  echo "COMPILE FAILED"; exit 1
fi
EOF
bash /tmp/check.sh
```

`COMPILE OK` on the last line is the only trustworthy pass signal. A run that
prints nothing and reports no `error CS` almost always means the script itself
broke (empty `$CSC`, wrong `$ED`), not that the code is clean.

`Library/ScriptAssemblies/*.dll` supplies the packages (UGUI, TMP, Input System) — an
`Assets/**`-only reference set will fail on `Slider`, `TextMeshProUGUI`, etc. Expect **spurious
`CS0649` on every `[SerializeField]`** (hence `-nowarn:CS0649`): inspector assignment is invisible
to a standalone compile. Baseline is exactly **one** `CS0414` (`DialogHUD.typeSFX`) —
`GameFeel.gameFeelEffects`' `CS0649` is suppressed here, which is why the full build lists two.
This still does not catch a bad `.meta` GUID or a missing inspector reference; only Play mode does.

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
`WebGL`. 6 scenes are enabled, in this order: `Boot`, `Main Menu`, `About Menu`, `Settings Menu`,
`Level 1`, `Level 2`. A half-authored scene left enabled fails the whole build — check
that file before blaming a code change. Scenes live in subfolders (`04_Scenes/Game/Level 1.unity`,
`04_Scenes/Menu/Main Menu.unity`, `04_Scenes/Dev/Playground.unity`). `04_Scenes/Game/Game.unity`
was renamed to `Level 1` (GUID preserved, so references followed it). `Playground` was dropped
from the build list when `Level 2` entered it, so `Playground` ships only when you open the
project directly. `BootstrapLoader.targetSceneName` must be `Main Menu` to boot through the
menu; `Level 2` is reachable from it.

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
| `LocalSingleton<T>` | Per-scene, does *not* persist. Only `ScreenFlash` and `BulletManager`. Because it doesn't persist, a scene that needs one must contain it (or instantiate the prefab that does). Neither is in a scene: only `BulletManager` is on `Players.prefab`, so `ScreenFlash.Instance` is always null at runtime. |
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
  non-iterator `Initialize()` that throws synchronously kills `LoadSequence` outright (latent —
  every in-repo one is an iterator); and the timeout sums only **scaled** `Time.deltaTime` per
  `MoveNext`, so one long nested wait under-counts badly.

**Scene changes** go through `SceneSystem.Load(name)` (static wrapper over
`SceneLoader.LoadScene`). `OnBeforeSceneLoad` is an **async gate**: the loader spins
`while (!ready)`, so a subscriber that never invokes its callback **hangs the load forever** (sole
subscriber: `LoadingScreenUI`). Re-entrant loads are dropped with a warning. `MainMenu.cs` still
calls `SceneManager.LoadScene` directly for About/Settings, bypassing the gate.

**Scene names are a live bug, not just a style nit.** Names are plain strings into
`LoadSceneAsync`, so a wrong one fails *silently*. Real names have spaces: `Level 1`,
`Main Menu`, `Settings Menu`, `About Menu`. The C# defaults don't match — `MainMenu.cs` uses
`GameScene` / `SettingsScene` / `AboutScene`, `AboutMenu.cs` / `SettingsMenu.cs` default to
`MainMenu` (real: `Main Menu`), and `BootstrapLoader.targetSceneName` defaults to `MainMenu`
(the scene serializes the correct `Main Menu`, so boot works). The committed `MainMenu.prefab`
still serializes `_gameSceneName: GameScene` — a scene that doesn't exist (it was renamed to
`Level 1`) — so **don't "trust the inspector value", verify it against
`EditorBuildSettings.asset`**. `GameOver.cs` is the one place this was done right
(`private const string MainMenuScene = "Main Menu"`).

**Bridges** (`Core/Bridge/`): `SingletonEventsBridge` maps `GetComponents<ISubBridge>()` in `Awake`
to `GetBridge<T>()`, so gameplay reaches system behaviour without a direct reference. Despite the
name it relays no events; `AudioBridge` is the only implementor.

**Input**: new Input System only (`activeInputHandler: 2`). Actions live in
`Assets/_Game/05_Settings/Input/Main Input.inputactions`; read input through the `Controls` static
facade in `InputHub.cs` — that file lives in `01_Objects/Prefabs/Input/`, not under `00_Scripts`.
`Main Input.cs` is `<auto-generated>` by the Input System code generator, so edit the
`.inputactions` asset, never the wrapper. Caveat to the "never legacy `UnityEngine.Input`" rule —
two files still call it, and **both throw at runtime** under `activeInputHandler: 2`:
`DialogHUD.cs:48` (`Input.GetKeyDown(KeyCode.Space)`) and `CameraZoom.cs:70`
(`Input.GetAxis("Mouse ScrollWheel")`).

**Triggers** (`00_Scripts/Game/Triggers/`, all **global** namespace like the rest of `Game/`):
`BaseTrigger` supplies the `playLimit` / `unlimited` gate, extended by `CountTrigger`,
`DelayTrigger` and `SceneStartTrigger`; `ChangeSceneTrigger` is standalone and just calls
`SceneSystem.Load`. Behaviour is inspector-authored through `UnityEvent`s, so wiring lives in the
scene YAML, not in code.

**`CollideTrigger` and `DialogTrigger` are in a *different* folder** (`00_Scripts/Game/Collide
Trigger/`), so the section above doesn't cover them. `DialogTrigger` has no collider callback at
all — it's invoked *by* a `CollideTrigger`'s UnityEvent. `CollideTrigger` filters both
`OnTriggerEnter2D` and `OnTriggerExit2D` through `requiredTag` (default `"Player"`); empty means
"accept anything". It compares `other.tag`, **not** `CompareTag`, on purpose: `CompareTag` throws
`UnityException` for a tag missing from `TagManager`, so one typo in the inspector field would fire
on every contact. The filter matters more than it looks — the player carries **four** `BoxCollider2D`
(`Pot` tagged `Player`; `Bayonet`, `Body`, `GunBayonet` all `Untagged`), so an unfiltered trigger
fires `onTriggerEnter` **once per collider**. Both level-exit `CollideTrigger`s wire to
`ChangeSceneTrigger.ChangeScene`, so that used to call `SceneSystem.Load` four times at once and
eat the re-entrancy warning. `TagManager.asset` declares only `Enemy`; `Player` is a Unity builtin.

**Menu UI helpers** (`00_Scripts/Utils/UI/`, namespace `Slafurry.Utils.UI` — *not* global):
`UIFloat` (sine `anchoredPosition` drift), `ButtonHover` + `ButtonClickPunch` (press-squash,
release-pop, submit). Both button scripts animate `target.localScale` **and** `Graphic.color`, and
each caches the rest value in `Awake`, so on one transform they overwrite each other — point one
`target` at a child GameObject. `ButtonClickPunch`'s `overshoot` is documented as scaling the
overshoot but actually multiplies the settle *duration*; the peak is always
`_restScale * releaseScale`, so its tooltip is wrong twice over.

`SpriteAnimator` (frames, `fps`, `loop`, `pingPong`, `useUnscaledTime`, plus `onCycle` /
`onFinished`) drives a UGUI `Image`; wired on `Loading.prefab` and
`CheckpointIndicatorHUD.prefab`. Two traps: the per-frame wait field is typed `object` on purpose
(`WaitForSeconds` and `WaitForSecondsRealtime` share no base narrower than `System.Object`;
assigning one to a `CustomYieldInstruction` is `CS0029`), and `OnDisable` stops the coroutine
*without* firing `onFinished` — disable isn't completion.

**Namespaces** mostly mirror folders (`Slafurry.Core.*`, `Slafurry.System.*`, `Slafurry.Utils.*`),
but all of `Game/` (except `Dialog/`), `Manager/`, `System/Audio`, `System/Health`,
`System/State Machine` and most of `UI/` are **global**. Match whatever the file already does; don't
mass-migrate. Known exceptions, so don't "fix" them blind: `Game/Dialog/*` → `Game.Dialog`,
`UI/HUD/DialogHUD/*` → `Game.UI.HUD`, `Game/Story/StoryConditionalExecutor.cs` →
`Slafurry.Game.Story`, `UI/Generic/Text/LocalizeText.cs` → `Slafurry.UI.Generic`,
`Utils/GameFeel/GamefeelEffectPlayer.cs` → global. Folder names contain spaces (`Collide Trigger`,
`State Machine`, `Bridges List`) — quote paths.

## Player and combat

`BayonetController` is a plain `MonoBehaviour` with **no static `Instance`** — it lives on
`Players.prefab`. `ParryMeter` and `PlayerHealth` are also plain `MonoBehaviour`s but each keeps a
hand-rolled `public static Instance` (and `PlayerHealth` destroys duplicates in `Awake`). So you
cannot assume a uniform way to reach the player; check the type.

- **Reload is the shoot cooldown.** There is no ammo, magazine, or reload action — nothing in
  `Main Input.inputactions` either. After a shot the pistol is simply locked, and "reloading" means
  that lock. The window is `shootTransitionTime` (0.3s, state lock) + `shootCooldown` (0.5s).
  `ReloadProgress` is computed with the *same expression* as the shoot gate in
  `HandleShootStarted`, deliberately: HUD and mechanic can't drift apart. Because
  `lastActionTime` is written in `ShootingState.Exit()`, the bar sits at 0 through the transition
  and only fills across the cooldown. A parry also moves `lastActionTime`, so the bar rewinds —
  correct, since the gun really is locked again.
- **`EffectiveShootCooldown` reads `ParryMeter` live**, so the buff changes reload duration
  mid-reload. Don't cache it.
- **Parry is a counter, not a stun window.** `IParryable.OnParried()` on everything still in
  `parryRadius` at press time; bullets despawn without damage. `DroneController.parryFireLockout`
  (0.8s) is the one lingering timing effect and may not be what you want.
- **`parryRadiusMask` must be the Enemy layer only.** The mask was renamed from `enemyLayer`, so
  existing prefab assignments were lost — bullets are found through `BulletManager.ActiveBullets`
  and must *not* be in the mask. Setting it to 0 (a common state) silently disables parry against
  collider sources while bullets still work. Any new `IParryable` collider source needs its layer
  in that mask or parry logs `[Parry Missed]` and nothing else.
- **`HazardArea`** (`Game/Triggers/`, global ns) is a damage area that is also `IParryable`. Parry
  does *not* remove it — it only suppresses damage for `parryGraceDuration` (0 = permanent until
  the object is disabled); the launch out of the area is `BayonetController.parryLaunchForce`.
  Two inspector requirements, both silent when unmet: its `Collider2D` needs `isTrigger` (or
  `OnTriggerEnter2D` never fires) and its layer must be in `parryRadiusMask`.
- **`ObjectiveManager` is not attached in any scene or prefab**, so its `Instance` is always null
  at runtime. `GameOver.BackToMainMenu` calls `ObjectiveManager.Instance?.ClearObjectives()` for
  this reason — use `?.` if you reach it too.
- **Bullets have no collider, by design.** `BulletManager` pools per prefab, tracks
  `ActiveBullets`, and parry tests `IsInsideParryArea` against the same `EffectiveBoxSize` the
  damage `BoxCast` uses. Don't "fix" a missing bullet collider — there isn't one to add.
- **`HealthSystem` guards first, then raises events.** `OnDamageReceived` / `OnHealthChanged` fire
  only after `if (IsDead || damage <= 0f) return`. Hook one-shot feedback (SFX, VFX, screenshake) to
  the event, not to your own `TakeDamage()` wrapper, or it plays for rejected hits.
- **i-frames are gated in `PlayerHealth.TakeDamage`, not in `HealthSystem`** — `HealthSystem` is a
  plain C# class shared with `EnemyHealth`, so i-frames there would apply to every enemy and it has
  no update loop anyway. `PlayerHealth.TakeDamage` is the single choke point, so `Bullet` (via
  `IDamageable`) and `HazardArea` are covered for free. Two consequences: anything calling
  `PlayerHealth.Instance.Health.TakeDamage()` directly **bypasses** i-frames (that's why
  `DummyEnemyTest` goes through `TakeDamage()`), and the window is opened inside
  `HandleDamageReceived` — the only place guaranteed to mean damage actually landed, so rejected
  hits can't keep resetting the timer.
- **The i-frame clock is `Time.unscaledTime`, deliberately.** `HitStop` zeroes `timeScale` exactly
  when the player is hit, so a scaled timer would freeze the window during hitstop; `GameOver` also
  pauses. `PlayerDamageBlink` uses `WaitForSecondsRealtime` to match.
- **`PlayerDamageBlink` multiplies sprite alpha, never sets it.** In `Players.prefab`, `Bayonet`
  and `Square` have alpha `0` (deliberately hidden) and `Pot` is red-tinted, so assigning
  `color.a = offAlpha` would reveal the hidden sprites and drop Pot's tint. It auto-discovers
  renderers and skips any under a `ParallaxLayer` — `Parallax Bg` is a child of `Players` too, so
  filtering by component (not object name) is what keeps the background from blinking. It restores
  the original colors in `OnDisable` **and** on `OnDeath`, or the player stays invisible into the
  game-over screen.
- **Unity class IDs in YAML are easy to misread:** `!u!61` is `BoxCollider2D` (2D — it has
  `m_EdgeRadius`); `BoxCollider` (3D) is `!u!65`. Both `Drone.prefab` and `Players.prefab` already
  carry correct `BoxCollider2D`s.

## HUD scripts

There is no base class; copy `ParryMeterHUD` / `ReloadSliderHUD`. The conventions that matter:

- A HUD can `OnEnable` before its data source's `Awake`, so **bind in a coroutine that yields until
  the reference is non-null**, not in `OnEnable` directly.
- Unsubscribe in `OnDisable`. Coroutines only auto-stop on **GameObject** deactivation, not
  component deactivation, so stop the bind coroutine yourself or the next enable double-subscribes.
- Bars are display-only: `slider.interactable = false` (or players drag the HUD with the mouse) and
  assign with `SetValueWithoutNotify` (or the listener fires and clobbers your own value).
- **Self-hiding trap:** if the script toggles `visuals.SetActive(false)` and `visuals` is the same
  GameObject holding the script, the HUD kills itself and can never reappear. Point `visuals` at a
  child. `ReloadSliderHUD` logs an error if they collide.
- Don't touch `fillAmount` — the `Slider` drives it. Set the `Slider` value and the fill `Image`
  colour only.

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
  `Time.deltaTime`, so they stall while the game is paused (`PauseSystem` sets
  `Time.timeScale = 0`, and so does `HitStop`).
- **Only the Main Menu plays music.** Project-wide there is exactly one `PlayMusic` UnityEvent:
  `Main Menu.unity` wires `SceneStartTrigger.onTrigger` → `AudioBridge.set_fadeDuration(1)` then
  `PlayMusic("MainTheme")`. Nothing stops it — because `AudioSystem` is `DontDestroyOnLoad`,
  `MainTheme` keeps playing into `Game.unity` until something else changes track.
- **`PlaySFX` gives one `AudioSource` per name and calls `Stop()` before `Play()`.** Overlapping
  plays of the *same* key therefore cut each other off. This bites rapid fire: the `Shoot` clip is
  0.46s but the `ParryMeter` buff halves `shootCooldown` to 0.25s. `waitForCompletion: true` queues
  instead of cutting, but desyncs from the visible projectile. A real fix needs a per-SFX source
  pool.
- Lookup is `Array.Find` (**first match wins**), so a duplicate key is harmless-but-wasteful: it
  just leaves a second `AudioSource` unclaimed. Don't assume a name is unique.
- **Auditing audio calls: grep `m_MethodName:` alongside
  `m_TargetAssemblyTypeName: Slafurry.Core.Bridge.AudioBridge`**, not for `PlayMusic` — the string
  misses Float-mode calls, and the type name is what scopes it to the bridge. The
  `PersistentListenerMode` encoding when reading raw YAML: `EventDefined=0, Void=1, Object=2,
  Int=3, Float=4, String=5, Bool=6`.
- `MusicPlayer.cs` is a code-driven alternative and is **not attached anywhere** — its GUID is
  never a `m_Script` in any scene or prefab. `Prefabs/System/Audio/MusicPlayer.prefab` is a naming
  trap: a **child** named `AudioBridge` holds `SingletonEventsBridge` + `AudioBridge`, and the root
  has only a Transform. The name matches the object, not a component.
- The mixer is `Assets/_Game/03_Audio/Master.mixer` (no subfolder) and **does** expose
  `MasterVolume`, `MusicVolume`, `SFXVolume`, so the Settings sliders are live. `Update*Volume`
  still null-guards the `AudioMixerGroup` lookup and still writes `PlayerPrefs`, so renaming or
  un-exposing a parameter degrades to a warning instead of an NRE — it does not throw, which makes
  it easy to miss. The `SettingsMenu` component lives on `Settings.prefab`, not the scene.
- `SettingsMenu.Start()` registers the slider listeners **before** assigning `.value`, on purpose:
  assigning `.value` fires the listener, so the restored value reaches both the slider and the
  mixer. Don't "optimise" it back to `SetValueWithoutNotify` — that moves the slider only and the
  mixer silently stays at its own default until the player drags something.
- **`sfxSounds` holds 7 keys:** `ParrySFX`, `TakeDamage`, `Reloading`, `Shoot` (the player set, all
  wired) plus `EnemyLaser`, `EnemyHit`, `EnemyDeath`, driven by `EnemyShooter.fireSFX` and
  `EnemyHealth.hitSFX` / `deathSFX` — all three default to a key, and empty means silence. Still
  unregistered, so these all miss and no-op: `ObjectiveManager`'s
  `"Objective"` / `"ObjectiveComplete"`, `DialogHUD`'s `sfxCategory: "UI"` (typing SFX), and
  `UIButtonSFX`'s `"Click"` / `"Close"` on ~10 buttons across the menu prefabs. An `AudioClip` in
  scene/prefab YAML is `{fileID: 8300000, guid: <32 hex>, type: 3}`. `Assets/_Game/03_Audio/SFX/Enemy`
  also ships `sfx_enemy-attack`, `sfx_enemy-hit-1` and `sfx_explosion`, which are still
  unregistered — dropping them in does nothing until a key is added.
- **`PlaySFX` re-triggers, so `waitForCompletion` is a real choice, not a detail.** One `AudioSource`
  per key, `Stop()` before `Play()`: overlapping plays of the *same* key cut each other off. The
  drone's `fireSFX` wants `false` (queueing desyncs a burst from its bullets); anything where the
  tail matters wants `true` and accepts the queue.
- `LocalizationSystem` is scaffolded but **not wired**: no `LocalizationTable` asset exists in the
  repo, the component's `table` field is `{fileID: 0}`, and `GetText` is an unguarded
  `table.GetText(...)`, so `Localize.Text(key)` will NRE. The only caller is also unusable —
  `LocalizeText.cs` declares `class LocalizedText`, so Unity won't let you attach it by that name,
  and nothing references it. Fix the table and the class/filename mismatch before building on it.

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
  GUID, make the committed `.meta` match what the YAML already references. When you attach a
  component in the editor, verify the on-disk GUID before committing rather than after.
- Verify with a sweep that includes `Library/PackageCache` (packages hold real scripts; an
  `Assets/**/*.meta`-only sweep reports ~26 false positives here):
  ```bash
  # expect exactly 3 (all benign): the two URP camera-data components in
  # Boot.unity + Dev/Boot For Playground.unity, plus Unity's own
  # 0000000000000000e000000000000000 placeholder, which appears in 10 files.
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
  *Missing (Mono Script)* in any other case means the referenced `.cs` isn't in the repo — find it
  by grepping the GUID from the YAML, and repair in the editor by re-assigning the component, never
  by hand-editing GUIDs. TMP's *Examples & Extras* adds 2 more, but only inside its own demo
  scenes/prefabs.
- **git reports a deleted `.meta` plus an added `.meta` as an `R083` rename.** That is cosmetic and
  does not mean you moved anything — check the GUIDs on disk, not the diff's rename arrow.
- **Moving or deleting a `.cs` or prefab breaks every YAML reference to it** (scenes *and*
  prefabs both store bare GUIDs). Grep for the `.meta` GUID before you move anything, and prefer
  Unity's `Move`/`Delete` so references are rewritten. An orphaned field on a *deleted* component
  (e.g. `stunDuration` left in `Drone.prefab` after `DroneStunnedState` was removed) is harmless —
  Unity drops it on the next save; don't hand-strip it.
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
  evidence it is one: verify `spriteSheet.sprites` and count `- name:` entries.
  `spritePixelsToUnits` varies per asset, so a wrong PPU rescales a sprite instead of breaking the
  reference.
- **A Drive sync will not restore `.meta`.** The retriever pulls media only, so import settings
  (Sprite type, slicing, PPU, filter mode) are safe to commit and survive the next sync — but so
  does a bad setting, since nothing re-validates them. If a reimport looks wrong, diff the `.meta`
  before assuming the art changed.
- **Music loop import settings are a red herring.** All `03_Audio/MUSIC/*/*.ogg` ship identical
  `loadType: 0` + `compressionFormat: 1` and **no `loop` field** in their `.meta`. Ticking Loop
  would change nothing: `AudioSystem` passes `loop: true` to `CreateSource` for every
  `MusicTrack.loop` clip. The `-intro`/`-loop` filename pair is the only signal for which loops.
- **Two TextMesh Pro trees**: `Assets/_Vendor/TextMesh Pro` (fonts, no `.cs`) and
  `Assets/TextMesh Pro` (full *Examples & Extras* import, 11 MB). No shared GUIDs, but the
  *Examples & Extras* scripts really do compile into `Assembly-CSharp`. Inert but shipped.
- **Hand-authoring scene YAML is a last resort** and Unity will not clean it up for you. A
  manually pasted `--- !u!1` GameObject block that omits `m_LocalScale` lands at `{0, 0, 0}`.
  Copy a real `RectTransform` block rather than hand-rolling one. A zero-scale *root Canvas* is
  a special case and is **correct**: at `m_RenderMode: 0` (Screen Space - Overlay) with
  `m_UiScaleMode: 1` (Scale With Screen Size) the canvas derives its own scale from
  `m_ReferenceResolution`, so the root `m_LocalScale` is inert — the `HealthHUD` /
  `ParryMeterHUD` / `CheckpointIndicatorHUD` prefabs sit at `{0, 0, 0}` for that reason, and
  `Players.prefab` overrides them to `0` too. Don't "fix" those. A `m_RenderMode: 2` canvas *does*
  use the RectTransform scale, which is why the HUD canvas in `Game.unity` carries an explicit
  `0.82734376` override. Check `m_RenderMode` before assuming zero scale is a bug.
- Wiring is inspector-authored, not code-authored: new UI screens are their own scenes under
  `04_Scenes/` and must be added to `ProjectSettings/EditorBuildSettings.asset` to ship.
- `README.md` is accurate (it documents `Assets/_Game/` and points here); its ARCHITECTURE
  section is a summary, so this file wins on any conflict.

## Git

- Tracked despite looking generated: `Packages/manifest.json`, `Packages/packages-lock.json`,
  `Potkeeter.slnx`, all of `ProjectSettings/`.
- `Potkeeter.slnx` is **tracked** and lists only the two `Assembly-CSharp*` projects — trimmed on
  purpose, so its IDE project list is far smaller than the ~44 Unity generates. `dotnet build` works
  off the `.csproj` files, so this is only an IntelliSense-completeness issue. The other generated
  `.csproj` files are gitignored (`*.csproj`, `*.sln`; note `.slnx` is *not* ignored). Expect
  Unity to regenerate the full list locally and show that as a diff — don't commit it unless asked.
- Ignored: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `build/`, `*.csproj`, `*.sln`.
- Commit style: `feat(scope):`, `fix:`, `chore(assets):`, `ci(retrieve):`, `sync:`; bot asset
  commits end with `[skip ci]`.
- **Read `git diff` before staging.** Editor saves and scripted edits both leave trailing-whitespace
  and missing-final-newline noise that has no business in a commit. Use
  `git checkout -- <file>` to drop a hunk that is only whitespace.
- The working tree often has unrelated in-progress edits from a live editor session. Stage by
  **explicit path**, never `git add -A`, and confirm nothing you didn't intend is in the commit.
- Feature work goes on `feature/*` / `fix/*` / `ci/*` branches, PR into `main`. Releases are
  manual `workflow_dispatch` on the itch.io deploy workflow (`platforms`: All / Windows / macOS /
  WebGL, optional `release_tag` to also cut a GitHub Release). Nothing builds on push to `main`.
