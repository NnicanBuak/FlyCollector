using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum GameOutcome
{
    Escaped,
    WrongBugs,
    Timeout
}

public class GameSceneController : MonoBehaviour
{
    [Header("Scene flow")]
    [SerializeField] private string gameOverScene = "GameOver";

    [Header("References (auto if empty)")]
    [SerializeField] private GameTimer gameTimer;
    [SerializeField] private InventoryManager inventory;

    [Header("Bug quota source")]
    [Tooltip("Если включено — считаем жуков по инвентарю, иначе — по CaughtBugsRuntime")]
    [SerializeField] private bool useInventoryAsSource = true;

    [Tooltip("Если список не пуст — учитываем только эти ассеты как жуков")]
    [SerializeField] private List<Item> bugWhitelistItems = new List<Item>();

    [Tooltip("Если список пуст — считаем жуков по типу (например, Quest)")]
    [SerializeField] private ItemType bugItemType = ItemType.Quest;

    [Header("Behaviour")]
    [Tooltip("Завершать игру сразу после открытия выхода")]
    [SerializeField] private bool finishWhenExitOpens = true;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    //УДАЛИТЬ!
    [SerializeField] private EditorInputBinder _editorInputBinder;
    //УДАЛИТЬ!

    private bool isFinished;

    void Awake()
    {
        if (!gameTimer) gameTimer = GameTimer.Instance;
        if (!inventory) inventory = InventoryManager.Instance;

        //УДАЛИТЬ!

        _editorInputBinder.Ended += FinisGames;

        //УДАЛИТЬ!
    }

    void OnEnable()
    {
        if (gameTimer) gameTimer.onTimerEnd.AddListener(OnTimerEnd);
        if (inventory) inventory.OnInventoryChanged.AddListener(HandleInventoryChanged);
    }

    void OnDisable()
    {
        if (gameTimer) gameTimer.onTimerEnd.RemoveListener(OnTimerEnd);
        if (inventory) inventory.OnInventoryChanged.RemoveListener(HandleInventoryChanged);
    }

    void HandleInventoryChanged()
    {
        CheckQuotaAndMaybeOpenExit();
    }

    void Start()
    {
        CheckQuotaAndMaybeOpenExit();
    }

    void OnTimerEnd()
    {
        if (showDebug) Debug.Log("[GameSceneController] ⏳ Timer ended → Timeout");
        FinishGame(GameOutcome.Timeout, overrideCaught: 0, wrongOverride: 0);
    }

    void OnInventoryChanged()
    {
        CheckQuotaAndMaybeOpenExit();
    }

    void OnExitOpened()
    {
        if (showDebug) Debug.Log("[GameSceneController] 🚪 Exit opened");
        if (finishWhenExitOpens) FinishUsingCurrentStats();
    }

    void CheckQuotaAndMaybeOpenExit()
    {
        int target = GetTargetCount();
        if (target <= 0) return;

        int caught = GetCaughtCount();
        if (showDebug) Debug.Log($"[GameSceneController] Quota check: caught={caught}, target={target}");
    }

    void FinishUsingCurrentStats()
    {
        int wrong = ComputeWrongCount();
        var outcome = (wrong > 0) ? GameOutcome.WrongBugs : GameOutcome.Escaped;
        FinishGame(outcome);
    }

//Удалить!!!
    void FinisGames(GameOutcome outcome)
    {
        if (outcome == GameOutcome.Escaped)
            FinishGame(outcome, 16, 0);

        if (outcome == GameOutcome.WrongBugs)
            FinishGame(outcome, 16, 16);

        if (outcome == GameOutcome.Timeout)
            FinishGame(outcome, 8, 0);
    }
//Удалить!!!

    void FinishGame(GameOutcome outcome, int? overrideCaught = null, int? wrongOverride = null)
    {
        if (isFinished) return;
        isFinished = true;

        int target = GetTargetCount();
        int caught = overrideCaught ?? GetCaughtCount();
        int wrong = wrongOverride ?? ComputeWrongCount();

        if (showDebug)
            Debug.Log($"[GameSceneController] Finish → {outcome} | caught={caught}, wrong={wrong}, target={target}");

        var gsm = GameSceneManager.Instance;
        if (gsm != null)
        {
            gsm.SetPersistentData("gameOutcome", outcome);
            gsm.SetPersistentData("totalCaught", caught);
            gsm.SetPersistentData("wrongCount", wrong);
        }

        GameSceneManager.Instance?.LoadScene(gameOverScene);
    }

    int GetTargetCount()
    {
        return TargetBugsRuntime.Instance ? TargetBugsRuntime.Instance.Targets.Count : 0;
    }

    int GetCaughtCount()
    {
        if (!useInventoryAsSource)
        {
            return CaughtBugsRuntime.Instance ? CaughtBugsRuntime.Instance.Caught.Count : 0;
        }

        if (inventory == null) return 0;

        int count = 0;
        var slots = inventory.GetAllItems();
        foreach (var s in slots)
        {
            if (s.item == null) continue;


            if (bugWhitelistItems != null && bugWhitelistItems.Count > 0)
            {
                if (bugWhitelistItems.Contains(s.item)) count += s.quantity;
            }
            else
            {
                if (s.item.itemType == bugItemType) count += s.quantity;
            }
        }

        return count;
    }

    int ComputeWrongCount()
    {
        if (TargetBugsRuntime.Instance && CaughtBugsRuntime.Instance)
        {
            var targetSet = new HashSet<string>(
                TargetBugsRuntime.Instance.Targets.Select(TargetBugsRuntime.NormalizeKey)
            );

            int wrong = 0;
            foreach (var c in CaughtBugsRuntime.Instance.Caught)
            {
                var key = TargetBugsRuntime.NormalizeKey(c);
                if (!targetSet.Contains(key)) wrong++;
            }

            return wrong;
        }


        return 0;
    }
}