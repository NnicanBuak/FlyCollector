using Game;
using UnityEngine;

public class EndGameResultUI : MonoBehaviour
{
    [Header("UI State Controller")]
    [SerializeField] private UIStateToggle stateToggle;

    [Header("State Names")]
    [SerializeField] private string victoryStateName = "Escape";
    [SerializeField] private string wrongBugsStateName = "Mismatch";
    [SerializeField] private string timeoutStateName = "Fail";

    [Header("Keys in GameSceneManager persistent data")]
    [SerializeField] private string outcomeKey = "gameOutcome";
    [SerializeField] private string totalCaughtKey = "totalCaught";
    [SerializeField] private string wrongCountKey = "wrongCount";

    private void Awake()
    {
        if (stateToggle == null)
            stateToggle = GetComponent<UIStateToggle>();
    }

    private void Start()
    {
        GameOutcome outcome = GameOutcome.Victory;
        
        var summary = BugSummaryUtil.Build(preferInventory: true);
        
        var gsm = GameSceneManager.Instance;
        if (gsm != null && gsm.HasPersistentData(outcomeKey))
        {
            outcome = gsm.GetPersistentData<GameOutcome>(outcomeKey, GameOutcome.Victory);
        }

        ApplyOutcome(outcome);
    }

    private void ApplyOutcome(GameOutcome outcome)
    {
        if (stateToggle == null)
        {
            Debug.LogWarning("[EndGameResultUI] UIStateToggle не назначен!");
            return;
        }

        string stateName = outcome switch
        {
            GameOutcome.Victory   => victoryStateName,
            GameOutcome.WrongBugs => wrongBugsStateName,
            GameOutcome.Timeout   => timeoutStateName,
            _ => victoryStateName
        };

        stateToggle.SetExclusive(stateName);
    }
}