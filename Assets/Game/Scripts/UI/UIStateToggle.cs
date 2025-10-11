using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Controls visibility of UI objects for the predefined states Fail / Mismatch / Win.
/// Configure state names and target objects via the two parallel lists. The checkboxes
/// for Fail/Mismatch/Win appear only when the corresponding state name exists.
/// </summary>
public class UIStateToggle : MonoBehaviour
{
    private const string StateFail = "fail";
    private const string StateMismatch = "mismatch";
    private const string StateWin = "win";

    [Header("State Mapping")]
    [Tooltip("List of state names. Use Fail / Mismatch / Win.")]
    [SerializeField] private List<string> stateNames = new List<string>();

    [Tooltip("GameObjects controlled by the corresponding state name.")]
    [SerializeField] private List<GameObject> stateObjects = new List<GameObject>();

    [Header("Active States")]
    [SerializeField] private bool showFail;
    [SerializeField] private bool showMismatch;
    [SerializeField] private bool showWin;

    private readonly Dictionary<string, List<GameObject>> stateLookup = new();

    private void Awake()
    {
        RebuildLookup();
        ApplyStateVisibility();
    }

    private void OnValidate()
    {
        RebuildLookup();
        SyncActiveFlags();
        ApplyStateVisibility();
    }

    public void ApplyStateVisibility()
    {
        foreach (var kvp in stateLookup)
        {
            bool active = ShouldShow(kvp.Key);
            foreach (var obj in kvp.Value)
            {
                if (obj == null) continue;
                if (obj.activeSelf != active)
                    obj.SetActive(active);
            }
        }
    }

    public void SetStateFail(bool value)     { SetState(StateFail, value); }
    public void SetStateMismatch(bool value) { SetState(StateMismatch, value); }
    public void SetStateWin(bool value)      { SetState(StateWin, value); }

    public void SetExclusiveFail()     { SetExclusive(StateFail); }
    public void SetExclusiveMismatch() { SetExclusive(StateMismatch); }
    public void SetExclusiveWin()      { SetExclusive(StateWin); }

    public bool SupportsFail()     => stateLookup.ContainsKey(StateFail);
    public bool SupportsMismatch() => stateLookup.ContainsKey(StateMismatch);
    public bool SupportsWin()      => stateLookup.ContainsKey(StateWin);

    private void SetState(string state, bool value)
    {
        switch (state)
        {
            case StateFail:     showFail = value; break;
            case StateMismatch: showMismatch = value; break;
            case StateWin:      showWin = value; break;
        }
        ApplyStateVisibility();
    }

    private void SetExclusive(string state)
    {
        showFail = state == StateFail;
        showMismatch = state == StateMismatch;
        showWin = state == StateWin;
        ApplyStateVisibility();
    }

    private void RebuildLookup()
    {
        stateLookup.Clear();
        int count = Mathf.Min(stateNames.Count, stateObjects.Count);
        for (int i = 0; i < count; i++)
        {
            string key = Normalize(stateNames[i]);
            if (string.IsNullOrEmpty(key))
                continue;

            if (!stateLookup.TryGetValue(key, out var list))
            {
                list = new List<GameObject>();
                stateLookup.Add(key, list);
            }

            var go = stateObjects[i];
            if (go != null && !list.Contains(go))
                list.Add(go);
        }
    }

    private void SyncActiveFlags()
    {
        if (!SupportsFail()) showFail = false;
        if (!SupportsMismatch()) showMismatch = false;
        if (!SupportsWin()) showWin = false;
    }

    private bool ShouldShow(string state)
    {
        return state switch
        {
            StateFail => showFail,
            StateMismatch => showMismatch,
            StateWin => showWin,
            _ => false
        };
    }

    private static string Normalize(string state)
    {
        return string.IsNullOrWhiteSpace(state) ? string.Empty : state.Trim().ToLowerInvariant();
    }
}
