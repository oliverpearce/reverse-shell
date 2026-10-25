using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>A spot on a wall face for a decal quad (vines), centered at Position, Length tall.</summary>
    public struct DecalSpot
    {
        public Vector3 Position;
        public Vector3 Normal;
        public float Length;
    }

    /// <summary>
    /// Turns an OverworldMap into one mesh textured from the ruins atlas (ruins.png, 8x5 cells of 64px, made by
    /// Tools/TileForge/ruins_atlas.py): submesh 0 = ground, submesh 1 = walls and roofs.
    /// Atlas cells used (col,row from top):
    ///   (1,1) cracked sidewalk · (0,4) overgrown road · (2,4) grass plaza · (3,1) moss · (5,0) dirt
    ///   (0-3,2) empty windows · (0-3,3) ivy-covered windows · (4-7,1) mossy bricks
    ///   (4-7,2) weathered concrete · (4-7,3) grassy rooftops · (4-7,4) ruined storefronts
    /// </summary>
    public static class WorldMeshBuilder
    {
        class Buffers
        {
            public readonly List<Vector3> Verts = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Ground = new List<int>();
            public readonly List<int> Walls = new List<int>();
        }

        public static Mesh Build(OverworldMap map, Texture atlas, List<DecalSpot> vines)
        {
            return Build(map, atlas, vines, new RectInt(0, 0, map.Width, map.Height));
        }

        /// <summary>One chunk of the map: the cells in <paramref name="cells"/> (x = column, y = row). The overworld is
        /// built as chunks so the ones far from the player can be skipped (NearbyCulling).</summary>
        public static Mesh Build(OverworldMap map, Texture atlas, List<DecalSpot> vines, RectInt cells)
        {
            var b = new Buffers();
            for (int row = Mathf.Max(0, cells.yMin); row < Mathf.Min(map.Height, cells.yMax); row++)
            {
                for (int x = Mathf.Max(0, cells.xMin); x < Mathf.Min(map.Width, cells.xMax); x++)
                {
                    char c = map.At(x, row);
                    int h = OverworldMap.BlockHeight(c);
                    float x0 = x, x1 = x + 1, z0 = map.Height - 1 - row, z1 = z0 + 1;

                    if (h == 0)
                    {
                        var uv = FloorCell(c);
                        Quad(b, b.Ground, new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), new Vector3(x1, 0, z0),
                            Vector3.up, AtlasUV(atlas, uv));
                        continue;
                    }

                    int ci = ColorIndex(c, x, row);
                    Quad(b, b.Walls, new Vector3(x0, h, z0), new Vector3(x0, h, z1), new Vector3(x1, h, z1), new Vector3(x1, h, z0),
                        Vector3.up, AtlasUV(atlas, RoofCell(c, ci)));

                    // South (-z), North (+z), East (+x), West (-x) faces, only where the neighbor is lower.
                    SideFaces(b, map, atlas, vines, c, ci, x, row, h, x, row + 1, Vector3.back);
                    SideFaces(b, map, atlas, vines, c, ci, x, row, h, x, row - 1, Vector3.forward);
                    SideFaces(b, map, atlas, vines, c, ci, x, row, h, x + 1, row, Vector3.right);
                    SideFaces(b, map, atlas, vines, c, ci, x, row, h, x - 1, row, Vector3.left);
                }
            }

            var mesh = new Mesh { name = "OverworldMesh" };
            if (b.Verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(b.Verts);
            mesh.SetNormals(b.Normals);
            mesh.SetUVs(0, b.Uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(b.Ground, 0);
            mesh.SetTriangles(b.Walls, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        static void SideFaces(Buffers b, OverworldMap map, Texture atlas, List<DecalSpot> vines, char c, int ci,
            int x, int row, int h, int nx, int nrow, Vector3 normal)
        {
            int neighborHeight = map.HeightAt(nx, nrow);
            if (neighborHeight >= h) return;
            bool facesStreet = neighborHeight == 0 && map.InBounds(nx, nrow);

            float x0 = x, x1 = x + 1, z0 = map.Height - 1 - row, z1 = z0 + 1;
            for (int level = neighborHeight; level < h; level++)
            {
                float y0 = level, y1 = level + 1;
                Vector3 bl, tl, tr, br;
                if (normal == Vector3.back) { bl = new Vector3(x0, y0, z0); tl = new Vector3(x0, y1, z0); tr = new Vector3(x1, y1, z0); br = new Vector3(x1, y0, z0); }
                else if (normal == Vector3.forward) { bl = new Vector3(x1, y0, z1); tl = new Vector3(x1, y1, z1); tr = new Vector3(x0, y1, z1); br = new Vector3(x0, y0, z1); }
                else if (normal == Vector3.right) { bl = new Vector3(x1, y0, z0); tl = new Vector3(x1, y1, z0); tr = new Vector3(x1, y1, z1); br = new Vector3(x1, y0, z1); }
                else { bl = new Vector3(x0, y0, z1); tl = new Vector3(x0, y1, z1); tr = new Vector3(x0, y1, z0); br = new Vector3(x0, y0, z0); }

                Quad(b, b.Walls, bl, tl, tr, br, normal, AtlasUV(atlas, SideCell(c, ci, level, x, row)));

                // Vines spill over the top edge of walls that face open ground.
                if (facesStreet && level == h - 1 && h >= 2 && OverworldMap.Hash(x, row, (int)(normal.x * 3 + normal.z * 7)) % 3 != 0)
                {
                    var center = (bl + tr) * 0.5f;
                    float length = Mathf.Min(h, 1.2f + OverworldMap.Hash(x, row, 9) % 3 * 0.6f);
                    vines.Add(new DecalSpot
                    {
                        Position = new Vector3(center.x, h - length / 2f, center.z) + normal * 0.02f,
                        Normal = normal,
                        Length = length
                    });
                }
            }
        }

        static Vector2Int FloorCell(char c)
        {
            switch (c)
            {
                case '=':
                case ':': return new Vector2Int(0, 4);
                case 'K':
                case 'U':
                case 'J':
                case 'w':
                case 'P':
                case 'G':
                case 'C':
                case 'c':
                case 'W':
                case 'X':
                case 'E': return new Vector2Int(2, 4);
                case ',':
                case '*': return new Vector2Int(3, 1);
                case 'T':
                case 'b':
                case 'r': return new Vector2Int(5, 0);
                default: return new Vector2Int(1, 1);
            }
        }

        static Vector2Int RoofCell(char c, int ci)
        {
            return c == 'H' || c == '#' ? new Vector2Int(4 + ci, 3) : new Vector2Int(4 + ci, 2);
        }

        static Vector2Int SideCell(char c, int ci, int level, int x, int row)
        {
            switch (c)
            {
                case 'H':
                case '#':
                    if (level == 0) return new Vector2Int(4 + ci, 4);
                    return new Vector2Int(ci, OverworldMap.Hash(x, row, level) % 7 == 0 ? 3 : 2);
                case 'B': return new Vector2Int(4 + ci, 1);
                default: return new Vector2Int(4 + ci, 2);
            }
        }

        static int ColorIndex(char c, int x, int row)
        {
            switch (c)
            {
                case 'B': return 0;
                case 'L': return 3;
                default: return (x / 4 + row / 6) % 4;
            }
        }

        static Rect AtlasUV(Texture atlas, Vector2Int cell) { return WorldArt.AtlasUV(atlas, cell.x, cell.y); }

        /// <summary>Corners as seen from the front: bottom-left, top-left, top-right, bottom-right (clockwise).</summary>
        static void Quad(Buffers b, List<int> tris, Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Vector3 normal, Rect uv)
        {
            int i = b.Verts.Count;
            b.Verts.Add(bl); b.Verts.Add(tl); b.Verts.Add(tr); b.Verts.Add(br);
            for (int k = 0; k < 4; k++) b.Normals.Add(normal);
            b.Uvs.Add(new Vector2(uv.xMin, uv.yMin));
            b.Uvs.Add(new Vector2(uv.xMin, uv.yMax));
            b.Uvs.Add(new Vector2(uv.xMax, uv.yMax));
            b.Uvs.Add(new Vector2(uv.xMax, uv.yMin));
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }
    }
}
