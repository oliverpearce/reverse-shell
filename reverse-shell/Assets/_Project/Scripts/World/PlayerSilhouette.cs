using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The player's outline behind scenery (Track C): a copy of the player's sprite drawn with the Silhouette shader,
    /// which only shows on the pixels something covers (a tree, a capsule, a wall), and only the sprite's edge there:
    /// the hidden parts of the player read as an outline. It copies the sprite and flip every frame, so it follows
    /// the animation.
    /// </summary>
    public class PlayerSilhouette : MonoBehaviour
    {
        SpriteRenderer source, copy;
        static Material material;

        /// <summary>Adds the outline to <paramref name="sprite"/> (the player's SpriteRenderer).</summary>
        public static PlayerSilhouette Attach(SpriteRenderer sprite)
        {
            if (sprite == null) return null;
            var shader = Shader.Find("Reverse Shell/Silhouette");
            if (shader == null) return null;
            if (material == null) material = new Material(shader) { name = "PlayerSilhouette" };
            var go = new GameObject("Silhouette");
            go.transform.SetParent(sprite.transform, false);
            var s = go.AddComponent<PlayerSilhouette>();
            s.source = sprite;
            s.copy = go.AddComponent<SpriteRenderer>();
            s.copy.sharedMaterial = material;
            s.copy.sortingOrder = sprite.sortingOrder + 1;
            return s;
        }

        void LateUpdate()
        {
            if (source == null) { Destroy(gameObject); return; }
            copy.sprite = source.sprite;
            copy.flipX = source.flipX;
            copy.flipY = source.flipY;
            copy.enabled = source.enabled && source.gameObject.activeInHierarchy;
        }
    }
}
