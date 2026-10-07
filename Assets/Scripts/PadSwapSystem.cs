using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PadSwapSystem : MonoBehaviour
{
    public BeastController beast;
    public Button swapButton;
    public TMP_Text swapLabel;
    public Text legacySwapLabel;
    public BeastId[] squad = new BeastId[0];

    private readonly List<BeastId> activeSquad = new List<BeastId>(3);
    private int activeSquadIndex;

    void Awake()
    {
        if (beast == null)
            beast = GetComponent<BeastController>();

        if (beast == null)
        {
            Debug.LogError("PadSwapSystem requires a BeastController on the same GameObject.", this);
            enabled = false;
            return;
        }

        if (swapButton != null)
            swapButton.onClick.AddListener(SwapToNextBeast);
    }

    void Start()
    {
        BuildSquad();
        UpdateSwapButton();
    }

    void Update()
    {
        UpdateSwapButton();
    }

    void OnDestroy()
    {
        if (swapButton != null)
            swapButton.onClick.RemoveListener(SwapToNextBeast);
    }

    public void SwapToNextBeast()
    {
        if (!CanSwap())
            return;

        activeSquadIndex = (activeSquadIndex + 1) % activeSquad.Count;
        beast.SetActiveBeast(activeSquad[activeSquadIndex]);
        UpdateSwapButton();
    }

    bool CanSwap()
    {
        return beast != null &&
            beast.State == BeastState.AtPad &&
            (activeSquad.Count > 1);
    }

    void BuildSquad()
    {
        activeSquad.Clear();
        bool hasConfiguredSquad = squad != null && squad.Length > 0;

        if (hasConfiguredSquad)
        {
            foreach (BeastId candidate in squad)
            {
                if (activeSquad.Count == 3)
                    break;

                if (IsUnlocked(candidate) && !activeSquad.Contains(candidate))
                    activeSquad.Add(candidate);
                else if (!IsUnlocked(candidate))
                    Debug.LogWarning("Ignoring unavailable Beast in the Pad Swap squad: " + candidate, this);
            }
        }

        BeastId selected = GameProgress.Instance != null
            ? GameProgress.Instance.SelectedBeast
            : BeastId.Choma;
        if (!hasConfiguredSquad)
        {
            if (IsUnlocked(selected))
                activeSquad.Add(selected);

            foreach (BeastId candidate in BeastRoster.AllBeasts)
            {
                if (activeSquad.Count == 3)
                    break;

                if (IsUnlocked(candidate) && !activeSquad.Contains(candidate))
                    activeSquad.Add(candidate);
            }
        }

        if (activeSquad.Count == 0)
            activeSquad.Add(BeastId.Choma);

        activeSquadIndex = activeSquad.IndexOf(selected);
        if (activeSquadIndex < 0)
            activeSquadIndex = 0;

        beast.SetActiveBeast(activeSquad[activeSquadIndex]);
        squad = activeSquad.ToArray();
    }

    bool IsUnlocked(BeastId candidate)
    {
        if (System.Array.IndexOf(BeastRoster.AllBeasts, candidate) < 0)
            return false;

        return GameProgress.Instance == null
            ? candidate == BeastId.Choma
            : GameProgress.Instance.IsBeastUnlocked(candidate);
    }

    void UpdateSwapButton()
    {
        bool canSwap = CanSwap();
        if (swapButton != null)
            swapButton.interactable = canSwap;

        if (activeSquad.Count == 0)
            return;

        int nextIndex = (activeSquadIndex + 1) % activeSquad.Count;
        string label = activeSquad.Count > 1
            ? "SWAP BEAST\nNEXT: " + BeastRoster.GetName(activeSquad[nextIndex]).ToUpper()
            : "SWAP BEAST\nSQUAD: 1 / 1";

        if (swapLabel != null)
            swapLabel.text = label;

        if (legacySwapLabel != null)
            legacySwapLabel.text = label;
    }
}
