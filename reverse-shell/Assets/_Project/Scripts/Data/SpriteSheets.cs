using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>Slices walk sheets into sprites at runtime (cached), so sheets need no import-time slicing.</summary>
    public static class SpriteSheets
    {
        public const int Down = 0, Left = 1, Right = 2, Up = 3;

        static readonly Dictionary<Texture2D, Sprite[,]> cache = new Dictionary<Texture2D, Sprite[,]>();

        /// <summary>[row, col] sprites, row 0 = top of the sheet, centered pivot.</summary>
        public static Sprite[,] Slice(Texture2D sheet, int columns, int rows)
        {
            Sprite[,] sprites;
            if (cache.TryGetValue(sheet, out sprites) && sprites != null && sprites[0, 0] != null) return sprites;

            int cw = sheet.width / columns, ch = sheet.height / rows;
            sprites = new Sprite[rows, columns];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                    sprites[r, c] = Sprite.Create(sheet, new Rect(c * cw, sheet.height - (r + 1) * ch, cw, ch),
                        new Vector2(0.5f, 0.5f), cw, 0, SpriteMeshType.FullRect);
            cache[sheet] = sprites;
            return sprites;
        }

        public static Sprite[,] Slice(UnitDefinition unit)
        {
            return unit.spriteSheet != null ? Slice(unit.spriteSheet, unit.sheetColumns, unit.sheetRows) : null;
        }
    }
}
