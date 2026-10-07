using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class LaunchController : MonoBehaviour
{
    public bool unlimitedLaunches = true;
    public int maxLaunches = 5;
    public int launchesLeft;
    public float launchPower = 3.6f;
    public float maxDragDistance = 2.5f;
    public float maxLaunchSpeed = 15f;
    public TMP_Text launchesText;

    private Rigidbody2D rb;
    private LineRenderer aimLine;
    private BeastController beastController;

    private Vector2 dragStart;
    private bool isDragging = false;

    void Start()
    {
        unlimitedLaunches = true;
        launchesLeft = Mathf.Max(0, maxLaunches);
        if (GameProgress.Instance != null)
            launchPower += GameProgress.Instance.LaunchPowerLevel * 0.75f;

        rb = GetComponent<Rigidbody2D>();
        beastController = GetComponent<BeastController>();
        if (GameProgress.Instance != null)
        {
            GameProgress.Instance.ApplySelectedBeast(gameObject);
            GameProgress.Instance.ApplySelectedBeastVisual(gameObject);
            if (beastController != null)
                beastController.SetActiveBeast(GameProgress.Instance.SelectedBeast);
        }
        aimLine = GetComponentInChildren<LineRenderer>();
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

        if (rb != null && rb.linearVelocity.magnitude > 0.1f)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

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
        Camera camera = Camera.main;

        if (camera == null)
        {
            Debug.LogError("LaunchController requires a Main Camera tagged camera in the scene.");
            return transform.position;
        }

        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z = -camera.transform.position.z;
        return camera.ScreenToWorldPoint(mousePosition);
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