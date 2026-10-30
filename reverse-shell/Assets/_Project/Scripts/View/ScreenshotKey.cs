#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;

namespace Combat.View
{
    /// <summary>
    /// Editor only: F12 in Play mode saves the game view (UI included) as a 1920 x 1080 PNG for the store page and the
    /// README, into itch/screenshots/new/ at the repo root (git-ignored). Any Game view size works; it's scaled to fit.
    /// </summary>
    public class ScreenshotKey : MonoBehaviour
    {
        const int Width = 1920, Height = 1080;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go = new GameObject("ScreenshotKey");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            go.AddComponent<ScreenshotKey>();
        }

        static string Folder
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "itch", "screenshots", "new")); }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F12)) StartCoroutine(Capture());
        }

        IEnumerator Capture()
        {
            yield return new WaitForEndOfFrame();
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            var rt = RenderTexture.GetTemporary(Width, Height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(shot, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var output = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            output.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            output.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "shot_" + System.DateTime.Now.ToString("HH-mm-ss-fff") + ".png");
            File.WriteAllBytes(path, output.EncodeToPNG());
            Destroy(shot);
            Destroy(output);
            Debug.Log("Screenshot saved: " + path);
        }
    }
}
#endif
