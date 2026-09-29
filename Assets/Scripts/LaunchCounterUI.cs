using UnityEngine;
using TMPro;

public class LaunchCounterUI : MonoBehaviour
{
    public LaunchController launchController;
    public TMP_Text launchText;

    void Update()
    {
        if (launchText == null || launchController == null)
            return;

        if (launchController.unlimitedLaunches)
        {
            launchText.text = string.Empty;
            return;
        }

        launchText.text = launchController.unlimitedLaunches
            ? "Launches: Unlimited"
            : "Launches: " + launchController.launchesLeft;
    }
}