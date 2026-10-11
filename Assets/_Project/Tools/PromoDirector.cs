#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// EDITOR-ONLY promo recorder. Plays a scripted demo of the real game (auto-typing,
// abilities, boss, shop), overlays captions and records it to an MP4 with
// Unity's built-in MediaEncoder. The whole file is compiled out of builds.
//
// Start it with PromoDirector.Arm() and then enter Play Mode from MainMenu;
// it stops Play Mode by itself when the video is written to OutputPath.
public class PromoDirector : MonoBehaviour
{
    const string RunFlag = "PromoDirector.Run";
    const int Fps = 30;
    public const string OutputPath = "Recordings/Lexora-promo.mp4";

    public static void Arm() => SessionState.SetBool(RunFlag, true);
    public static bool IsDone { get; private set; }
    public static string LastError { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoStart()
    {
        if (!SessionState.GetBool(RunFlag, false)) return;
        Application.runInBackground = true; // keep ticking while the editor is unfocused
        SessionState.SetBool(RunFlag, false);
        IsDone = false;
        LastError = null;
        var go = new GameObject("[PromoDirector]");
        DontDestroyOnLoad(go);
        go.AddComponent<PromoDirector>();
    }

    // ------------------------------------------------------------------
    // Recording
    // ------------------------------------------------------------------
    MediaEncoder encoder;
    bool recording;
    int framesWritten;
    Texture2D frameTex;
    RenderTexture frameRT;
    int videoW, videoH;

    void StartEncoder()
    {
        videoW = Screen.width & ~1;
        videoH = Screen.height & ~1;
        string full = Path.GetFullPath(OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        if (File.Exists(full)) File.Delete(full);
        var attrs = new VideoTrackAttributes
        {
            frameRate = new MediaRational(Fps),
            width = (uint)videoW,
            height = (uint)videoH,
            includeAlpha = false,
            bitRateMode = VideoBitrateMode.High
        };
        encoder = new MediaEncoder(full, attrs);
        if (Directory.Exists("Temp/promo_frames")) Directory.Delete("Temp/promo_frames", true);
        Directory.CreateDirectory("Temp/promo_frames");
        frameTex = new Texture2D(videoW, videoH, TextureFormat.RGBA32, false);
        frameRT = new RenderTexture(videoW, videoH, 0, RenderTextureFormat.ARGB32);
        Debug.Log($"[PromoDirector] Recording {videoW}x{videoH} @ {Fps} fps to {full}");
    }

    IEnumerator CaptureLoop()
    {
        var eof = new WaitForEndOfFrame();
        while (true)
        {
            yield return eof;
            if (!recording || encoder == null) continue;
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            if (shot.width == videoW && shot.height == videoH && shot.format == TextureFormat.RGBA32)
            {
                encoder.AddFrame(shot);
            }
            else
            {
                // The captured pixels only live on the CPU until Apply(), so upload before blitting.
                shot.Apply(false);
                Graphics.Blit(shot, frameRT);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = frameRT;
                frameTex.ReadPixels(new Rect(0, 0, videoW, videoH), 0, 0, false);
                RenderTexture.active = prev;
                encoder.AddFrame(frameTex);
            }
            if (framesWritten % (Fps / 2) == 0)
                File.WriteAllBytes($"Temp/promo_frames/f_{framesWritten:00000}.jpg", shot.EncodeToJPG(70));
            Destroy(shot);
            framesWritten++;
        }
    }

    void StopEncoder()
    {
        recording = false;
        if (encoder != null) { encoder.Dispose(); encoder = null; }
        Debug.Log($"[PromoDirector] Wrote {framesWritten} frames ({framesWritten / (float)Fps:F1}s) to {Path.GetFullPath(OutputPath)}");
    }

    // ------------------------------------------------------------------
    // Overlay (captions, fades, tap rings)
    // ------------------------------------------------------------------
    RectTransform canvasRT;
    Image fade;
    CanvasGroup captionGroup;
    TMP_Text captionText;
    RectTransform captionRT;
    TMP_FontAsset font;
    Coroutine captionRoutine;

    void BuildOverlay()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/ThirdParty/Layer Lab/GUI Pro-SurvivalClean/ResourcesData/Fonts/NEXON Football Gothic B SDF.asset");

        var canvasGO = new GameObject("PromoOverlay", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRT = canvasGO.GetComponent<RectTransform>();

        // Caption: dark ribbon under the HUD, gold title + white subtitle.
        var capGO = new GameObject("Caption", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        capGO.transform.SetParent(canvasRT, false);
        captionRT = capGO.GetComponent<RectTransform>();
        captionRT.anchorMin = new Vector2(0f, 1f);
        captionRT.anchorMax = new Vector2(1f, 1f);
        captionRT.pivot = new Vector2(0.5f, 1f);
        captionRT.sizeDelta = new Vector2(0f, 210f);
        captionRT.anchoredPosition = new Vector2(0f, -250f);
        var capImg = capGO.GetComponent<Image>();
        capImg.color = new Color(0.02f, 0.03f, 0.08f, 0.72f);
        capImg.raycastTarget = false;
        captionGroup = capGO.GetComponent<CanvasGroup>();
        captionGroup.alpha = 0f;
        captionGroup.blocksRaycasts = false;

        var txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(captionRT, false);
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(40f, 10f);
        txtRT.offsetMax = new Vector2(-40f, -10f);
        var tmp = txtGO.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 30f;
        tmp.fontSizeMax = 84f;
        tmp.lineSpacing = -8f;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        captionText = tmp;

        // Full-screen fade, drawn last so it covers everything.
        var fadeGO = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        fadeGO.transform.SetParent(canvasRT, false);
        var fadeRT = fadeGO.GetComponent<RectTransform>();
        fadeRT.anchorMin = Vector2.zero;
        fadeRT.anchorMax = Vector2.one;
        fadeRT.offsetMin = fadeRT.offsetMax = Vector2.zero;
        fade = fadeGO.GetComponent<Image>();
        fade.color = Color.black;
        fade.raycastTarget = false;
    }

    // Gameplay captions sit under the HUD; menu captions sit near the bottom so
    // they never cover the LEXORA logo.
    void Caption(string title, string subtitle, float seconds, bool bottom = false)
    {
        Mark("caption: " + title + " / " + subtitle);
        captionRT.anchorMin = new Vector2(0f, bottom ? 0f : 1f);
        captionRT.anchorMax = new Vector2(1f, bottom ? 0f : 1f);
        captionRT.pivot = new Vector2(0.5f, bottom ? 0f : 1f);
        captionRT.anchoredPosition = new Vector2(0f, bottom ? 90f : -225f);
        if (captionRoutine != null) StopCoroutine(captionRoutine);
        captionRoutine = StartCoroutine(CaptionRoutine(title, subtitle, seconds));
    }

    IEnumerator CaptionRoutine(string title, string subtitle, float seconds)
    {
        string body = string.IsNullOrEmpty(title)
            ? subtitle
            : $"<color=#FFD24D>{title}</color>" + (string.IsNullOrEmpty(subtitle) ? "" : $"\n<size=70%>{subtitle}</size>");
        captionText.text = body;
        const float inTime = 0.25f, outTime = 0.25f;
        float hold = Mathf.Max(0f, seconds - inTime - outTime);
        for (float t = 0; t < inTime; t += 1f / Fps)
        {
            float k = t / inTime;
            captionGroup.alpha = k;
            captionRT.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, 1f - (1f - k) * (1f - k));
            yield return null;
        }
        captionGroup.alpha = 1f;
        captionRT.localScale = Vector3.one;
        yield return Wait(hold);
        for (float t = 0; t < outTime; t += 1f / Fps)
        {
            captionGroup.alpha = 1f - t / outTime;
            yield return null;
        }
        captionGroup.alpha = 0f;
        captionRoutine = null;
    }

    IEnumerator Fade(float to, float seconds)
    {
        float from = fade.color.a;
        for (float t = 0; t < seconds; t += 1f / Fps)
        {
            fade.color = new Color(0, 0, 0, Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }
        fade.color = new Color(0, 0, 0, to);
    }

    // A white ring that pops where a button is "tapped", so viewers see the press.
    void TapRing(Component target)
    {
        if (target == null) return;
        var rt = target.transform as RectTransform;
        if (rt == null) return;
        Canvas c = rt.GetComponentInParent<Canvas>();
        Camera cam = c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
        StartCoroutine(TapRingRoutine(screen));
    }

    IEnumerator TapRingRoutine(Vector2 screenPos)
    {
        var go = new GameObject("Tap", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasRT, false);
        go.transform.SetSiblingIndex(captionRT.GetSiblingIndex());
        var rt = go.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPos, null, out Vector2 local);
        rt.anchoredPosition = local;
        var img = go.GetComponent<Image>();
        img.sprite = RingSprite();
        img.raycastTarget = false;
        const float life = 0.45f;
        for (float t = 0; t < life; t += 1f / Fps)
        {
            float k = t / life;
            rt.sizeDelta = Vector2.one * Mathf.Lerp(70f, 190f, k);
            img.color = new Color(1f, 1f, 1f, 0.9f * (1f - k));
            yield return null;
        }
        Destroy(go);
    }

    static Sprite s_ring;
    static Sprite RingSprite()
    {
        if (s_ring != null) return s_ring;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - (r - 8f)) / 5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        s_ring = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return s_ring;
    }

    // ------------------------------------------------------------------
    // Auto-typer: types the lowest on-screen enemy's word at a human-ish pace.
    // Only enemies below the caption ribbon count, so viewers see every hit.
    // ------------------------------------------------------------------
    float typeRate;          // letters per second, 0 = hands off
    float typeAcc;
    bool typeBoss = true;    // false = leave the boss alone, type only minions
    const float TypeableViewportY = 0.74f;

    void Update()
    {
        if (typeRate <= 0f) { typeAcc = 0f; return; }
        var tc = TypingController.Instance;
        var gm = GameManager.Instance;
        if (tc == null || gm == null || gm.IsGameOver || Time.timeScale == 0f) return;
        if (UpgradeManager.Instance != null && UpgradeManager.Instance.IsDraftOpen) return;

        typeAcc += typeRate / Fps;
        while (typeAcc >= 1f)
        {
            typeAcc -= 1f;
            Enemy target = tc.CurrentTarget;
            if (target == null || target.IsDefeated || !Typeable(target))
                target = LowestTypeableEnemy();
            if (target == null) { typeAcc = 0f; break; }
            if (tc.CurrentTarget != target && target.TypedCount == 0)
                tc.LockAndTypeFirst(target);
            else if (target.NextChar != '\0')
                tc.ReceiveChar(target.NextChar);
        }
    }

    static float ViewportY(Enemy e)
    {
        Camera cam = Camera.main;
        return cam != null ? cam.WorldToViewportPoint(e.transform.position).y : 0f;
    }

    bool Typeable(Enemy e) =>
        e != null && !e.IsDefeated && e.NextChar != '\0' && (typeBoss || !e.isBoss) && ViewportY(e) < TypeableViewportY;

    Enemy LowestTypeableEnemy()
    {
        Enemy best = null;
        float bestDist = float.MaxValue;
        foreach (Enemy e in Enemy.Active)
        {
            if (!Typeable(e)) continue;
            float d = e.DistanceToFortress;
            if (d < bestDist) { bestDist = d; best = e; }
        }
        return best;
    }

    static int EnemiesInView()
    {
        int n = 0;
        foreach (Enemy e in Enemy.Active)
            if (e != null && !e.IsDefeated && ViewportY(e) < TypeableViewportY) n++;
        return n;
    }

    static Enemy ActiveBoss()
    {
        foreach (Enemy e in Enemy.Active)
            if (e != null && e.isBoss && !e.IsDefeated) return e;
        return null;
    }

    // Slides an enemy down its lane until it sits at the given screen height.
    static void PlaceAtViewportY(Enemy e, float targetY)
    {
        Camera cam = Camera.main;
        if (cam == null || e == null) return;
        Transform t = e.transform;
        for (int i = 0; i < 80 && cam.WorldToViewportPoint(t.position).y > targetY; i++)
            t.position += new Vector3(0f, 0f, -0.2f);
    }

    // Spawns real wave enemies (same code path as the wave) straight into view,
    // so a beat starts with action instead of waiting for walkers.
    static void SpawnInView(int count, float minY, float maxY)
    {
        var wm = WaveManager.Instance;
        MethodInfo spawn = typeof(WaveManager).GetMethod("SpawnOne", Priv);
        if (wm == null || spawn == null) { Debug.LogWarning("[PromoDirector] SpawnOne not found"); return; }
        for (int i = 0; i < count; i++)
        {
            int before = Enemy.Active.Count;
            spawn.Invoke(wm, new object[] { 0f, wm.CurrentWaveNumber });
            if (Enemy.Active.Count > before)
                PlaceAtViewportY(Enemy.Active[Enemy.Active.Count - 1], Mathf.Lerp(maxY, minY, (i + 0.5f) / count));
        }
    }

    // Runs the game fast and off-camera until something worth filming happens.
    IEnumerator FastForwardUntil(Func<bool> condition, float maxVideoSeconds)
    {
        bool wasRecording = recording;
        float rate = typeRate;
        recording = false;
        typeRate = 0f;
        float prevScale = Time.timeScale;
        if (Time.timeScale > 0f) Time.timeScale = 4f;
        yield return WaitUntilOrTimeout(condition, maxVideoSeconds);
        if (Time.timeScale > 0f) Time.timeScale = prevScale > 0f ? prevScale : 1f;
        typeRate = rate;
        recording = wasRecording;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    // Waits in VIDEO time (frames), so pacing is exact regardless of timeScale.
    static IEnumerator Wait(float seconds)
    {
        int frames = Mathf.Max(1, Mathf.RoundToInt(seconds * Fps));
        for (int i = 0; i < frames; i++) yield return null;
    }

    static IEnumerator WaitUntilOrTimeout(Func<bool> condition, float timeoutSeconds)
    {
        int frames = Mathf.RoundToInt(timeoutSeconds * Fps);
        for (int i = 0; i < frames && !condition(); i++) yield return null;
    }

    static readonly BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    static void SetPrivate(object target, string field, object value)
    {
        FieldInfo f = target.GetType().GetField(field, Priv);
        if (f != null) f.SetValue(target, value);
        else Debug.LogWarning($"[PromoDirector] Field {field} not found on {target.GetType().Name}");
    }

    static bool WaveIsActive()
    {
        var wm = WaveManager.Instance;
        if (wm == null) return false;
        FieldInfo f = typeof(WaveManager).GetField("waveActive", Priv);
        return f != null && (bool)f.GetValue(wm);
    }

    static void JumpToWave(int waveNumber)
    {
        if (WaveManager.Instance != null)
            SetPrivate(WaveManager.Instance, "debugJumpToWaveIndex", (int?)(waveNumber - 1));
    }

    static Button AbilityButton(string upgradeId)
    {
        var reg = AbilityUIRegistry.Instance;
        if (reg == null) return null;
        foreach (AbilityUISlot slot in reg.slots)
            if (slot != null && slot.upgrade != null && slot.uiParent != null &&
                string.Equals(slot.upgrade.id, upgradeId, StringComparison.OrdinalIgnoreCase))
                return slot.uiParent.GetComponentInChildren<Button>(true);
        return null;
    }

    void Mark(string what) => Debug.Log($"[PromoTimeline] {framesWritten / (float)Fps:F1}s {what}");

    void Fail(string message)
    {
        LastError = message;
        Debug.LogError("[PromoDirector] " + message);
    }

    // ------------------------------------------------------------------
    // The script
    // ------------------------------------------------------------------
    IEnumerator Start()
    {
        Time.captureFramerate = Fps;
        BuildOverlay();
        StartCoroutine(CaptureLoop());
        IEnumerator show = Show();
        while (true)
        {
            object current;
            try
            {
                if (!show.MoveNext()) break;
                current = show.Current;
            }
            catch (Exception e)
            {
                Fail(e.ToString());
                break;
            }
            yield return current;
        }
        Finish();
    }

    void Finish()
    {
        StopEncoder();
        typeRate = 0f;
        GameManager.DebugGodMode = false;
        Time.captureFramerate = 0;
        IsDone = true;
        EditorApplication.isPlaying = false;
    }

    IEnumerator Show()
    {
        // Main menu is up (Play Mode is entered from MainMenu).
        yield return Wait(1.0f);

        StartEncoder();
        recording = true;

        // 1) Title: the real main menu with its logo.
        StartCoroutine(Fade(0f, 0.5f));
        yield return Wait(0.3f);
        Caption("", "A TYPING TOWER-DEFENSE GAME", 2.6f, bottom: true);
        yield return Wait(2.8f);
        yield return Fade(1f, 0.35f);
        recording = false;

        // 2) Into a run. Wave 1 is skipped off-camera and we resume on wave 3.
        SaveManager.IsContinuing = false;
        SceneManager.LoadScene("GameScene");
        yield return WaitUntilOrTimeout(() => WaveManager.Instance != null && GameManager.Instance != null && UpgradeManager.Instance != null, 20f);
        if (WaveManager.Instance == null) { Fail("GameScene did not load"); yield break; }
        yield return null;

        var wm = WaveManager.Instance;
        var gm = GameManager.Instance;
        var um = UpgradeManager.Instance;
        GameManager.DebugGodMode = true;
        wm.announceTime = 1.2f;
        wm.breakTime = 0f;
        if (wm.waves != null)
            foreach (Wave w in wm.waves) if (w != null && w.breakTimeOverride > 0f) w.breakTimeOverride = 0f;
        um.enableDraft = false;
        gm.AddCoins(400);

        JumpToWave(3);
        SetPrivate(wm, "debugSkipRequested", true);
        yield return WaitUntilOrTimeout(() => wm.CurrentWaveNumber == 3, 20f);
        um.enableDraft = true;
        yield return WaitUntilOrTimeout(WaveIsActive, 8f);

        // 3) Core loop: open on a busy field, type words, build combos.
        SpawnInView(3, 0.48f, 0.72f);
        yield return null;
        recording = true;
        Mark("wave 3 on screen, enemies in view=" + EnemiesInView());
        StartCoroutine(Fade(0f, 0.3f));
        typeRate = 5.5f;
        Caption("TYPE TO ATTACK", "FINISH A WORD TO DESTROY ITS ENEMY", 3.6f);
        yield return Wait(3.8f);
        if (EnemiesInView() < 2) SpawnInView(2, 0.55f, 0.72f);
        Caption("BUILD COMBOS", "PERFECT WORDS EARN MORE COINS", 3.4f);
        yield return Wait(3.0f);
        SetPrivate(wm, "debugSkipRequested", true); // no more spawns; clear what's on screen
        typeRate = 12f;

        // 4) Upgrade draft between waves (the game's own title says "Choose an upgrade").
        yield return FastForwardUntil(() => um.IsDraftOpen || EnemiesInView() == 0, 20f);
        if (!um.IsDraftOpen)
        {
            WaveManager.DebugKillAllEnemies();
            yield return FastForwardUntil(() => um.IsDraftOpen, 10f);
        }
        Mark("draft open=" + um.IsDraftOpen);
        typeRate = 0f;
        yield return Wait(0.2f);
        Caption("CHOOSE A POWER", "EVERY WAVE MAKES YOU STRONGER", 2.6f, bottom: true);
        yield return Wait(2.2f);

        // Unlock every ability for the showcase and queue the boss wave BEFORE
        // the pick, since the wave loop moves on in the same frame as the pick.
        if (um.pool != null)
            foreach (UpgradeDefinition def in um.pool.upgrades)
                if (def != null) um.DebugSetLevel(def, 3);
        JumpToWave(5);
        if (um.IsDraftOpen && um.draftUI != null && um.draftUI.cardGridRoot != null)
        {
            UpgradeCardUI card = um.draftUI.cardGridRoot.GetComponentInChildren<UpgradeCardUI>();
            if (card != null && card.pickButton != null) { TapRing(card.pickButton); card.pickButton.onClick.Invoke(); }
        }
        yield return Wait(0.4f);
        um.enableDraft = false; // the boss wave ends the video's gameplay; no draft after it

        // 5) Boss wave: skip the walk-in, open with the boss looming mid-screen.
        yield return FastForwardUntil(() => ActiveBoss() != null, 30f);
        Enemy boss = ActiveBoss();
        Mark("wave " + wm.CurrentWaveNumber + " boss=" + (boss != null));
        if (boss != null) PlaceAtViewportY(boss, 0.62f);
        SpawnInView(2, 0.45f, 0.66f);
        yield return null;
        recording = true;
        typeBoss = false;
        typeRate = 3.5f;
        Caption("FACE GIANT BOSSES", "AND THEIR SWARMING MINIONS", 3.0f);
        yield return Wait(3.0f);

        // Chronos: slow time with a crowd on screen.
        SpawnInView(3, 0.42f, 0.7f);
        var ts = TimeSinkManager.Instance;
        if (ts != null)
        {
            ts.DebugFillCharge();
            Button b = AbilityButton("slowmo");
            TapRing(b);
            ts.Activate();
            SfxPlayer.PlayGameStart();
            if (b != null) UIToast.ShowAt(b.transform, "Chronos Activated!", Color.cyan);
            Mark("chronos active=" + ts.IsActive);
        }
        typeRate = 5f;
        Caption("CHRONOS", "SLOW DOWN TIME", 3.4f);
        yield return Wait(3.6f);

        // Aegis: shield up while minions crash into it.
        typeRate = 0f;
        GameManager.DebugGodMode = false;
        if (gm.coins < 200) gm.AddCoins(200);
        SpawnInView(3, 0.36f, 0.46f);
        ShieldButton shield = FindAnyObjectByType<ShieldButton>();
        if (shield != null) { TapRing(shield); shield.Buy(); }
        Mark("shield active=" + (ShieldManager.Instance != null && ShieldManager.Instance.IsActive));
        Caption("AEGIS", "RAISE A SHIELD", 3.4f);
        yield return Wait(3.6f);

        // Field Repair: a heavy hit cracks the shield and walls, then repair.
        int shieldLeft = ShieldManager.Instance != null ? ShieldManager.Instance.Current : 0;
        gm.DamageFortress(shieldLeft + Mathf.RoundToInt(gm.maxHealth * 0.4f), wm.fortress != null ? wm.fortress.position : Vector3.zero);
        yield return Wait(0.5f);
        GameManager.DebugGodMode = true;
        Caption("FIELD REPAIR", "HEAL YOUR FORTRESS", 3.0f);
        UpgradeButton repair = null;
        foreach (UpgradeButton ub in FindObjectsByType<UpgradeButton>(FindObjectsInactive.Exclude))
            if (ub.type == UpgradeButton.UpgradeType.RepairFortress) { repair = ub; break; }
        if (repair != null)
        {
            for (int i = 0; i < 3 && gm.currentHealth < gm.maxHealth; i++)
            {
                if (gm.coins < repair.cost) gm.AddCoins(repair.cost * 2);
                TapRing(repair);
                repair.Purchase();
                yield return Wait(0.7f);
            }
        }
        Mark("health after repair=" + gm.currentHealth + "/" + gm.maxHealth);
        typeRate = 6f;
        yield return Wait(1.0f);

        // Take the boss down.
        typeBoss = true;
        typeRate = 10f;
        boss = ActiveBoss();
        if (boss != null) PlaceAtViewportY(boss, 0.6f);
        yield return WaitUntilOrTimeout(() => ActiveBoss() == null, 6f);
        Mark("boss down=" + (ActiveBoss() == null));
        yield return Wait(0.8f);

        // Finale: a fresh horde, wiped out by Overheat.
        typeRate = 0f;
        wm.StopAllCoroutines(); // no more spawns, banners or drafts after this
        SpawnInView(7, 0.36f, 0.72f);
        yield return Wait(0.7f);
        var cm = ComboManager.Instance;
        if (cm != null)
        {
            cm.DebugFillOverload();
            Button b = AbilityButton("overload");
            TapRing(b);
            int before = EnemiesInView();
            cm.TriggerOverload();
            SfxPlayer.PlayGameStart();
            if (b != null) UIToast.ShowAt(b.transform, "Overload Blast!", Color.yellow);
            Mark("overheat cleared " + before + " -> " + EnemiesInView());
        }
        Caption("OVERHEAT", "BLAST EVERY ENEMY AT ONCE", 3.0f);
        yield return Wait(3.0f);
        yield return Fade(1f, 0.35f);
        recording = false;

        // 6) Shop montage back on the main menu.
        GameManager.DebugGodMode = false;
        Time.timeScale = 1f;
        PlayerPrefs.SetInt(MainMenu.GamesPlayedSinceInterstitialKey, 0); // keep the menu interstitial out of the shot
        SceneManager.LoadScene("MainMenu");
        yield return WaitUntilOrTimeout(() => FindAnyObjectByType<MainMenu>() != null, 20f);
        yield return Wait(0.5f);
        GameObject shopPanel = null;
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == "ShopPanel" && t.GetComponentInChildren<ShopUI>(true) != null) { shopPanel = t.gameObject; break; }
        ShopUI shopUI = shopPanel != null ? shopPanel.GetComponentInChildren<ShopUI>(true) : null;
        if (shopPanel != null) shopPanel.SetActive(true);
        yield return Wait(0.3f);

        recording = true;
        yield return Fade(0f, 0.3f);
        Caption("UNLOCK SKINS", "TURRETS, MAPS AND WORD PACKS", 4.4f, bottom: true);
        yield return Wait(1.4f);
        PressShopTab(shopUI, 1);
        yield return Wait(1.4f);
        PressShopTab(shopUI, 2);
        yield return Wait(1.6f);
        if (shopPanel != null) shopPanel.SetActive(false);

        // 7) End card on the menu logo.
        Caption("PLAY LEXORA NOW", "HOW LONG CAN YOU HOLD THE LINE?", 3.2f, bottom: true);
        yield return Wait(3.0f);
        yield return Fade(1f, 0.4f);
        yield return Wait(0.2f);
    }

    void PressShopTab(ShopUI shop, int index)
    {
        if (shop == null || shop.categoryListRoot == null) return;
        var tabs = new List<Button>();
        foreach (Transform child in shop.categoryListRoot)
        {
            if (!child.gameObject.activeInHierarchy) continue;
            Button b = child.GetComponent<Button>();
            if (b != null) tabs.Add(b);
        }
        if (index < 0 || index >= tabs.Count) return;
        TapRing(tabs[index]);
        tabs[index].onClick.Invoke();
    }

    void OnDestroy()
    {
        if (encoder != null) StopEncoder();
        Time.captureFramerate = 0;
    }
}
#endif
