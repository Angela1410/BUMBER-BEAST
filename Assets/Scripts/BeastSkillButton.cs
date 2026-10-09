using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BeastSkillButton : MonoBehaviour
{
    public BeastController beast;
    public Button button;
    public TMP_Text cooldownText;
    public Text legacyCooldownText;

    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(OnSkillPressed);
    }

    void Start()
    {
        PortraitButtonLayout.Apply(transform as RectTransform, 0);
    }

    void Update()
    {
        if (beast == null || button == null)
            return;

        button.interactable = beast.CanUseSkill;

        string text;
        if (beast.IsDoubleBounceActive)
            text = "ACTIVE";
        else if (beast.IsSkillReady)
            text = "READY";
        else
            text = "COOLDOWN  " + Mathf.CeilToInt(beast.CooldownRemaining) + "s";

        string label = BeastRoster.GetAbilityName(beast.ActiveBeast).ToUpper() + "\n" + text;

        if (cooldownText != null)
            cooldownText.text = label;

        if (legacyCooldownText != null)
            legacyCooldownText.text = label;
    }

    public void OnSkillPressed()
    {
        if (beast != null)
            beast.ActivateSkill();
    }
}