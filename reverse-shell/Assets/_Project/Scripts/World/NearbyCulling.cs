using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Draws only what's near the player: every renderer under the area (the map's mesh chunks, trees, grass tufts,
    /// vines, props, characters, effects) whose bounds are farther than <see cref="Radius"/> tiles from a point a little
    /// north of the player (the camera looks north, so the view reaches further that way) is skipped. Uses
    /// Renderer.forceRenderingOff, so it never fights scripts that show / hide things themselves. Checks a few times a
    /// second (unscaled time, so it keeps up under the Esc menu); new objects are picked up every couple of seconds.
    /// The radius covers the whole screen at the normal camera distance: a smaller one shows empty ground at the edges.
    /// </summary>
    public class NearbyCulling : MonoBehaviour
    {
        /// <summary>Tiles (world units) from the centre point. About 12 just covers the screen's far corners.</summary>
        public float Radius = 12f;

        /// <summary>How far north of the player the circle's centre sits.</summary>
        public float NorthBias = 3f;

        /// <summary>Where the player is (the circle follows it).</summary>
        public Func<Vector3> Center;

        /// <summary>Never culled (the player and everything under it).</summary>
        public Transform Keep;

        const float CheckEvery = 0.2f, CollectEvery = 2f;

        readonly List<Renderer> renderers = new List<Renderer>();
        float nextCheck, nextCollect;

        void Collect()
        {
            renderers.Clear();
            GetComponentsInChildren(true, renderers);
            if (Keep != null) renderers.RemoveAll(r => r.transform.IsChildOf(Keep));
        }

        void LateUpdate()
        {
            float now = Time.unscaledTime;
            if (now >= nextCollect)
            {
                nextCollect = now + CollectEvery;
                Collect();
                nextCheck = 0f;
            }
            if (now < nextCheck || Center == null) return;
            nextCheck = now + CheckEvery;

            var c = Center() + new Vector3(0f, 0f, NorthBias);
            float r2 = Radius * Radius;
            for (int i = renderers.Count - 1; i >= 0; i--)
            {
                var r = renderers[i];
                if (r == null)
                {
                    renderers.RemoveAt(i);
                    continue;
                }
                var b = r.bounds;
                // Distance on the ground only: measure at the bounds' own height.
                bool far = b.SqrDistance(new Vector3(c.x, b.center.y, c.z)) > r2;
                if (r.forceRenderingOff != far) r.forceRenderingOff = far;
            }
        }
    }
}
