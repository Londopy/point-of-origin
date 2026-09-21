using UnityEngine;

namespace PointOfOrigin
{
    /// <summary>The scene stays empty apart from its camera; the game builds itself at load.</summary>
    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Object.FindFirstObjectByType<GameController>() != null) return;
            var go = new GameObject("Point of Origin");
            go.AddComponent<Sfx>();
            go.AddComponent<GameController>();
        }
    }
}
