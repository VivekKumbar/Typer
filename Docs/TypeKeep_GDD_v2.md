# TypeKeep GDD v2

> Note: this file did not exist in the repository before this section was
> added. Sections 1-19 (design pillars, core loop, economy, etc.) are not
> reproduced here since no prior draft of them exists to update — only
> Section 20 is populated, per the specific request that created it. Add the
> earlier sections separately when that content exists.

## Section 20 — Technical Conventions

### 20.1 Folder structure

As of the September 2026 Assets reorganization, the project follows this
structure. Every file below was moved with `AssetDatabase.MoveAsset` (GUIDs
preserved, no broken references) rather than raw filesystem moves.

```
Assets/
  _Project/
    Scripts/
      Runtime/
        Core/        GameManager, WaveManager, TypingController, WordBank,
                      SaveManager, RunContext, DayNightCycle, StatsManager,
                      Bullet, Coin, Tower, TurretAim, Soldier,
                      NativeKeyboardInput, KeyboardInput_LATER
        Enemies/      Enemy, EnemyAnimator, EnemyDissolve, EnemyHitFlash,
                      EnemySkinApplier, BigEnemySpawner
        UI/           MainMenu, LevelCarousel/LevelCard, ShopUI/ShopItemUI,
                      ConfirmPopup, AchievementsPanel/AchievementCardUI,
                      HUD-related scripts, PopupManager, CounterBounce,
                      and every other panel/popup/HUD/button script
        Systems/      ComboManager, ShieldManager/Shieldcontroller,
                      TimeSinkManager, UpgradeManager, AbilityUIRegistry,
                      AchievementManager, GroundSkinApplier, SkinApplier,
                      AdManager/AdRewardManager, FTUEManager, IAPManager
        Data/         ShopItem/Category/Catalog/Inventory, AchievementData/
                      Bank, UpgradeDefinition/Pool, RunSaveData, Wallet,
                      GameSettings, AdCompletionState, AdRewardType, FtueState
        Utilities/    CameraShake, SfxPlayer, HitStop, CoinFlyManager,
                      FloatingText, Billboard, LookAtCamera, ButtonClickSfx,
                      GlobalButtonSfx, MagnifyingObject, TargetSpotlight,
                      FrameRateUnlock, ScriptX
        Platform/     BridgeManager, BridgeStorageSync,
                      BridgeLeaderboardManager (the Playgama Bridge
                      integration layer, #if UNITY_WEBGL throughout)
      Editor/         DebugConsole (dev/DEVELOPMENT_BUILD-gated cheat
                      console), ProfilePanelBuilder, TimeSinkUIBuilder
      Tests/          empty -- no tests exist yet
    Art/
      Materials/      all .mat files (enemy/coin/fortress materials, skin
                      materials, dissolve/bubble-shield materials, etc.)
      Textures/       all standalone .png/.jpg (UI images, skin icons,
                      upgrade icons, backgrounds, splash/loading art)
      Shaders/        .shader / .shadergraph files (Dissolve_Shader,
                      ForceField, Galaxy, Shield, BubbleShieldShader)
      Models/         raw .fbx meshes not owned by a third-party pack
                      (X Bot.fbx, Circle.fbx) -- not in the original spec,
                      added because these two files had no other home
    Audio/            placeholder -- no audio clips exist yet (SfxPlayer is
                      fully procedural)
    Prefabs/
      Enemies/        Brute, Grunt, Monster, Runner, BigEnemy, X Bot 1
      UI/             card/button/popup prefabs, plus MainMenu/ (the
                      full main-menu prefab set: buttons, panels, popups)
      Abilities/      Bubble Shield
      Effects/        Bullet, Coin, Laser_Impact, Smokepuff, FloatingText,
                      SmokeEffect, tower_e, Global/Local Volume game
    Scenes/           MainMenu, GameScene, FTUEScene, SplashScene, plus a
                      disregard/ subfolder of old unused test scenes
    Resources/        BillingMode.json, PerformanceTestRunInfo.json,
                      PerformanceTestRunSettings.json -- kept in a literal
                      "Resources" folder in case any package resolves
                      BillingMode.json via Resources.Load internally
                      (no such call exists anywhere in Assets/Scripts)
    StreamingAssets/  Words/ (per-category word .txt + words.csv + .docx
                      reference copies), WordPacks/ (10 category .txt
                      files), achievements_sample.csv
    ScriptableObjects/ ShopCatalog + every Shop category/item instance,
                      UpgradePool + every Upgrade_* definition,
                      WordBank.asset, AchievementBank.asset
    Settings/         DefaultVolumeProfile, Global_Volume, Local_Volume,
                      Mobile/PC Renderer + RPAsset,
                      UniversalRenderPipelineGlobalSettings, Build
                      Profiles/ (Android)
  ThirdParty/         Protofactor, Sci Fi Assets, Layer Lab (merged from
                      two prior purchase locations -- GUI Pro-SurvivalClean
                      and GUI Pro-FantasyRPG + its bundled Scripts are now
                      siblings under one Layer Lab folder), castle,
                      Viking Village, UnityTechnologies (ParticlePack),
                      Vefects. Internal structure of every pack is
                      untouched -- only the top-level folder moved.
```

### 20.2 Folders intentionally left outside `_Project/` and `ThirdParty/`

A few existing top-level folders were **not** moved, because Unity requires
them at fixed, engine-reserved paths or moving them would risk breaking an
SDK's own auto-resolve mechanism:

- `Assets/Plugins/Android/` — Gradle template overrides. Unity's Android
  build pipeline scans this exact path.
- `Assets/WebGLTemplates/Bridge/` — the WebGL template (including the
  Playgama Bridge JS files: `playgama-bridge.js`, `playgama-bridge-unity.js`,
  `playgama-bridge-config.json`). Unity's Player Settings → WebGL Template
  picker only scans `Assets/WebGLTemplates/`.
- `Assets/TextMesh Pro/` — Unity's TMP Essential Resources import location.
- `Assets/Firebase/`, `Assets/ExternalDependencyManager/`,
  `Assets/MobileDependencyResolver/` — SDK/dependency-resolver folders with
  their own path assumptions; left in place rather than risk breaking their
  auto-update mechanisms.
- `Assets/_Recovery/` — pre-existing Unity crash-recovery scene backups
  (19 numbered `.unity` files), not project content. Left untouched since
  deleting scene files is outside the scope of a folder reorg; review and
  delete manually if no longer needed.

**Note on the actual Playgama Bridge SDK**: the C# API (`using Playgama;`,
`Bridge.platform`, `Bridge.storage`, etc.) is a Git-based UPM package
(`com.playgama.bridge` in `Packages/manifest.json`), not files under
`Assets/`. There is nothing under `Assets/Plugins/` to relocate for it.

### 20.3 CSV/text data loading convention

`WordBank` and `AchievementBank` both load their data through a plain
`public TextAsset` Inspector field (`wordFile` / `achievementFile`) parsed
once at runtime — never through `Resources.Load`. This means their source
`.csv`/`.txt` files can live anywhere, including `StreamingAssets/`, without
special handling; the reference is a normal GUID-based asset reference, not
a runtime path lookup. Confirmed via a project-wide search: zero
`Resources.Load` calls exist in `Assets/Scripts` (now `_Project/Scripts`).

### 20.4 Data vs. Art split

A ScriptableObject *definition script* (e.g. `ShopItem.cs`) lives under
`Scripts/Runtime/Data/`. A ScriptableObject *instance asset* built from that
definition (e.g. `Item_TowerBlue.asset`) lives under `ScriptableObjects/`.
Any material or texture an item references (skin materials, icon PNGs)
lives under `Art/Materials` or `Art/Textures`, not alongside the instance —
this was the main pattern needing untangling during the reorg, since the
old `Assets/Shop/Item_*/` folders mixed all three together per category.

### 20.5 Adding new content

- **New script**: add to the matching `Scripts/Runtime/<Category>/`
  subfolder. Editor-only scripts (anything gated by
  `#if UNITY_EDITOR`/`DEVELOPMENT_BUILD` for its entire file, or a custom
  inspector/editor window) go in `Scripts/Editor/` instead.
- **New prefab**: `Prefabs/<Enemies|UI|Abilities|Effects>/`.
- **New material/texture/shader**: the matching `Art/` subfolder.
- **New ScriptableObject instance**: `ScriptableObjects/`, referencing a
  definition script in `Scripts/Runtime/Data/`.
- **New word pack or achievement row**: no code changes — edit the CSV/txt
  directly in `StreamingAssets/`.
