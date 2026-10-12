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
            
            Transform selectedPoint = emptyPoints[Random.Range(0, emptyPoints.Count)];
            Instantiate(bumperPrefab, selectedPoint.position, Quaternion.identity);
        }
        else
        {
            
            yield return new WaitForSeconds(1f);
            StartCoroutine(RespawnBumperRoutine());
        }
    }
}