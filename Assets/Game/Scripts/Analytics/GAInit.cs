using UnityEngine;
using GameAnalyticsSDK;

public class GAInit : MonoBehaviour
{
    void Start()
    {
        GameAnalytics.Initialize();
        GameAnalytics.NewDesignEvent("Test:DesignEvent:Test", 400);
    }
}
