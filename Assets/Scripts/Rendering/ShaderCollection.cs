using UnityEngine;

/// <summary>
/// Holds references to all custom shaders so Unity includes them in builds.
/// Shader.Find() only works at runtime if the shader is referenced somewhere —
/// this SO lives in Resources/ to guarantee inclusion.
/// </summary>
[CreateAssetMenu(menuName = "Iris/Shader Collection")]
public class ShaderCollection : ScriptableObject
{
    [Tooltip("All custom shaders that must survive build stripping.")]
    public Shader[] shaders;

    private static ShaderCollection s_instance;

    /// <summary>
    /// Loads the collection from Resources on first access.
    /// Just accessing this property forces Unity to load (and therefore include) the shaders.
    /// </summary>
    public static ShaderCollection Instance
    {
        get
        {
            if (s_instance == null)
                s_instance = Resources.Load<ShaderCollection>("ShaderCollection");
            return s_instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Preload()
    {
        // Force-load at startup so all shader references are alive before any Shader.Find() calls.
        // NOTE: We no longer call Shader.WarmupAllShaders() here — it blocked the main thread
        // for 3-5+ seconds compiling every variant. Shaders compile on first use instead,
        // which spreads the cost across gameplay rather than front-loading it.
        var inst = Instance;
        if (inst != null)
            Debug.Log($"[ShaderCollection] Loaded {inst.shaders.Length} shaders.");
    }
}
