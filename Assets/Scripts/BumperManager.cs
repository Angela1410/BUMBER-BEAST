using System.Collections;
using UnityEngine;

// brings broken bumpers back. every bumper has a fixed spot for its stage (placed by the editor tool
// BumperLayouts), so a broken bumper comes back in that same spot, with the same color, after a short wait
public class BumperManager : MonoBehaviour
{
    [Header("Respawn Settings")]
    [Tooltip("The bumper the editor copies from, and where stage bumpers borrow missing pictures from.")]
    public GameObject bumperPrefab;
    public float respawnDelay = 5f;
    [Tooltip("A bumper only comes back when nothing (the Beast, an enemy) is this close to its spot.")]
    public float checkRadius = 0.5f;

    // which BumperLayouts layout this stage has (stage x 100 + version), the editor reads it
    [HideInInspector] public int layoutStamp;

    // a bumper broke: hide it now and bring it back in its own spot later
    public void Respawn(Bumper bumper)
    {
        if (bumper == null)
            return;
        bumper.gameObject.SetActive(false);
        StartCoroutine(RespawnRoutine(bumper));
    }

    IEnumerator RespawnRoutine(Bumper bumper)
    {
        yield return new WaitForSeconds(respawnDelay);

        // wait until its spot is free, so it never pops up on top of the Beast or an enemy
        while (bumper != null && Physics2D.OverlapCircle(bumper.HomePosition, checkRadius) != null)
            yield return new WaitForSeconds(1f);

        if (bumper != null)
            bumper.ComeBack();
    }
}
