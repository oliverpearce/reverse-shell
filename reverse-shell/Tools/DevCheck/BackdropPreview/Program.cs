using System;
using System.IO;
using Combat.View;

// Preview harness: renders BackdropPainterCore with the ruins atlas (raw RGBA dump) to a raw RGB file.
class RawAtlas : IBackdropAtlas
{
    readonly byte[] data; readonly int w, h;
    public RawAtlas(string path)
    {
        var all = File.ReadAllBytes(path);
        w = BitConverter.ToInt32(all, 0); h = BitConverter.ToInt32(all, 4);
        data = new byte[all.Length - 8];
        Array.Copy(all, 8, data, 0, data.Length);
    }
    public bool Sample(int col, int row, int u, int v, out Rgb c)
    {
        int x = col * 64 + u * 4 + 2, y = row * 64 + v * 4 + 2; // top-down
        int i = (y * w + x) * 4;
        c = new Rgb(data[i] / 255f, data[i + 1] / 255f, data[i + 2] / 255f);
        return data[i + 3] > 127;
    }
}

static class Program
{
    static void Main(string[] args)
    {
        int W = 480, H = 270;
        var px = BackdropPainterCore.Paint(W, H, new BackdropParams(), new RawAtlas(args[0]));
        var outBytes = new byte[W * H * 3];
        for (int i = 0; i < px.Length; i++)
        {
            outBytes[i * 3] = (byte)(Math.Min(1f, px[i].R) * 255);
            outBytes[i * 3 + 1] = (byte)(Math.Min(1f, px[i].G) * 255);
            outBytes[i * 3 + 2] = (byte)(Math.Min(1f, px[i].B) * 255);
        }
        File.WriteAllBytes(args[1], outBytes);
    }
}
