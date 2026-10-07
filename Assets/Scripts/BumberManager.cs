using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BumperManager : MonoBehaviour
{
    [Header("Respawn Settings")]
    public GameObject bumperPrefab; 
    public Transform[] fixedSpawnPoints; 
    public float respawnDelay = 5f;
    public float checkRadius = 0.5f; // How wide to check for obstacles before spawning

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

        // Find all spawn points that don't have an enemy or another bumper sitting on them
        List<Transform> emptyPoints = new List<Transform>();
        foreach (Transform point in fixedSpawnPoints)
        {
            Collider2D hit = Physics2D.OverlapCircle(point.position, checkRadius);
            if (hit == null)
            {
                emptyPoints.Add(point);
            }
        }

        if (emptyPoints.Count > 0)
        {
            // Pick a random empty safe point
            Transform selectedPoint = emptyPoints[Random.Range(0, emptyPoints.Count)];
            Instantiate(bumperPrefab, selectedPoint.position, Quaternion.identity);
        }
        else
        {
            // If all points are blocked by enemies, wait 1 second and try again
            yield return new WaitForSeconds(1f);
            StartCoroutine(RespawnBumperRoutine());
        }
    }
}