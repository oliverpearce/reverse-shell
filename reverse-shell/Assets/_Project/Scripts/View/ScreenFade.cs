using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// One persistent ink fade for every scene change (overworld, battle, title): <see cref="LoadScene"/> fades the
    /// screen to ink, loads the scene, and fades back in once the new scene has had a frame to build itself.
    /// Lives on its own overlay canvas above everything (DontDestroyOnLoad) and runs on unscaled time.
    /// </summary>
    public class ScreenFade : MonoBehaviour
    {
        static readonly Color Ink = new Color(0.07f, 0.065f, 0.06f, 1f);
        const float OutSeconds = 0.35f, InSeconds = 0.45f;

        static ScreenFade instance;
        Image cover;
        bool busy;

        /// <summary>True while a fade is running (input should wait).</summary>
        public static bool Busy { get { return instance != null && instance.busy; } }

        static ScreenFade Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("ScreenFade", typeof(RectTransform));
                DontDestroyOnLoad(go);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32000;
                instance = go.AddComponent<ScreenFade>();
                instance.cover = UIFactory.CreateImage(go.transform, "Cover", Ink);
                instance.cover.rectTransform.Fill();
                instance.cover.raycastTarget = false;
                instance.SetAlpha(0f);
                return instance;
            }
        }

        /// <summary>Fades to ink, loads the scene, fades back in.</summary>
        public static void LoadScene(string sceneName)
        {
            Instance.StartCoroutine(Instance.Run(() => SceneManager.LoadScene(sceneName)));
        }

        /// <summary>Fades to ink, runs the action (e.g. swapping a screen), fades back in.</summary>
        public static void Cut(Action midpoint)
        {
            Instance.StartCoroutine(Instance.Run(midpoint));
        }

        IEnumerator Run(Action midpoint)
        {
            if (busy) yield break;
            busy = true;
            cover.raycastTarget = true;   // swallow clicks while covered
            yield return Fade(cover.color.a, 1f, OutSeconds);
            if (midpoint != null) midpoint();
            yield return null;            // let the new scene's Start run under the cover
            yield return null;
            yield return Fade(1f, 0f, InSeconds);
            cover.raycastTarget = false;
            busy = false;
        }

        IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                SetAlpha(Mathf.Lerp(from, to, p * p * (3f - 2f * p)));
                yield return null;
            }
            SetAlpha(to);
        }

        void SetAlpha(float a)
        {
            var c = Ink;
            c.a = a;
            cover.color = c;
            cover.enabled = a > 0.001f;
        }
    }
}
