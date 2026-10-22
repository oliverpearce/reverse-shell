using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    public struct SignSpot
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Color Color;
    }

    /// <summary>
    /// Turns an OverworldMap into one mesh textured from the CyberCity atlas (city.png, 8x5 cells of 64px):
    /// submesh 0 = ground (glossy, "wet"), submesh 1 = walls and roofs.
    /// Atlas cells used (col,row from top):
    ///   (1,1) concrete sidewalk · (0,4) road · (2,4) plaza panel
    ///   (0-3,2) windows · (0-3,3) broken windows · (4-7,1) bricks
    ///   (4-7,2) flat metal · (4-7,3) riveted metal · (4-7,4) vented shopfronts
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

        public static Mesh Build(OverworldMap map, Texture atlas, List<SignSpot> signs)
        {
            var b = new Buffers();
            for (int row = 0; row < map.Height; row++)
            {
                for (int x = 0; x < map.Width; x++)
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
                    SideFaces(b, map, atlas, signs, c, ci, x, row, h, x, row + 1, Vector3.back);
                    SideFaces(b, map, atlas, signs, c, ci, x, row, h, x, row - 1, Vector3.forward);
                    SideFaces(b, map, atlas, signs, c, ci, x, row, h, x + 1, row, Vector3.right);
                    SideFaces(b, map, atlas, signs, c, ci, x, row, h, x - 1, row, Vector3.left);
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

        static void SideFaces(Buffers b, OverworldMap map, Texture atlas, List<SignSpot> signs, char c, int ci,
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

                bool building = c == 'H' || c == '#';
                if (building && facesStreet && level == 1 && OverworldMap.Hash(x, row, (int)(normal.x * 3 + normal.z * 7)) % 3 == 0)
                {
                    var center = (bl + tr) * 0.5f;
                    signs.Add(new SignSpot
                    {
                        Position = new Vector3(center.x, 1.55f, center.z) + normal * 0.03f,
                        Normal = normal,
                        Color = WorldArt.NeonPalette[OverworldMap.Hash(x, row, 5) % WorldArt.NeonPalette.Length]
                    });
                }
            }
        }

        static Vector2Int FloorCell(char c)
        {
            switch (c)
            {
                case '=': return new Vector2Int(0, 4);
                case 'P':
                case 'X':
                case 'E': return new Vector2Int(2, 4);
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
