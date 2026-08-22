using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Put this on an object in your MAIN MENU scene.
// - Hook the "NEW GAME" / "PLAY" button's OnClick to PlayGame() (unchanged
//   binding — if a save exists it now shows a confirm popup before erasing it)
// - Hook the "CONTINUE" button's OnClick to ContinueGame() (shows a "Continuing
//   from Wave X" confirm popup, then proceeds)
// - Hook a Quit button (optional) to Quit()
// Both confirm flows share ONE ConfirmPopup instance (confirmPopup below) —
// it's just Show()'n with different text/callbacks each time, not duplicated.
// It loads the game scene asynchronously and shows a loading bar.
public class MainMenu : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Exact name of your game scene (must be added to Build Settings).")]
    public string gameSceneName = "GameScene";

    [Header("Loading UI (optional, but you asked for it)")]
    public GameObject loadingPanel;   // full-screen panel, disabled by default
    public Slider progressBar;
    public TMP_Text progressText;
    [Tooltip("Keep the loading screen visible at least this long so it doesn't flash by.")]
    public float minShowTime = 1.2f;

    [Header("Continue / New Game")]
    [Tooltip("The whole Continue button — shown only when a save exists.")]
    public GameObject continueButtonRoot;
    [Tooltip("Label on the Continue button, set to 'CONTINUE - WAVE X'.")]
    public TMP_Text continueLabel;
    [Tooltip("Single shared popup used to confirm BOTH New Game (erase warning) and Continue (resume confirmation). Leave empty to skip confirmation entirely (not recommended) and act immediately.")]
    public ConfirmPopup confirmPopup;
    [Tooltip("The full upgrade pool — used to resolve the Continue popup's saved upgrade ids to their icon/name for the build-preview row. Assign the same UpgradePool asset UpgradeManager uses in GameScene.")]
    public UpgradePool upgradePool;
    [Tooltip("The shop catalog — used to resolve the Continue popup's saved word-pack ids AND ground-skin id to their ShopItems (icon/name/previewImage). Assign the same ShopCatalog WordBank/GroundSkinApplier use in GameScene.")]
    public ShopCatalog shopCatalog;

    void Start()
    {
        RefreshContinueButton();
    }

    void RefreshContinueButton()
    {
        bool hasSave = SaveManager.HasSave();
        if (continueButtonRoot != null) continueButtonRoot.SetActive(hasSave);
        if (hasSave && continueLabel != null)
            continueLabel.text = "CONTINUE - WAVE " + SaveManager.GetSavedWave();
    }

    // Hook the "CONTINUE" button here. Button should already be hidden/
    // disabled when no save exists (see RefreshContinueButton), but the
    // HasSave() check below is the real guard in case it's clicked anyway.
    public void ContinueGame()
    {
        if (!SaveManager.HasSave()) return;

        if (confirmPopup != null)
        {
            RunSaveData save = SaveManager.LoadRun();
            int wave = save != null ? save.waveNumber : SaveManager.GetSavedWave();
            confirmPopup.ShowWithPreview(
                "Continue Run",
                "Continuing from Wave " + wave + ".",
                BuildAbilityPreview(save),
                BuildWordPackPreview(save),
                ResolveGroundSkinBackground(save),
                ProceedWithContinue);
        }
        else
        {
            ProceedWithContinue();
        }
    }

    // Resolves RunSaveData's saved (id, level) pairs to their UpgradeDefinition
    // via upgradePool — UpgradeManager.Instance doesn't exist in this scene, so
    // the pool has to be looked up directly rather than through the runtime
    // singleton. Reads ANY unlocked upgrade generically (no hardcoded ability
    // list), so it stays correct as more upgrades are added later.
    List<(UpgradeDefinition def, int level)> BuildAbilityPreview(RunSaveData save)
    {
        var result = new List<(UpgradeDefinition, int)>();
        if (save == null || save.upgradeIds == null || upgradePool == null || upgradePool.upgrades == null)
            return result;

        for (int i = 0; i < save.upgradeIds.Length; i++)
        {
            int level = i < save.upgradeLevels.Length ? save.upgradeLevels[i] : 0;
            if (level <= 0) continue;

            UpgradeDefinition def = upgradePool.upgrades.Find(u => u != null && u.id == save.upgradeIds[i]);
            if (def != null) result.Add((def, level));
        }
        return result;
    }

    // Resolves RunSaveData's LOCKED word-pack ids (see RunContext) to their
    // ShopItem via shopCatalog — same pattern as BuildAbilityPreview
    // above (a live WordBank doesn't exist in the Main Menu scene either, so
    // the catalog has to be searched directly). Empty/no packs locked in
    // returns an empty list; ConfirmPopup shows its own "Default Words"
    // placeholder for that case.
    List<ShopItem> BuildWordPackPreview(RunSaveData save)
    {
        var result = new List<ShopItem>();
        if (save == null || save.selectedWordPackIds == null || shopCatalog == null || shopCatalog.categories == null)
            return result;

        foreach (string id in save.selectedWordPackIds)
        {
            ShopItem found = null;
            foreach (ShopCategory cat in shopCatalog.categories)
            {
                if (cat == null || cat.items == null) continue;
                found = cat.items.Find(i => i != null && i.kind == ShopItemKind.WordPack && i.id == id);
                if (found != null) break;
            }
            if (found != null) result.Add(found);
        }
        return result;
    }

    // Resolves RunSaveData's LOCKED ground-skin id (see RunContext) to its
    // ShopItem's previewImage/icon via shopCatalog -- the SAVED run's
    // ground, not whatever's currently equipped in the shop. Returns null if
    // nothing was locked in or it can't be resolved; ConfirmPopup then just
    // resets the Dialog to its default background sprite.
    Sprite ResolveGroundSkinBackground(RunSaveData save)
    {
        if (save == null || string.IsNullOrEmpty(save.groundSkinId) || shopCatalog == null || shopCatalog.categories == null)
            return null;

        foreach (ShopCategory cat in shopCatalog.categories)
        {
            if (cat == null || cat.items == null) continue;
            ShopItem item = cat.items.Find(i => i != null && i.id == save.groundSkinId);
            if (item != null) return item.previewImage != null ? item.previewImage : item.icon;
        }
        return null;
    }

    void ProceedWithContinue()
    {
        SaveManager.IsContinuing = true;
        StartCoroutine(LoadGame());
    }

    // Hook the "NEW GAME" / "PLAY" button here. If a save exists, confirms
    // first (erasing it is a one-way action); if not, starts immediately —
    // no point confirming "start a new game" when there's nothing to lose.
    public void PlayGame()
    {
        if (SaveManager.HasSave() && confirmPopup != null)
        {
            int wave = SaveManager.GetSavedWave();
            confirmPopup.Show(
                "Start New Game?",
                "Your current progress at Wave " + wave + " will be lost. Are you sure you want to start a new game?",
                StartFreshGame);
        }
        else
        {
            StartFreshGame();
        }
    }

    void StartFreshGame()
    {
        SaveManager.ClearSave();
        SaveManager.IsContinuing = false;
        StartCoroutine(LoadGame());
    }

    public void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // stops Play mode in the editor
#endif
    }

    IEnumerator LoadGame()
    {
        if (loadingPanel) loadingPanel.SetActive(true);
        float start = Time.unscaledTime;

        AsyncOperation op = SceneManager.LoadSceneAsync(gameSceneName);
        op.allowSceneActivation = false; // wait until we say go

        while (!op.isDone)
        {
            // Unity reports 0 -> 0.9 while loading, then holds at 0.9 until activated
            float progress = Mathf.Clamp01(op.progress / 0.9f);
            if (progressBar) progressBar.value = progress;
            if (progressText) progressText.text = Mathf.RoundToInt(progress * 100f) + "%";

            // Loaded AND minimum display time elapsed -> enter the game
            if (op.progress >= 0.9f && Time.unscaledTime - start >= minShowTime)
            {
                if (progressBar) progressBar.value = 1f;
                if (progressText) progressText.text = "100%";
                op.allowSceneActivation = true;
            }
            yield return null;
        }
    }
}