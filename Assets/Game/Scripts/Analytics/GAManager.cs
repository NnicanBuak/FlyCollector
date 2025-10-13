using System.Collections.Generic;
using UnityEngine;
using GameAnalyticsSDK;

/// <summary>
/// Universal GameAnalytics manager for tracking all game events
/// Thread-safe singleton, persists across scenes
/// </summary>
public sealed class GAManager : MonoBehaviour
{
    private static GAManager _instance;
    public static GAManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[GAManager]");
                _instance = go.AddComponent<GAManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool enableAnalytics = true;

    // Track first-time events per session
    private HashSet<string> _firstTimeEvents = new HashSet<string>();

    // Focus time tracking (optional feature)
    private Dictionary<string, float> _focusTimeAccumulator = new Dictionary<string, float>();
    private string _currentFocusObject;
    private float _focusStartTime;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (enableAnalytics)
        {
            GameAnalytics.Initialize();
            LogDebug("GameAnalytics initialized");
        }
    }

    void OnApplicationQuit()
    {
        TrackApplicationQuit();

        // Send accumulated focus time data
        if (_focusTimeAccumulator.Count > 0)
        {
            SendFocusTimeSummary();
        }
    }

    #region Progression Events

    /// <summary>
    /// Track scene entry (level start)
    /// </summary>
    public void TrackSceneEnter(string sceneName)
    {
        if (!enableAnalytics) return;

        GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, "scene", sceneName);
        LogDebug($"[Progression] Scene Enter: {sceneName}");
    }

    /// <summary>
    /// Track game timer start (gameplay begins)
    /// </summary>
    public void TrackGameStart()
    {
        if (!enableAnalytics) return;

        GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, "gameplay", "timer_started");
        LogDebug("[Progression] Game Started (Timer)");
    }

    /// <summary>
    /// Track game over with results
    /// </summary>
    public void TrackGameOver(GameOutcome outcome, int caughtBugs, int wrongBugs, int targetBugs, float timeRemaining)
    {
        if (!enableAnalytics) return;

        string outcomeStr = outcome.ToString();
        GameAnalytics.NewProgressionEvent(
            outcome == GameOutcome.Victory ? GAProgressionStatus.Complete : GAProgressionStatus.Fail,
            "gameplay",
            "game_over",
            outcomeStr
        );

        // Additional design events for detailed stats
        GameAnalytics.NewDesignEvent($"GameOver:{outcomeStr}:CaughtBugs", caughtBugs);
        GameAnalytics.NewDesignEvent($"GameOver:{outcomeStr}:WrongBugs", wrongBugs);
        GameAnalytics.NewDesignEvent($"GameOver:{outcomeStr}:TimeRemaining", timeRemaining);

        LogDebug($"[Progression] Game Over: {outcomeStr} | Caught: {caughtBugs}/{targetBugs}, Wrong: {wrongBugs}, Time: {timeRemaining:F1}s");
    }

    /// <summary>
    /// Track credits screen reached
    /// </summary>
    public void TrackCreditsReached()
    {
        if (!enableAnalytics) return;

        GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, "flow", "credits");
        LogDebug("[Progression] Credits Reached");
    }

    #endregion

    #region First-Time Interaction Events

    /// <summary>
    /// Track first interaction/inspect with any object (one-time per object type)
    /// Returns true if this is the first time
    /// </summary>
    public bool TrackFirstInteraction(string objectType, string actionType = "interact")
    {
        if (!enableAnalytics) return false;

        string eventKey = $"{actionType}:{objectType}";

        if (_firstTimeEvents.Add(eventKey)) // Returns true if added (first time)
        {
            GameAnalytics.NewDesignEvent($"FirstTime:{actionType.ToUpper()}:{objectType}");
            LogDebug($"[FirstTime] {actionType} with {objectType}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Track first bug pickup (bring to camera for inspection)
    /// </summary>
    public void TrackFirstBugPickup(string bugType)
    {
        TrackFirstInteraction(bugType, "pickup");
    }

    /// <summary>
    /// Track first bug caught (sealed in jar)
    /// </summary>
    public void TrackFirstBugCaught(string bugType)
    {
        TrackFirstInteraction(bugType, "catch");
    }

    /// <summary>
    /// Track screwdriver pickup (first tool pickup)
    /// </summary>
    public void TrackScrewdriverPickup()
    {
        TrackFirstInteraction("screwdriver", "pickup");
    }

    #endregion

    #region Repeatable Interaction Events

    /// <summary>
    /// Track light switch toggle
    /// </summary>
    public void TrackLightToggle(bool isOn)
    {
        if (!enableAnalytics) return;

        string state = isOn ? "on" : "off";
        GameAnalytics.NewDesignEvent($"Interaction:Light:{state}");
        LogDebug($"[Interaction] Light: {state}");
    }

    /// <summary>
    /// Track ventilation interaction attempts
    /// </summary>
    public void TrackVentilationInteraction(bool success, string reason = "")
    {
        if (!enableAnalytics) return;

        if (success)
        {
            GameAnalytics.NewDesignEvent("Interaction:Ventilation:Success");
            LogDebug("[Interaction] Ventilation: Success");
        }
        else
        {
            string eventName = string.IsNullOrEmpty(reason)
                ? "Interaction:Ventilation:Fail"
                : $"Interaction:Ventilation:Fail:{reason}";
            GameAnalytics.NewDesignEvent(eventName);
            LogDebug($"[Interaction] Ventilation: Fail ({reason})");
        }
    }

    /// <summary>
    /// Track bug caught (repeatable - counts each bug)
    /// </summary>
    public void TrackBugCaught(string bugType)
    {
        if (!enableAnalytics) return;

        GameAnalytics.NewDesignEvent($"Bug:Caught:{bugType}");
        LogDebug($"[Bug] Caught: {bugType}");
    }

    /// <summary>
    /// Track milestone: all target bugs collected
    /// </summary>
    public void TrackAllBugsCollected(int totalCount, float timeElapsed)
    {
        if (!enableAnalytics) return;

        GameAnalytics.NewDesignEvent("Milestone:AllBugsCollected", totalCount);
        GameAnalytics.NewDesignEvent("Milestone:AllBugsCollected:Time", timeElapsed);
        LogDebug($"[Milestone] All Bugs Collected: {totalCount} bugs in {timeElapsed:F1}s");
    }

    #endregion

    #region Settings Events

    /// <summary>
    /// Track settings menu opened
    /// </summary>
    public void TrackSettingsOpened()
    {
        if (!enableAnalytics) return;

        GameAnalytics.NewDesignEvent("UI:Settings:Open");
        LogDebug("[UI] Settings Opened");
    }

    /// <summary>
    /// Track settings parameter changed
    /// </summary>
    public void TrackSettingChanged(string settingName, string value)
    {
        if (!enableAnalytics) return;

        GameAnalytics.NewDesignEvent($"Settings:{settingName}:{value}");
        LogDebug($"[Settings] {settingName} = {value}");
    }

    /// <summary>
    /// Track audio setting changed (common use case)
    /// </summary>
    public void TrackAudioSettingChanged(string audioType, bool enabled)
    {
        TrackSettingChanged($"Audio_{audioType}", enabled ? "On" : "Off");
    }

    #endregion

    #region Focus Time Tracking (Optional)

    /// <summary>
    /// Start tracking focus time on an object
    /// Call when camera enters focus mode
    /// </summary>
    public void StartFocusTracking(string objectName)
    {
        if (!enableAnalytics) return;

        // End previous focus if any
        if (!string.IsNullOrEmpty(_currentFocusObject))
        {
            EndFocusTracking();
        }

        _currentFocusObject = objectName;
        _focusStartTime = Time.realtimeSinceStartup;
        LogDebug($"[Focus] Started tracking: {objectName}");
    }

    /// <summary>
    /// End tracking focus time on current object
    /// Call when camera exits focus mode
    /// </summary>
    public void EndFocusTracking()
    {
        if (!enableAnalytics || string.IsNullOrEmpty(_currentFocusObject)) return;

        float duration = Time.realtimeSinceStartup - _focusStartTime;

        // Accumulate time for this object
        if (!_focusTimeAccumulator.ContainsKey(_currentFocusObject))
        {
            _focusTimeAccumulator[_currentFocusObject] = 0f;
        }
        _focusTimeAccumulator[_currentFocusObject] += duration;

        LogDebug($"[Focus] Ended tracking: {_currentFocusObject} | Duration: {duration:F2}s | Total: {_focusTimeAccumulator[_currentFocusObject]:F2}s");

        _currentFocusObject = null;
    }

    /// <summary>
    /// Send accumulated focus time data to analytics
    /// Called on application quit or manually when needed
    /// </summary>
    private void SendFocusTimeSummary()
    {
        if (!enableAnalytics) return;

        foreach (var kvp in _focusTimeAccumulator)
        {
            GameAnalytics.NewDesignEvent($"Focus:TotalTime:{kvp.Key}", kvp.Value);
            LogDebug($"[Focus Summary] {kvp.Key}: {kvp.Value:F2}s total");
        }
    }

    #endregion

    #region Application Lifecycle

    /// <summary>
    /// Track application quit
    /// </summary>
    public void TrackApplicationQuit()
    {
        if (!enableAnalytics) return;

        GameAnalytics.NewDesignEvent("Application:Quit");
        LogDebug("[Application] Quit");
    }

    #endregion

    #region Camera System Analytics

    /// <summary>
    /// Track camera mode transitions (Normal, Focus, Inspect)
    /// </summary>
    public void TrackCameraModeChange(string fromMode, string toMode)
    {
        if (!enableAnalytics) return;

        GameAnalytics.NewDesignEvent($"Camera:ModeChange:{fromMode}_to_{toMode}");
        LogDebug($"[Camera] Mode: {fromMode} → {toMode}");
    }

    /// <summary>
    /// Track nest level progression (key mechanic)
    /// </summary>
    public void TrackNestLevelChange(int fromLevel, int toLevel, string objectName)
    {
        if (!enableAnalytics) return;
        if (fromLevel == toLevel) return;

        GameAnalytics.NewDesignEvent($"Camera:NestLevel:{fromLevel}_to_{toLevel}", toLevel);
        if (!string.IsNullOrEmpty(objectName))
        {
            GameAnalytics.NewDesignEvent($"Camera:NestLevel:Object:{objectName}", toLevel);
        }
        LogDebug($"[Camera] Nest Level: {fromLevel} → {toLevel} (Object: {objectName})");
    }

    /// <summary>
    /// Track object hover duration (what players examine)
    /// Only tracks meaningful hovers (> 1 second)
    /// </summary>
    public void TrackObjectHover(string objectName, float hoverDuration)
    {
        if (!enableAnalytics) return;

        if (hoverDuration > 1f)
        {
            GameAnalytics.NewDesignEvent($"Camera:Hover:{objectName}", hoverDuration);
            LogDebug($"[Camera] Hover: {objectName} ({hoverDuration:F1}s)");
        }
    }

    #endregion

    #region Failed Interaction Analytics

    /// <summary>
    /// Track when conditions block interaction (helps identify stuck players)
    /// </summary>
    public void TrackInteractionBlocked(string objectName, string failedCondition)
    {
        if (!enableAnalytics) return;

        GameAnalytics.NewDesignEvent($"Interaction:Blocked:{objectName}:{failedCondition}");
        LogDebug($"[Interaction] Blocked: {objectName} (Reason: {failedCondition})");
    }

    /// <summary>
    /// Track when player tries to interact during freeze/animation
    /// </summary>
    public void TrackInteractionDuringFreeze(string attemptedAction)
    {
        if (!enableAnalytics) return;

        GameAnalytics.NewDesignEvent($"Interaction:Blocked:Freeze:{attemptedAction}");
        LogDebug($"[Interaction] Blocked during freeze: {attemptedAction}");
    }

    #endregion

    #region Bug Events

    /// <summary>
    /// Track bug escaped (timer ran out with bug still free)
    /// </summary>
    public void TrackBugEscaped(string bugType)
    {
        if (!enableAnalytics) return;

        GameAnalytics.NewDesignEvent($"Bug:Escaped:{bugType}");
        LogDebug($"[Bug] Escaped: {bugType}");
    }

    /// <summary>
    /// Track time to catch each bug (important performance metric)
    /// </summary>
    public void TrackBugCatchTime(string bugType, float timeSinceSpawn)
    {
        if (!enableAnalytics) return;

        GameAnalytics.NewDesignEvent($"Bug:CatchTime:{bugType}", timeSinceSpawn);
        LogDebug($"[Bug] Catch Time: {bugType} ({timeSinceSpawn:F1}s)");
    }

    #endregion

    #region Player Journey Funnel

    /// <summary>
    /// Track key progression points in player journey
    /// Examples: "ScrewdriverFound", "FirstScrewRemoved", "VentOpened",
    ///           "FirstBugSeen", "FirstBugCaught", "ExitRoomReached"
    /// </summary>
    public void TrackFunnelStep(string stepName)
    {
        if (!enableAnalytics) return;

        GameAnalytics.NewDesignEvent($"Funnel:{stepName}");
        LogDebug($"[Funnel] {stepName}");
    }

    #endregion

    #region Inventory Analytics

    /// <summary>
    /// Track when inventory is full and item cannot be added
    /// </summary>
    public void TrackInventoryFull(string attemptedItemType)
    {
        if (!enableAnalytics) return;

        GameAnalytics.NewDesignEvent($"Inventory:Full:Attempted:{attemptedItemType}");
        LogDebug($"[Inventory] Full - attempted to add: {attemptedItemType}");
    }

    /// <summary>
    /// Track when item is used from inventory
    /// </summary>
    public void TrackItemUsed(string itemID, string usedOn)
    {
        if (!enableAnalytics) return;

        GameAnalytics.NewDesignEvent($"Inventory:ItemUsed:{itemID}:On:{usedOn}");
        LogDebug($"[Inventory] Item Used: {itemID} on {usedOn}");
    }

    #endregion

    #region Error Events

    /// <summary>
    /// Track resource events (economy tracking)
    /// </summary>
    public void TrackResourceEvent(GameAnalyticsSDK.GAResourceFlowType flowType, string currency, float amount, string itemType, string itemId)
    {
        if (!enableAnalytics) return;

        GameAnalytics.NewResourceEvent(flowType, currency, amount, itemType, itemId);
        LogDebug($"[Resource] {flowType}: {amount} {currency} ({itemType}:{itemId})");
    }

    /// <summary>
    /// Track errors and exceptions
    /// </summary>
    public void TrackError(GameAnalyticsSDK.GAErrorSeverity severity, string message)
    {
        if (!enableAnalytics) return;

        GameAnalytics.NewErrorEvent(severity, message);
        LogDebug($"[Error] {severity}: {message}");
    }

    #endregion

    #region Utilities

    private void LogDebug(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[GAManager] {message}");
        }
    }

    /// <summary>
    /// Enable or disable analytics at runtime
    /// </summary>
    public void SetAnalyticsEnabled(bool enabled)
    {
        enableAnalytics = enabled;
        LogDebug($"Analytics {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Get accumulated focus time for an object
    /// </summary>
    public float GetFocusTime(string objectName)
    {
        return _focusTimeAccumulator.TryGetValue(objectName, out float time) ? time : 0f;
    }

    /// <summary>
    /// Reset first-time events tracking (useful for testing)
    /// </summary>
    public void ResetFirstTimeEvents()
    {
        _firstTimeEvents.Clear();
        LogDebug("First-time events reset");
    }

    #endregion
}
