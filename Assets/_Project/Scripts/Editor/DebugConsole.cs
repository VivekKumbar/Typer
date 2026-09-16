#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================================
// DEBUG CHEAT CONSOLE -- dev/testing only, see the RELEASE SAFETY note below.
// ============================================================================
// A hidden in-game panel for instantly testing functionality (skip cooldowns,
// add coins, jump waves, trigger abilities, ...) without waiting or replaying
// runs. Every command routes through the SAME real public methods the actual
// systems use (Wallet.Add, UpgradeManager.DebugSetLevel, SaveManager,
// DayNightCycle, Enemy.Defeat, ...) -- it never fakes a shortcut that could
// hide a real bug. A handful of systems (GameManager, WaveManager,
// UpgradeManager, ComboManager, TimeSinkManager, DayNightCycle, MainMenu)
// each got ONE small additive "DEBUG CONSOLE HOOK" public method/flag added
// where no existing public entry point could do the job -- none of them
// change any existing behavior.
//
// RELEASE SAFETY -- two independent layers:
//   1) This entire file is wrapped in #if UNITY_EDITOR || DEVELOPMENT_BUILD.
//      In a real release build (Development Build UNCHECKED in Build
//      Settings) this class doesn't exist at all -- the compiler strips it
//      out completely. Verify: Build Settings > confirm "Development Build"
//      is unchecked before a Play Store build.
//   2) EVEN inside a development build, the "Enable Debug Console" toggle on
//      this component (below) must ALSO be on. It defaults OFF. When off,
//      the DEBUG button is hidden (Start() calls debugButtonRoot.SetActive),
//      the backtick key does nothing, and the panel is never built -- flip
//      it on only on your own testing builds.
//
// Placed once as a scene object (MainMenu.unity), DontDestroyOnLoad, same
// pattern as AdsManager -- persists into GameScene automatically. The DEBUG
// button is a REAL child GameObject of this same object (see DebugButton in
// the Hierarchy under DebugConsole) -- not created at runtime -- with its
// Button.OnClick wired directly, in the Inspector, to this component's
// public TogglePanel() method. No gesture, no intermediate handler.
// ============================================================================
public class DebugConsole : MonoBehaviour
{
    public static DebugConsole Instance { get; private set; }

    [Header("Build safety")]
    [Tooltip("Master switch. Defaults OFF. When off, the DEBUG button is hidden, the backtick key does nothing, and no console UI is built -- this is a second safety layer on top of the UNITY_EDITOR || DEVELOPMENT_BUILD compile guard this whole file is wrapped in. Turn this ON only on your own testing builds.")]
    public bool enableDebugConsole = false;

    [Header("Activation button (real scene object, see DebugConsole child GameObjects)")]
    [Tooltip("The always-present DEBUG button's root GameObject -- a real child of this same DontDestroyOnLoad object, wired directly in the Inspector to call TogglePanel() below. Its active state is driven by Enable Debug Console: shown when on, hidden when off.")]
    public GameObject debugButtonRoot;

    const int LogLinesKept = 15;
    readonly List<string> logLines = new List<string>();

    bool consoleVisible;
    RectTransform panelRoot;
    TMP_InputField inputField;
    TMP_Text logText;
    ScrollRect logScroll;

    readonly Dictionary<string, Func<string, string>> commands = new Dictionary<string, Func<string, string>>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> commandSyntax = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Command registry built here (not just event subscriptions, but same
    // "wire things up in Start not OnEnable" spirit as the rest of the project).
    void Start()
    {
        // The DEBUG button is a real scene GameObject (always exists in the
        // Hierarchy, inspectable in Edit Mode) -- its ACTIVE state is what
        // the master switch controls, not its existence.
        if (debugButtonRoot != null) debugButtonRoot.SetActive(enableDebugConsole);

        if (!enableDebugConsole) return; // master switch off -- panel never built, key does nothing

        RegisterCommands();
        BuildUI();
        SetConsoleVisible(false);
    }

    void Update()
    {
        if (!enableDebugConsole) return;
        if (Input.GetKeyDown(KeyCode.BackQuote))
            SetConsoleVisible(!consoleVisible);
    }

    // The DEBUG button's OnClick is wired DIRECTLY to this in the Inspector
    // (see the DebugConsole > DebugButton GameObject's Button component) --
    // no gesture, no counters, no intermediate handler. One tap = toggle.
    public void TogglePanel() => SetConsoleVisible(!consoleVisible);

    void SetConsoleVisible(bool visible)
    {
        consoleVisible = visible;
        if (panelRoot != null) panelRoot.gameObject.SetActive(visible);
        if (visible)
        {
            Log("Debug console opened. Type 'listcommands' for the full list.");
            if (inputField != null) inputField.ActivateInputField();
        }
    }

    // ------------------------------------------------------------------
    // Command execution
    // ------------------------------------------------------------------
    void RunFromInputField()
    {
        if (inputField == null) return;
        string raw = inputField.text;
        inputField.text = "";
        RunLine(raw);
        inputField.ActivateInputField();
    }

    // Runs a full "command arg" line (used by the input field's Run button)
    // or just a bare command (used by the one-tap buttons, which pass their
    // own fixed arg separately via RunCommand).
    public void RunLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        line = line.Trim();
        int space = line.IndexOf(' ');
        string cmd = space < 0 ? line : line.Substring(0, space);
        string arg = space < 0 ? "" : line.Substring(space + 1).Trim();
        RunCommand(cmd, arg);
    }

    public void RunCommand(string cmd, string arg = "")
    {
        string display = string.IsNullOrEmpty(arg) ? cmd : $"{cmd} {arg}";

        if (!commands.TryGetValue(cmd, out var handler))
        {
            Log($"> {display} -- ERROR: unknown command. Try 'listcommands'.");
            return;
        }

        try
        {
            string result = handler(arg ?? "");
            Log($"> {display} -- OK: {result}");
        }
        catch (Exception e)
        {
            Log($"> {display} -- ERROR: {e.Message}");
        }
    }

    // Keeps only the last LogLinesKept entries, newest at the bottom,
    // auto-scrolling -- matches "last ~10-15 results" rather than an
    // unbounded growing log.
    void Log(string line)
    {
        Debug.Log("[DebugConsole] " + line);
        logLines.Add(line);
        while (logLines.Count > LogLinesKept) logLines.RemoveAt(0);

        if (logText == null) return;
        logText.text = string.Join("\n", logLines);
        Canvas.ForceUpdateCanvases();
        if (logScroll != null) logScroll.verticalNormalizedPosition = 0f; // snap to newest line
    }

    // ------------------------------------------------------------------
    // Command registry -- adding a new command later is one entry here.
    // Every handler calls into the SAME real systems the actual game uses.
    // ------------------------------------------------------------------
    void RegisterCommands()
    {
        Register("addcoins", "addcoins [amount=1000]", arg =>
        {
            int amount = ParseIntOrDefault(arg, 1000);
            Wallet.Add(amount);
            return $"Wallet +{amount} -> Total={Wallet.Total}";
        });

        Register("setcoins", "setcoins [amount]", arg =>
        {
            int amount = ParseIntOrDefault(arg, 0);
            Wallet.ResetWallet();
            if (amount > 0) Wallet.Add(amount);
            return $"Wallet set to {Wallet.Total}";
        });

        Register("resetadcooldown", "resetadcooldown", _ =>
        {
            MainMenu.DebugResetAdCooldown();
            return "Watch-Ad cooldown cleared.";
        });

        Register("forcereward", "forcereward", _ =>
        {
            MainMenu mm = FindMainMenu();
            if (mm == null) return ThrowNotFound("MainMenu (are you on the Main Menu scene?)");
            mm.DebugForceReward();
            return "Reward granted and cooldown started, as if a real ad was watched.";
        });

        Register("skipwave", "skipwave", _ =>
        {
            var wm = WaveManager.Instance;
            if (wm == null) return ThrowNotFound("WaveManager");
            wm.DebugSkipWave();
            return "Current wave abandoned -- advancing to the next.";
        });

        Register("setwave", "setwave [n]", arg =>
        {
            var wm = WaveManager.Instance;
            if (wm == null) return ThrowNotFound("WaveManager");
            if (!int.TryParse(arg, out int n) || n < 1)
                throw new ArgumentException("usage: setwave [n], n >= 1");
            wm.DebugSetWave(n);
            return $"Jumping to wave {n}.";
        });

        Register("godmode", "godmode [on/off]", arg =>
        {
            bool target = arg.Trim().ToLowerInvariant() switch
            {
                "on" => true,
                "off" => false,
                "" => !GameManager.DebugGodMode, // no arg -> toggle
                _ => throw new ArgumentException("usage: godmode [on/off]"),
            };
            GameManager.DebugGodMode = target;
            return target ? "ON -- fortress takes no damage." : "OFF -- normal damage resumed.";
        });

        Register("killall", "killall", _ =>
        {
            int count = Enemy.Active.Count(e => e != null && !e.IsDefeated);
            WaveManager.DebugKillAllEnemies();
            return $"Defeated {count} active enemies via the real Enemy.Defeat() path (coins/rewards apply normally).";
        });

        Register("unlockability", "unlockability [id]", arg => SetAbilityLevel(arg, 1));
        Register("maxability", "maxability [id]", arg => SetAbilityLevel(arg, UpgradeDefinition.BossLevel));

        Register("fillovercharge", "fillovercharge", _ =>
        {
            var cm = ComboManager.Instance;
            if (cm == null) return ThrowNotFound("ComboManager");
            cm.DebugFillOverload();
            return "Overload meter filled and ready.";
        });

        Register("filltimesink", "filltimesink", _ =>
        {
            var ts = TimeSinkManager.Instance;
            if (ts == null) return ThrowNotFound("TimeSinkManager");
            ts.DebugFillCharge();
            return "Time Sink charge filled and ready.";
        });

        Register("clearsave", "clearsave", _ =>
        {
            SaveManager.ClearSave();
            return "Save cleared -- Continue will no longer offer this run.";
        });

        Register("forcesave", "forcesave", _ =>
        {
            int wave = WaveManager.Instance != null ? WaveManager.Instance.CurrentWaveNumber : 1;
            SaveManager.CaptureAndSave(wave);
            return $"Saved at wave {wave}.";
        });

        Register("toggledaynight", "toggledaynight", _ =>
        {
            var dn = DayNightCycle.Instance;
            if (dn == null) return ThrowNotFound("DayNightCycle");
            dn.DebugToggleDayNight();
            return dn.IsNight ? "Now NIGHT." : "Now DAY.";
        });

        Register("listcommands", "listcommands", _ =>
        {
            foreach (var kv in commandSyntax.OrderBy(k => k.Key))
                Log("  " + kv.Value);
            return $"{commandSyntax.Count} commands listed above.";
        });
    }

    void Register(string name, string syntax, Func<string, string> handler)
    {
        commands[name] = handler;
        commandSyntax[name] = syntax;
    }

    string SetAbilityLevel(string id, int level)
    {
        var um = UpgradeManager.Instance;
        if (um == null) return ThrowNotFound("UpgradeManager");
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("usage: " + (level >= UpgradeDefinition.BossLevel ? "maxability" : "unlockability") + " [id] (e.g. shield, timesink, overload, repair)");

        UpgradeDefinition def = um.FindUpgradeById(id);
        if (def == null)
        {
            string known = um.pool != null && um.pool.upgrades != null
                ? string.Join(", ", um.pool.upgrades.Where(u => u != null).Select(u => u.id))
                : "(no pool assigned)";
            throw new ArgumentException($"no upgrade with id '{id}'. Known ids: {known}");
        }

        um.DebugSetLevel(def, level);
        return $"{def.id} set to level {level}.";
    }

    static MainMenu FindMainMenu() => UnityEngine.Object.FindFirstObjectByType<MainMenu>(FindObjectsInactive.Include);

    static int ParseIntOrDefault(string arg, int fallback) => int.TryParse(arg, out int v) ? v : fallback;

    static string ThrowNotFound(string what) => throw new InvalidOperationException($"{what} not found in the current scene.");

    // ========================================================================
    // RUNTIME UI CONSTRUCTION -- plain, functional styling (dev tool, not
    // player-facing). Built entirely in code: the scene only needs this one
    // GameObject+component, everything visual is generated at Start().
    //
    // Layout, top to bottom: title bar + [X] close -> input field + RUN ->
    // scrollable one-tap command grid -> scrolling log (last ~15 lines).
    // ========================================================================
    void BuildUI()
    {
        Canvas canvas = BuildCanvas();

        RectTransform root = CreatePanel(canvas.transform as RectTransform, "ConsolePanel", new Color(0f, 0f, 0f, 0.88f));
        Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        panelRoot = root;

        // Title bar: a 90px strip pinned to the top edge, stretched full width.
        RectTransform title = CreateText(root, "Title", "DEBUG CONSOLE", 36, TextAlignmentOptions.Left, Color.white);
        title.anchorMin = new Vector2(0f, 1f);
        title.anchorMax = new Vector2(1f, 1f);
        title.offsetMin = new Vector2(20f, -100f);
        title.offsetMax = new Vector2(-90f, -10f);

        // Close button: fixed 70x70, top-right corner -- explicit, visible
        // close affordance in addition to the corner toggle button.
        Button closeBtn = CreateButton(root, "CloseButton", "X", 32, out RectTransform closeRt);
        closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-15f, -15f);
        closeRt.sizeDelta = new Vector2(70f, 70f);
        closeBtn.onClick.AddListener(() => SetConsoleVisible(false));

        // Input row: field + Run button, a 70px strip just below the title.
        TMP_InputField field = BuildInputField(root, out RectTransform fieldRt);
        fieldRt.anchorMin = new Vector2(0f, 1f);
        fieldRt.anchorMax = new Vector2(0.78f, 1f);
        fieldRt.pivot = new Vector2(0f, 1f);
        fieldRt.offsetMin = new Vector2(20f, -180f);
        fieldRt.offsetMax = new Vector2(-10f, -110f);
        inputField = field;

        Button runBtn = CreateButton(root, "RunButton", "RUN", 28, out RectTransform runRt);
        runBtn.onClick.AddListener(RunFromInputField);
        field.onSubmit.AddListener(_ => RunFromInputField());
        runRt.anchorMin = new Vector2(0.78f, 1f);
        runRt.anchorMax = new Vector2(1f, 1f);
        runRt.pivot = new Vector2(0f, 1f);
        runRt.offsetMin = new Vector2(10f, -180f);
        runRt.offsetMax = new Vector2(-20f, -110f);

        // Scrolling log: fixed 380px-tall strip pinned to the BOTTOM of the
        // panel. anchorMin.y == anchorMax.y == 0 (a point anchor at the
        // bottom edge) means offsetMin.y/offsetMax.y directly ARE the
        // bottom/top edge distances from that anchor -- NOT sizeDelta, which
        // would silently fight these if set afterward, so it's deliberately
        // never touched here.
        RectTransform logArea = BuildScrollingLog(root);
        logArea.anchorMin = new Vector2(0f, 0f);
        logArea.anchorMax = new Vector2(1f, 0f);
        logArea.offsetMin = new Vector2(20f, 20f);
        logArea.offsetMax = new Vector2(-20f, 400f);

        // Scrollable one-tap command grid: fills everything between the
        // input row and the log.
        RectTransform grid = BuildQuickButtonGrid(root);
        grid.anchorMin = new Vector2(0f, 0f);
        grid.anchorMax = new Vector2(1f, 1f);
        grid.offsetMin = new Vector2(20f, 420f);   // above the log (log height 380 + 40 margin)
        grid.offsetMax = new Vector2(-20f, -190f); // below the input row
    }

    Canvas BuildCanvas()
    {
        var canvasGO = new GameObject("DebugConsoleCanvas", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760; // always on top

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); // portrait convention
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    RectTransform BuildScrollingLog(Transform parent)
    {
        RectTransform container = CreatePanel(parent, "LogScrollArea", new Color(1f, 1f, 1f, 0.08f));

        var scrollGO = container.gameObject;
        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollGO.AddComponent<RectMask2D>();

        RectTransform viewport = container; // the panel itself acts as the viewport
        RectTransform content = CreateText(viewport, "LogContent", "", 22, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.95f, 0.85f));
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(10f, 0f);
        content.offsetMax = new Vector2(-10f, 0f);

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.viewport = viewport;

        logText = content.GetComponent<TMP_Text>();
        logScroll = scrollRect;
        return container;
    }

    TMP_InputField BuildInputField(Transform parent, out RectTransform rootRt)
    {
        RectTransform root = CreatePanel(parent, "CommandInput", new Color(1f, 1f, 1f, 0.12f));
        rootRt = root;

        RectTransform textArea = CreatePanel(root, "TextArea", Color.clear);
        Stretch(textArea, new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), Vector2.zero, Vector2.zero);
        textArea.gameObject.AddComponent<RectMask2D>();

        RectTransform placeholder = CreateText(textArea, "Placeholder", "type a command...", 24, TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.4f));
        Stretch(placeholder, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        RectTransform text = CreateText(textArea, "Text", "", 24, TextAlignmentOptions.MidlineLeft, Color.white);
        Stretch(text, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        TMP_InputField field = root.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = textArea;
        field.textComponent = text.GetComponent<TMP_Text>();
        field.placeholder = placeholder.GetComponent<TMP_Text>();
        field.lineType = TMP_InputField.LineType.SingleLine;
        return field;
    }

    // Scrollable grid (2-3 columns depending on screen width) of one-tap
    // buttons for the most common test actions -- faster than typing on
    // mobile. Each just calls RunCommand with a fixed arg.
    RectTransform BuildQuickButtonGrid(Transform parent)
    {
        RectTransform container = CreatePanel(parent, "QuickButtonsScrollArea", Color.clear);
        var scrollRect = container.gameObject.AddComponent<ScrollRect>();
        container.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreatePanel(container, "QuickButtonsContent", Color.clear);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        var layout = content.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(320, 90);
        layout.spacing = new Vector2(10, 10);
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.UpperLeft;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Matches the exact "common command" list requested -- typing covers
        // every other registered command (setwave N, maxability, etc.).
        (string label, string cmd, string arg)[] quickActions =
        {
            ("Add 1000 Coins", "addcoins", "1000"),
            ("Reset Ad Cooldown", "resetadcooldown", ""),
            ("Force Reward", "forcereward", ""),
            ("Skip Wave", "skipwave", ""),
            ("Kill All", "killall", ""),
            ("God Mode Toggle", "godmode", ""),
            ("Unlock Shield", "unlockability", "shield"),
            ("Unlock TimeSink", "unlockability", "timesink"),
            ("Unlock Overload", "unlockability", "overload"),
            ("Unlock Repair", "unlockability", "repair"),
            ("Clear Save", "clearsave", ""),
            ("Toggle Day/Night", "toggledaynight", ""),
        };

        foreach (var (label, cmd, arg) in quickActions)
        {
            Button b = CreateButton(content, cmd + "_" + arg, label, 18, out _);
            b.onClick.AddListener(() => RunCommand(cmd, arg));
        }

        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.viewport = container;
        return container;
    }

    // ------------------------------------------------------------------
    // Small UI factory helpers -- plain functional styling only.
    // ------------------------------------------------------------------
    RectTransform CreatePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return go.GetComponent<RectTransform>();
    }

    RectTransform CreateText(Transform parent, string name, string text, float size, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        return go.GetComponent<RectTransform>();
    }

    Button CreateButton(Transform parent, string name, string label, float fontSize, out RectTransform rt)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.55f, 0.9f, 0.9f);
        var btn = go.AddComponent<Button>();
        rt = go.GetComponent<RectTransform>();

        if (!string.IsNullOrEmpty(label))
        {
            RectTransform labelRt = CreateText(rt, "Label", label, fontSize, TextAlignmentOptions.Center, Color.white);
            Stretch(labelRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }
        return btn;
    }

    static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}
#endif
