using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Manages a collection of flower plants and attached flowers</summary>
public class FlowerArea : MonoBehaviour
{
    // The diameter of the area where the agent and flowers can be
    public const float AreaDiameter = 20f;

    // The list of all flower plants in this flower area (flower plants have multiple flowers)
    private List<GameObject> flowerPlants;

    // A lookup dictionary for looking up a flower from a nectar collider
    private Dictionary<Collider, Flower> nectarFlowerDictionary;

    /// <summary>The list of all flowers in the flower area</summary>
    public List<Flower> Flowers { get; private set; }

    // ---------- lifecycle ----------

    private void Awake()
    {
        flowerPlants = new List<GameObject>();
        nectarFlowerDictionary = new Dictionary<Collider, Flower>();
        Flowers = new List<Flower>();
    }

    private IEnumerator Start()
    {
        // Let all Flower.Awake() run so nectarCollider is assigned
        yield return null;
        RebuildLookup();
    }

    public void RebuildLookup()
    {
        Flowers.Clear();
        flowerPlants.Clear();
        nectarFlowerDictionary.Clear();
        FindChildFlowers(transform);
        Debug.Log($"[FlowerArea] Flowers={Flowers.Count}, NectarColliders={nectarFlowerDictionary.Count}");
    }

    /// <summary>Reset the flowers and flower plants</summary>
    public void ResetFlowers()
    {
        foreach (GameObject flowerPlant in flowerPlants)
        {
            float xRot = Random.Range(-5f, 5f);
            float yRot = Random.Range(-180f, 180f);
            float zRot = Random.Range(-5f, 5f);
            flowerPlant.transform.localRotation = Quaternion.Euler(xRot, yRot, zRot);
        }

        foreach (Flower flower in Flowers)
            flower.ResetFlower();
    }

    /// <summary>Gets the Flower that a nectar collider belongs to</summary>
    public Flower GetFlowerFromNectar(Collider collider)
    {
        return collider != null && nectarFlowerDictionary.TryGetValue(collider, out var f) ? f : null;
    }

    // ---------- discovery ----------

    /// <summary>Recursively finds all flowers and flower plants that are children of a parent transform</summary>
    private void FindChildFlowers(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.CompareTag("flower_plant"))
            {
                if (!flowerPlants.Contains(child.gameObject))
                    flowerPlants.Add(child.gameObject);

                FindChildFlowers(child);
                continue;
            }

            // Look for a Flower component on this child
            Flower flower = child.GetComponent<Flower>();
            if (flower != null)
            {
                if (!Flowers.Contains(flower))
                    Flowers.Add(flower);

                // Resolve the nectar collider (robust)
                Collider col = flower.nectarCollider;
                if (col == null)
                {
                    var allCols = child.GetComponentsInChildren<Collider>(true);

                    col = allCols.FirstOrDefault(c => string.Equals(c.gameObject.name, "FlowerNectarCollider", System.StringComparison.OrdinalIgnoreCase))
                       ?? allCols.FirstOrDefault(c => string.Equals(c.gameObject.name, "Nectar", System.StringComparison.OrdinalIgnoreCase))
                       ?? allCols.FirstOrDefault(c => string.Equals(c.tag, "nectar", System.StringComparison.OrdinalIgnoreCase))
                       ?? allCols.FirstOrDefault(c => c.isTrigger)
                       ?? allCols.FirstOrDefault();

                    if (col != null) flower.nectarCollider = col; // persist to the component
                }

                if (col == null)
                {
                    Debug.LogError($"[FlowerArea] '{flower.name}' is missing Nectar Collider reference.", flower);
                    continue;   // skip this flower so we don't add a null key
                }

                if (nectarFlowerDictionary.ContainsKey(col))
                {
                    Debug.LogWarning($"[FlowerArea] Duplicate nectar collider '{col.name}' on '{flower.name}'. Skipping.");
                }
                else
                {
                    nectarFlowerDictionary.Add(col, flower);
                }
            }
            else
            {
                // Not a Flower; keep scanning children
                FindChildFlowers(child);
            }
        }
    }
}
