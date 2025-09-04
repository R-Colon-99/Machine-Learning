using System;
using System.Linq;
using UnityEngine;

/// <summary>Manages a single flower with nectar</summary>
public class Flower : MonoBehaviour
{
    [Tooltip("The color when the flower is full")]
    public Color fullFlowerColor = new Color(1f, 0f, .3f);

    [Tooltip("The color when the flower is empty")]
    public Color emptyFlowerColor = new Color(.5f, 0f, 1f);

    /// <summary>The trigger collider representing the nectar</summary>
    [SerializeField] public Collider nectarCollider;   // visible in Inspector

    // The solid collider representing the flower petals
    private Collider flowerCollider;

    // The flower's material
    private Material flowerMaterial;

    /// <summary>A vector pointing straight out of the flower</summary>
    public Vector3 FlowerUpVector => (nectarCollider ? nectarCollider.transform.up : transform.up);

    /// <summary>The center position of the nectar collider</summary>
    public Vector3 FlowerCenterPosition => (nectarCollider ? nectarCollider.transform.position : transform.position);

    /// <summary>The amount of nectar remaining in the flower</summary>
    public float NectarAmount { get; private set; }

    /// <summary>Whether the flower has any nectar remaining</summary>
    public bool HasNectar => NectarAmount > 0f;

    /// <summary>Attempts to remove nectar from the flower</summary>
    public float Feed(float amount)
    {
        // Clamp, then subtract the *actual* amount taken
        float nectarTaken = Mathf.Clamp(amount, 0f, NectarAmount);
        NectarAmount -= nectarTaken;

        if (NectarAmount <= 0f)
        {
            NectarAmount = 0f;
            if (flowerCollider) flowerCollider.gameObject.SetActive(false);
            if (nectarCollider) nectarCollider.gameObject.SetActive(false);
            if (flowerMaterial) flowerMaterial.SetColor("_BaseColor", emptyFlowerColor);
        }
        return nectarTaken;
    }

    /// <summary>Resets the flower</summary>
    public void ResetFlower()
    {
        NectarAmount = 1f;
        if (flowerCollider) flowerCollider.gameObject.SetActive(true);
        if (nectarCollider) nectarCollider.gameObject.SetActive(true);
        if (flowerMaterial) flowerMaterial.SetColor("_BaseColor", fullFlowerColor);
    }

    // ---------- auto-wiring & init ----------

    private void Awake()
    {
        var mr = GetComponent<MeshRenderer>();
        flowerMaterial = mr ? mr.material : null;

        // Try exact child names first
        var petalsTf = transform.Find("FlowerCollider");
        var nectarTf = transform.Find("FlowerNectarCollider");

        if (petalsTf && petalsTf.TryGetComponent(out Collider petalsCol)) flowerCollider = petalsCol;
        if (nectarTf && nectarTf.TryGetComponent(out Collider nectarCol)) nectarCollider = nectarCol;

        // Fallbacks so the reference is never left null
        if (!flowerCollider) flowerCollider = GetComponentsInChildren<Collider>(true)
                                            .FirstOrDefault(c => !c.isTrigger);
        if (!nectarCollider) nectarCollider = GetComponentsInChildren<Collider>(true)
                                            .FirstOrDefault(c => c.isTrigger)
                                         ?? GetComponentInChildren<Collider>(true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // keep the reference wired in the editor
        if (!nectarCollider)
            nectarCollider = GetComponentsInChildren<Collider>(true)
                             .FirstOrDefault(c =>
                                 c.gameObject.name.IndexOf("nectar", StringComparison.OrdinalIgnoreCase) >= 0)
                          ?? GetComponentsInChildren<Collider>(true).FirstOrDefault(c => c.isTrigger);
    }
#endif
}
