using UnityEngine;

namespace Snake
{
    /// <summary>
    /// Spawns the game automatically when the first scene loads, so pressing Play in any scene works
    /// with no setup. If you place a SnakeGame component in the scene yourself, this does nothing.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<SnakeGame>() != null) return;
            new GameObject("Snake Game").AddComponent<SnakeGame>();
        }
    }

    public static class Haptics
    {
        public static void Vibrate()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
