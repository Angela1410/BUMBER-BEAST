using System.Collections;
using System;
using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class ThornmawAbility : MonoBehaviour
{
    public GameObject bramblePrefab;

    void Start()
    {
        Enemy enemy = GetComponent<Enemy>();
        if (enemy.isBoss &&
            !string.IsNullOrEmpty(enemy.bossName) &&
            enemy.bossName.IndexOf("Thornmaw", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            StartCoroutine(DropBrambleRoutine());
        }
    }

    IEnumerator DropBrambleRoutine()
    {
        if (bramblePrefab == null)
        {
            Debug.LogError("ThornmawAbility requires a bramble prefab.", this);
            yield break;
        }

        yield return new WaitForSeconds(3f);

        while (true)
        {
            GameObject bramble = Instantiate(
                bramblePrefab,
                transform.position + new Vector3(0f, 0.8f, 0f),
                Quaternion.identity);
            Bumper bumper = bramble.GetComponent<Bumper>();
            if (bumper == null)
            {
                Debug.LogError("The Thornmaw bramble prefab must have a Bumper component.", bramble);
                Destroy(bramble);
                yield break;
            }

            bumper.hp = 1;
            yield return new WaitForSeconds(4.5f);
        }
    }
}
