using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BumperManager : MonoBehaviour
{
    [Header("Respawn Settings")]
    public GameObject bumperPrefab; 
    public Transform[] fixedSpawnPoints; 
    public float respawnDelay = 5f;
    public float checkRadius = 0.5f;

    [Header("Respawn Colours")]
    [Tooltip("A respawned bumper gets one of these types at random (equal odds). " +
        "Leave empty to keep the prefab's own type.")]
    public BumperType[] respawnTypes = { BumperType.Green, BumperType.Blue, BumperType.Red };

    void OnDrawGizmos()
    {
        if (fixedSpawnPoints == null)
            return;

        Gizmos.color = new Color(0.15f, 0.7f, 1f, 0.9f);
        foreach (Transform point in fixedSpawnPoints)
        {
            if (point == null)
                continue;

            Gizmos.DrawWireSphere(point.position, Mathf.Max(0.1f, checkRadius));

#if UNITY_EDITOR
            UnityEditor.Handles.Label(point.position + Vector3.up * (checkRadius + 0.15f), point.name);
#endif
        }
    }

    public void HandleBumperBroken()
    {
        StartCoroutine(RespawnBumperRoutine());
    }

    IEnumerator RespawnBumperRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        if (fixedSpawnPoints == null || fixedSpawnPoints.Length == 0)
        {
            Debug.LogWarning("BumperManager: No fixed spawn points assigned!");
            yield break;
        }

        if (bumperPrefab == null)
        {
            Debug.LogError("BumperManager: No bumper prefab assigned; the broken bumper cannot respawn.", this);
            yield break;
        }

        
        List<Transform> emptyPoints = new List<Transform>();
        foreach (Transform point in fixedSpawnPoints)
        {
            if (point == null)
                continue;

            Collider2D hit = Physics2D.OverlapCircle(point.position, checkRadius);
            if (hit == null)
            {
                emptyPoints.Add(point);
            }
        }

        if (emptyPoints.Count > 0)
        {
            
            Transform selectedPoint = emptyPoints[Random.Range(0, emptyPoints.Count)];
            GameObject spawned = Instantiate(bumperPrefab, selectedPoint.position, Quaternion.identity);

            // the prefab is always one color, so we pick the new bumper's color here
            if (respawnTypes != null && respawnTypes.Length > 0)
            {
                Bumper bumper = spawned.GetComponent<Bumper>();
                if (bumper != null && !bumper.isBramble)
                {
                    bumper.type = respawnTypes[Random.Range(0, respawnTypes.Length)];
                    bumper.RefreshVisuals();
                }
            }
        }
        else
        {
            
            yield return new WaitForSeconds(1f);
            StartCoroutine(RespawnBumperRoutine());
        }
    }
}
