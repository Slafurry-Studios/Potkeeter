# Potkeeter
A 2D rage-bait precision platformer where a Musketeer trapped in a cooking pot has no legs — only a musket, well-timed parries, and gravity.

<p align="center">
  <img src="https://github.com/user-attachments/assets/ad8c0ffa-ed4c-48d2-b0a1-54ed2dee8de1" width="100%" alt="Potkeeter banner" />
</p>

<br>
<p align="left">
  <img src="https://img.shields.io/badge/UNITY-ffffff?style=for-the-badge&logo=unity&logoColor=000000" />
  <img src="https://img.shields.io/badge/STATUS • IN DEVELOPMENT-000000?style=for-the-badge&logo=git&logoColor=ffffff" />
  <img src="https://img.shields.io/badge/MADE BY SLAFURRY STUDIOS-ffffff?style=for-the-badge&logo=gamepad&logoColor=000000" />
</p>

---

## ABOUT
Potkeeter is a 2D rage-bait action platformer inspired by *Getting Over It*. You play a heroic Musketeer trapped inside a cooking pot in the middle of a chaotic war, and with his legs rendered useless, shooting and precisely timed parries are your only means of movement, combat, and survival. Every mistake can send you tumbling back down the battlefield, so progress is earned the hard way — through failure, adaptation, and a stubborn refusal to stay in the pot.

---

## PLAY IT ON
<p align="left">
  <a href="https://lordzaini.itch.io/"><img src="https://img.shields.io/badge/ITCH.IO-ffffff?style=for-the-badge&logo=itch.io&logoColor=000000" /></a>
</p>

Available on Windows, macOS, and Web (HTML5).

---

## FEATURES
- **360° Parry-Based Movement** — deflect attacks with the musket to launch yourself through the environment
- **Shoot & Parry Combat** — switch between shooting enemies and precisely timed parries to fight and navigate
- **Physics-Based Climbing** — use the musket and parry mechanics to climb and overcome challenging obstacles
- **Rage-Bait Gameplay** — difficult precision challenges and the risk of losing progress create frustrating but rewarding moments
- **Checkpoint System** — checkpoints provide progress milestones throughout the level
- **Dialogue System** — character dialogue adds personality, humor, and context to the Musketeer's journey
- **Absurd Heroic Premise** — a heroic Musketeer trapped in a cooking pot for a distinctive mix of comedy, action, and frustration

---

## ARCHITECTURE
Core Unity conventions with a custom lightweight service layer. Everything game-specific lives under `Assets/_Game/`; the repo root **is** the Unity project root.

```
Assets/_Game/
├── 00_Scripts/
│   ├── Core/       — base classes & interfaces (Singleton, GameSystem, Manager) + Bridge/
│   ├── System/     — persistent cross-scene services (Audio, Health, Localization, Scene)
│   ├── Manager/    — per-session gameplay coordinators
│   ├── Game/       — per-instance controllers, entities & triggers
│   ├── UI/         — reactive observers & screen coordination
│   └── Utils/      — generic reusable helpers (GameFeel, UI/ animation helpers)
├── 01_Objects/     — prefabs, data & materials, grouped by domain
├── 02_Art/         — sprites & fonts (Sprite/ is Drive-synced, Debug/ is hand-added)
├── 03_Audio/       — SFX, music tracks & the audio mixer
├── 04_Scenes/      — Boot, Menu/, Game/, Dev/ & Cutscene/
└── 05_Settings/    — Input actions & project settings
```

Cross-scene services are `GameSystem<T>` singletons that survive scene loads; the ones that matter live on GameObjects in the `Boot` scene, not in a prefab. See [`AGENTS.md`](./AGENTS.md) for the conventions, wiring rules, and the traps that cost us real time.

---

## GETTING STARTED

### Prerequisites
- Unity `2022.3.62f3` (the version is pinned in `ProjectSettings/ProjectVersion.txt`)

### Setup
```bash
git clone https://github.com/Slafurry-Studios/Potkeeter.git
```
Open the project in Unity Hub, let it import, then open the `Boot` scene to start. Git LFS is not required — art and audio are stored as regular files.

---

## TECH STACK
![Unity](https://img.shields.io/badge/Unity-000000?style=flat-square&logo=unity&logoColor=ffffff)
![C#](https://img.shields.io/badge/C%23-000000?style=flat-square&logo=csharp&logoColor=ffffff)
![Cinemachine](https://img.shields.io/badge/Cinemachine-000000?style=flat-square&logo=unity&logoColor=ffffff)
![Input System](https://img.shields.io/badge/Input%20System-000000?style=flat-square&logo=unity&logoColor=ffffff)
![TextMeshPro](https://img.shields.io/badge/TextMeshPro-000000?style=flat-square&logo=unity&logoColor=ffffff)

---

## TEAM
| Role | Name |
|---|---|
| Project Manager, Game Designer, Level Designer | Lord Zaini (`haruto7013`) |
| 2D Artist, Game Designer | csw (`csw177`) |
| Audio Engineer | Ifanitas (`ifant2_56937`) |
| Unity Programmer | Faiz2979. (`flamexq`) |

---

## CONNECT
<p align="left">
  <a href="https://github.com/Slafurry-Studios"><img src="https://img.shields.io/badge/GITHUB-ffffff?style=for-the-badge&logo=github&logoColor=000000" /></a>
  <a href="https://www.linkedin.com/company/slafurry-studios/"><img src="https://img.shields.io/badge/LINKEDIN-000000?style=for-the-badge&logo=linkedin&logoColor=ffffff" /></a>
  <a href="https://slafurrystudios.itch.io/"><img src="https://img.shields.io/badge/ITCH.IO-ffffff?style=for-the-badge&logo=itch.io&logoColor=000000" /></a>
</p>
