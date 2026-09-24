using System;
using UnityEngine;
#if UNITY_WEBGL
using Playgama;
using Playgama.Modules.Advertisement;
#endif

public class PlayGamaAds : MonoBehaviour
{
    private static PlayGamaAds _instance;
    public static PlayGamaAds Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<PlayGamaAds>();
                if (_instance == null)
                {
                    var go = new GameObject("[PlayGamaAds]");
                    _instance = go.AddComponent<PlayGamaAds>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (_instance != null) return;
        _instance = FindAnyObjectByType<PlayGamaAds>();
        if (_instance == null)
        {
            var go = new GameObject("[PlayGamaAds]");
            _instance = go.AddComponent<PlayGamaAds>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // =========================================================================
    // SIMULATED AD RUNTIME STATE (Active in Editor and Fallback modes)
    // =========================================================================
    private bool _isShowingSimulatedAd = false;
    private bool _isSimulatedRewarded = false;
    private string _simulatedAdTitle = "";
    private float _simulatedAdDuration = 5f;
    private float _simulatedAdTimeRemaining = 0f;
    private Action<bool> _simulatedAdCallback = null;
    private bool _wasAudioMutedBeforeAd = false;

    private void Update()
    {
        if (_isShowingSimulatedAd)
        {
            if (_simulatedAdTimeRemaining > 0f)
            {
                // CRITICAL: UnscaledDeltaTime allows countdown to progress even when Game Over pauses Time.timeScale
                _simulatedAdTimeRemaining -= Time.unscaledDeltaTime;
                if (_simulatedAdTimeRemaining <= 0f)
                {
                    _simulatedAdTimeRemaining = 0f;
                    if (!_isSimulatedRewarded)
                    {
                        CompleteSimulatedAd(true);
                    }
                }
            }
        }
    }

    private void StartSimulatedAd(bool isRewarded, string title, Action<bool> callback)
    {
        _isShowingSimulatedAd = true;
        _isSimulatedRewarded = isRewarded;
        _simulatedAdTitle = title;
        _simulatedAdDuration = isRewarded ? 5f : 3f;
        _simulatedAdTimeRemaining = _simulatedAdDuration;
        _simulatedAdCallback = callback;
        _wasAudioMutedBeforeAd = AudioListener.pause;
        AudioListener.pause = true;

        Debug.Log($"[PlayGamaAds] Started simulated ad: {title} (duration: {_simulatedAdDuration}s)");
    }

    private void CompleteSimulatedAd(bool success)
    {
        _isShowingSimulatedAd = false;
        AudioListener.pause = _wasAudioMutedBeforeAd;

        Action<bool> cb = _simulatedAdCallback;
        _simulatedAdCallback = null;

        if (success)
        {
            Debug.Log("[PlayGamaAds] Simulated ad completed successfully. Granting reward.");
        }
        else
        {
            Debug.LogWarning("[PlayGamaAds] Simulated ad was skipped or closed early. No reward granted.");
        }

        cb?.Invoke(success);
    }

    private void OnGUI()
    {
        if (!_isShowingSimulatedAd) return;

        // Dark dim backdrop over entire screen
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float boxWidth = Mathf.Min(520f, Screen.width - 40f);
        float boxHeight = 280f;
        float x = (Screen.width - boxWidth) * 0.5f;
        float y = (Screen.height - boxHeight) * 0.5f;

        GUI.Box(new Rect(x, y, boxWidth, boxHeight), GUIContent.none);
        GUILayout.BeginArea(new Rect(x + 20, y + 16, boxWidth - 40, boxHeight - 32));

        var headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        headerStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);

        GUILayout.Label(_isSimulatedRewarded ? "PLAYGAMA REWARDED AD (SIMULATOR)" : "PLAYGAMA INTERSTITIAL AD (SIMULATOR)", headerStyle);
        GUILayout.Space(8);

        var subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        GUILayout.Label(_simulatedAdTitle, subStyle);
        GUILayout.Space(12);

        // Visual progress bar
        float progress = Mathf.Clamp01(1f - (_simulatedAdTimeRemaining / _simulatedAdDuration));
        int percent = Mathf.RoundToInt(progress * 100f);

        Rect progressRect = GUILayoutUtility.GetRect(boxWidth - 40, 24);
        GUI.Box(progressRect, GUIContent.none);
        Rect fillRect = new Rect(progressRect.x + 2, progressRect.y + 2, (progressRect.width - 4) * progress, progressRect.height - 4);
        GUI.color = new Color(0.2f, 0.7f, 1f);
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var timerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        if (_simulatedAdTimeRemaining > 0f)
        {
            GUILayout.Label($"Simulating Ad Playback... {Mathf.CeilToInt(_simulatedAdTimeRemaining)}s remaining ({percent}%)", timerStyle);
        }
        else
        {
            timerStyle.normal.textColor = new Color(0.3f, 1f, 0.4f);
            GUILayout.Label("Ad Complete! You can now claim your reward.", timerStyle);
        }

        GUILayout.Space(16);

        GUILayout.BeginHorizontal();

        if (_isSimulatedRewarded)
        {
            if (_simulatedAdTimeRemaining <= 0f)
            {
                GUI.backgroundColor = new Color(0.2f, 0.85f, 0.3f);
                if (GUILayout.Button("CLAIM REWARD\n(Close Ad)", GUILayout.Height(48)))
                {
                    CompleteSimulatedAd(true);
                }
            }
            else
            {
                GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f);
                if (GUILayout.Button("SKIP AD\n(Close Early - No Reward)", GUILayout.Height(48)))
                {
                    CompleteSimulatedAd(false);
                }

                GUILayout.Space(10);

                GUI.backgroundColor = new Color(0.3f, 0.6f, 0.9f);
                if (GUILayout.Button("FAST COMPLETE\n(Dev Test Only)", GUILayout.Height(48)))
                {
                    CompleteSimulatedAd(true);
                }
            }
        }
        else
        {
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("CLOSE AD", GUILayout.Height(48)))
            {
                CompleteSimulatedAd(true);
            }
        }

        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    // =========================================================================
    // PUBLIC AD APIS
    // =========================================================================

    public bool IsInterstitialSupported()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            return Bridge.advertisement != null && Bridge.advertisement.isInterstitialSupported;
        }
        catch
        {
            return false;
        }
#else
        return true;
#endif
    }

    public bool IsRewardedSupported()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            return Bridge.advertisement != null && Bridge.advertisement.isRewardedSupported;
        }
        catch
        {
            return false;
        }
#else
        return true;
#endif
    }

    public void ShowInterstitial(Action<bool> onComplete = null)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            if (Bridge.advertisement != null && Bridge.advertisement.isInterstitialSupported)
            {
                Action<InterstitialState> handler = null;
                bool wasAudioMuted = AudioListener.pause;
                AudioListener.pause = true;

                handler = state =>
                {
                    if (state == InterstitialState.Closed)
                    {
                        Bridge.advertisement.interstitialStateChanged -= handler;
                        AudioListener.pause = wasAudioMuted;
                        onComplete?.Invoke(true);
                    }
                    else if (state == InterstitialState.Failed)
                    {
                        Bridge.advertisement.interstitialStateChanged -= handler;
                        AudioListener.pause = wasAudioMuted;
                        onComplete?.Invoke(false);
                    }
                };
                Bridge.advertisement.interstitialStateChanged += handler;
                Bridge.advertisement.ShowInterstitial();
                return;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Playgama Interstitial failed: {e}");
        }
#endif
        StartSimulatedAd(false, "Simulating Interstitial Ad", onComplete);
    }

    public void ShowRewarded(Action<bool> onRewarded = null, string adTitle = "Simulating Rewarded Video Ad")
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            if (Bridge.advertisement != null && Bridge.advertisement.isRewardedSupported)
            {
                Action<RewardedState> handler = null;
                bool rewarded = false;
                bool wasAudioMuted = AudioListener.pause;
                AudioListener.pause = true;

                handler = state =>
                {
                    if (state == RewardedState.Rewarded)
                    {
                        rewarded = true;
                    }
                    else if (state == RewardedState.Closed)
                    {
                        Bridge.advertisement.rewardedStateChanged -= handler;
                        AudioListener.pause = wasAudioMuted;
                        if (rewarded)
                            Debug.Log("[PlayGamaAds] Rewarded ad completed successfully.");
                        onRewarded?.Invoke(rewarded);
                    }
                    else if (state == RewardedState.Failed)
                    {
                        Bridge.advertisement.rewardedStateChanged -= handler;
                        AudioListener.pause = wasAudioMuted;
                        onRewarded?.Invoke(false);
                    }
                };
                Bridge.advertisement.rewardedStateChanged += handler;
                Bridge.advertisement.ShowRewarded();
                return;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Playgama Rewarded failed: {e}");
        }
#endif
        StartSimulatedAd(true, adTitle, onRewarded);
    }
}
