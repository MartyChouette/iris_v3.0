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
    private Material _drinkMat;

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

        // Tint the cup to liquid color (track material to avoid leak)
        var rend = _currentDrink.GetComponent<Renderer>();
        if (rend == null) rend = _currentDrink.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            // Always create from a known-good URP Lit shader to avoid broken materials
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            _drinkMat = new Material(shader);
            _drinkMat.SetColor("_BaseColor", liquidColor);
            _drinkMat.color = liquidColor;
            // Ensure surface type is opaque
            _drinkMat.SetFloat("_Surface", 0f);
            rend.material = _drinkMat;
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
        if (_drinkMat != null)
        {
            Destroy(_drinkMat);
            _drinkMat = null;
        }
        if (_currentDrink != null)
        {
            Destroy(_currentDrink);
            _currentDrink = null;
        }
    }
}
