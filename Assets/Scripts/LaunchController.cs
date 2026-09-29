using UnityEngine;
using TMPro;

public class LaunchController : MonoBehaviour
{
    public bool unlimitedLaunches = true;
    public int maxLaunches = 5;
    public int launchesLeft;
    public float launchPower = 3.6f;
    public float maxDragDistance = 2.5f;
    public float maxLaunchSpeed = 9f;
    public TMP_Text launchesText;

    private Rigidbody2D rb;
    private LineRenderer aimLine;
    private BeastController beastController;

    private Vector2 dragStart;
    private bool isDragging = false;

    void Start()
    {
        launchesLeft = maxLaunches;
        if (GameProgress.Instance != null)
            launchPower += GameProgress.Instance.LaunchPowerLevel * 0.75f;

        rb = GetComponent<Rigidbody2D>();
        if (GameProgress.Instance != null)
        {
            GameProgress.Instance.ApplySelectedBeast(gameObject);
            GameProgress.Instance.ApplySelectedBeastVisual(gameObject);
        }
        aimLine = GetComponentInChildren<LineRenderer>();
        beastController = GetComponent<BeastController>();
        UpdateLaunchText();

        if (aimLine != null)
{
    aimLine.positionCount = 2;
    aimLine.enabled = false;
}
    }

    void OnValidate()
    {
        UpdateLaunchText();
    }

    void Update()
    {
        if (!unlimitedLaunches && launchesLeft <= 0)
            return;

        if (beastController != null && beastController.State != BeastState.AtPad)
            return;

        if (rb.linearVelocity.magnitude > 0.1f)
    return;

        if (Input.GetMouseButtonDown(0))
        {
            dragStart = GetMouseWorldPosition();
            isDragging = true;

            if (aimLine != null)
                aimLine.enabled = true;
        }

        if (isDragging)
        {
            Vector2 currentMousePosition = GetMouseWorldPosition();

            Vector2 launchVector = dragStart - currentMousePosition;

            float dragDistance = Mathf.Min(
                launchVector.magnitude,
                maxDragDistance
            );

            Vector2 direction = launchVector.normalized;

            if (aimLine != null)
            {
                aimLine.SetPosition(0, transform.position);
                aimLine.SetPosition(
                    1,
                    (Vector2)transform.position + direction * dragDistance
                );
            }
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            Vector2 dragEnd = GetMouseWorldPosition();

            Vector2 launchVector = dragStart - dragEnd;

            float dragDistance = Mathf.Min(
                launchVector.magnitude,
                maxDragDistance
            );

            if (dragDistance > 0.1f)
            {
                Vector2 direction = launchVector.normalized;

                float launchSpeed = Mathf.Min(dragDistance * launchPower, maxLaunchSpeed);
                rb.linearVelocity = direction * launchSpeed;

                if (!unlimitedLaunches)
                {
                    launchesLeft--;
                    UpdateLaunchText();
                }

                if (beastController != null)
                    beastController.NotifyLaunched();

            }

            isDragging = false;
            if (aimLine != null)
                aimLine.enabled = false;
        }
    }

    Vector2 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Input.mousePosition;

        mousePosition.z =
            -Camera.main.transform.position.z;

        return Camera.main.ScreenToWorldPoint(mousePosition);
    }

    void UpdateLaunchText()
    {
        if (launchesText == null)
        {
            GameObject launchesObject = GameObject.Find("LaunchesText");

            if (launchesObject != null)
                launchesText = launchesObject.GetComponent<TMP_Text>();
        }

        if (launchesText == null)
            return;

        if (unlimitedLaunches)
        {
            launchesText.text = string.Empty;
            return;
        }

        launchesText.text = unlimitedLaunches
            ? "Launches: Unlimited"
            : "Launches: " + launchesLeft;
    }

}