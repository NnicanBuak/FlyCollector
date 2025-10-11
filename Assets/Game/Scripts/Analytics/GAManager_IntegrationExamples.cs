using UnityEngine;
using Bug;

public class GAManager_IntegrationExamples
{
    void Example_GameSceneController_Start()
    {
        GAManager.Instance.TrackSceneEnter("Game");
    }


    void Example_GameOverController_Awake()
    {
        GAManager.Instance.TrackSceneEnter("GameOver");
    }


    void Example_CreditsController_Awake()
    {
        GAManager.Instance.TrackSceneEnter("Credits");
        GAManager.Instance.TrackCreditsReached();
    }


    void Example_GameTimer_OnTimerStart()
    {
        GAManager.Instance.TrackGameStart();
    }


    void Example_GameSceneController_FinishGame(GameOutcome outcome, int caught, int wrong, int target)
    {
        float timeRemaining = 0f;
        if (GameTimer.Instance != null)
        {
            timeRemaining = GameTimer.Instance.GetRemainingTime();
        }


        GAManager.Instance.TrackGameOver(outcome, caught, wrong, target, timeRemaining);
    }


    void Example_LightController_SetLightState(bool isOn)
    {
        GAManager.Instance.TrackLightToggle(isOn);
    }


    void Example_OnItemPickup(Item item)
    {
        if (item.itemID == "screwdriver")
        {
            GAManager.Instance.TrackScrewdriverPickup();
        }
    }


    void Example_VentilationGate_TryOpen()
    {
        bool hasAllScrews = CheckIfAllScrewsRemoved();

        if (hasAllScrews)
        {
            GAManager.Instance.TrackVentilationInteraction(success: true);
        }
        else
        {
            GAManager.Instance.TrackVentilationInteraction(success: false, reason: "screws_not_removed");
        }
    }

    private bool CheckIfAllScrewsRemoved()
    {
        return false;
    }


    void Example_BugJarTrap_OnBugCaught(string bugType)
    {
        bool isFirst = GAManager.Instance.TrackFirstInteraction(bugType, "catch");


        GAManager.Instance.TrackBugCaught(bugType);
    }


    void Example_OnBugInspect(GameObject bugObject)
    {
        BugAI bugAI = bugObject.GetComponent<BugAI>();
        if (bugAI != null)
        {
            string bugType = bugAI.GetBugType();
            GAManager.Instance.TrackFirstBugPickup(bugType);
        }
    }


    void Example_OnAllBugsCollected()
    {
        int totalBugs = GetCaughtCount();
        float timeElapsed = 0f;

        if (GameTimer.Instance != null)
        {
            timeElapsed = GameTimer.Instance.GetElapsedTime();
        }

        GAManager.Instance.TrackAllBugsCollected(totalBugs, timeElapsed);
    }

    private int GetCaughtCount()
    {
        return 0;
    }


    void Example_FocusSession_OnEnterFocus(string objectName)
    {
        GAManager.Instance.StartFocusTracking(objectName);
    }


    void Example_FocusSession_OnExitFocus()
    {
        GAManager.Instance.EndFocusTracking();
    }


    void Example_OnSettingsMenuOpen()
    {
        GAManager.Instance.TrackSettingsOpened();
    }


    void Example_OnAudioToggle(bool enabled)
    {
        GAManager.Instance.TrackAudioSettingChanged("Music", enabled);
    }


    void Example_OnSettingChanged(string settingName, string value)
    {
        GAManager.Instance.TrackSettingChanged(settingName, value);
    }


    void Example_OnFirstInteract(GameObject target, string actionType)
    {
        string objectName = target.name;
        bool isFirstTime = GAManager.Instance.TrackFirstInteraction(objectName, actionType);

        if (isFirstTime)
        {
            Debug.Log($"First time interacting with {objectName}!");
        }
    }
}