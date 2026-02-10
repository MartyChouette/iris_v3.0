using UnityEngine;

/// <summary>
/// Auto-delivers drinks to the coffee table when a drink is scored.
/// Spawns a visual cup and notifies DateSessionManager.
/// </summary>
public class CoffeeTableDelivery : MonoBehaviour
{
    public static CoffeeTableDelivery Instance { get; private set; }

    [Header("References")]
    [Tooltip("Where the drink cup appears.")]
    [SerializeField] private Transform drinkSpawnPoint;

    [Tooltip("Prefab for the drink cup visual. If null, a primitive is created.")]
    [SerializeField] private GameObject drinkCupPrefab;

    [Header("Audio")]
    [SerializeField] private AudioClip deliverSFX;

    private GameObject _currentDrink;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CoffeeTableDelivery] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Place a drink on the coffee table and notify the date.</summary>
    public void DeliverDrink(DrinkRecipeDefinition recipe, Color liquidColor, int score)
    {
        ClearDrink();

        Vector3 spawnPos = drinkSpawnPoint != null ? drinkSpawnPoint.position : transform.position;

        if (drinkCupPrefab != null)
        {
            _currentDrink = Instantiate(drinkCupPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // Fallback: small cylinder as cup
            _currentDrink = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _currentDrink.name = "DrinkCup";
            _currentDrink.transform.position = spawnPos;
            _currentDrink.transform.localScale = new Vector3(0.08f, 0.06f, 0.08f);
        }

        // Tint the cup to liquid color
        var rend = _currentDrink.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(rend.sharedMaterial);
            mat.color = liquidColor;
            rend.material = mat;
        }

        if (deliverSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(deliverSFX);

        // Notify DateSessionManager
        DateSessionManager.Instance?.ReceiveDrink(recipe, score);

        Debug.Log($"[CoffeeTableDelivery] Delivered {recipe?.drinkName ?? "drink"} (score={score})");
    }

    /// <summary>Remove the current drink from the table.</summary>
    public void ClearDrink()
    {
        if (_currentDrink != null)
        {
            Destroy(_currentDrink);
            _currentDrink = null;
        }
    }
}
