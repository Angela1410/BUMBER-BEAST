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

    void Update()
    {
        if (beast == null || button == null)
            return;

        button.interactable = beast.CanUseSkill;

        string text = beast.IsSkillReady
            ? "READY"
            : beast.CooldownRemaining.ToString("0.0") + "s";

        if (cooldownText != null)
            cooldownText.text = text;

        if (legacyCooldownText != null)
            legacyCooldownText.text = text;
    }

    public void OnSkillPressed()
    {
        if (beast != null)
            beast.ActivateSkill();
    }
}