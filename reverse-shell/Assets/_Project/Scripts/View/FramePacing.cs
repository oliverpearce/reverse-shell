using UnityEngine;
using UnityEngine.SceneManagement;

namespace Combat.View
{
    /// <summary>
    /// Keeps the game light, made once at startup and kept across scenes:
    /// - Web build only: a frame cap, 60 while playing and 30 while the game is paused (title screen, Esc menu,
    ///   battle pause, tutorials), instead of the monitor's full refresh rate. The editor and desktop builds are left as they are.
    /// - Every camera's HDR follows the Graphics setting (<see cref="GameSettings.FastGraphics"/>), on each scene load
    ///   and whenever the setting changes.
    /// </summary>
    public class FramePacing : MonoBehaviour
    {
        const int PlayingFps = 60, PausedFps = 30;

        static bool Web { get { return Application.platform == RuntimePlatform.WebGLPlayer; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            var go = new GameObject("FramePacing");
            DontDestroyOnLoad(go);
            go.AddComponent<FramePacing>();
            SceneManager.sceneLoaded += (scene, mode) => ApplyCameras();
            GameSettings.GraphicsChanged += ApplyCameras;
        }

        static void ApplyCameras()
        {
            foreach (var cam in Camera.allCameras) GameSettings.ApplyGraphics(cam);
        }

        void Update()
        {
            if (!Web) return;
            int fps = Time.timeScale == 0f ? PausedFps : PlayingFps;
            if (Application.targetFrameRate != fps) Application.targetFrameRate = fps;
        }
    }
}
