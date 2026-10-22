# Gera src\Interface\app.ico: o olho de serpente em todos os tamanhos que o Windows usa.
# Uso: powershell -ExecutionPolicy Bypass -File ferramentas\gerar-icone.ps1
#
# Cada tamanho é desenhado de novo, não reduzido do 256: em 8x, reduzido por média, com as bordas
# horizontais do olho no limite dos pixels. Até 30 px a pupila vira uma fenda de lados retos e
# largura inteira, para não sumir num borrão. Os quadros são bitmap de 32 bits e só o 256 é PNG:
# o System.Drawing.Icon do .NET Framework, que a janela usa, não lê quadros PNG menores.
$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class SnakeEyeIcon
{
    // Ícone pequeno (16-32) e grande (32-64) de 100% a 200% de escala, modos do Explorer e 256.
    public static readonly int[] Sizes = { 16, 20, 24, 28, 30, 32, 36, 40, 48, 56, 60, 64, 72, 80, 96, 256 };
    const int Supersample = 8;
    static readonly Color TileColor = Color.FromArgb(14, 16, 15);
    static readonly Color EyeColor = Color.FromArgb(68, 214, 44);

    public static void Write(string path)
    {
        var frames = new List<byte[]>();
        foreach (int s in Sizes)
        {
            byte[] pixels = Render(s);
            frames.Add(s < 256 ? EncodeBitmap(pixels, s) : EncodePng(pixels, s));
        }
        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)Sizes.Length);
            int offset = 6 + 16 * Sizes.Length;
            for (int i = 0; i < Sizes.Length; i++)
            {
                byte b = (byte)(Sizes[i] >= 256 ? 0 : Sizes[i]);
                w.Write(b); w.Write(b); w.Write((byte)0); w.Write((byte)0);
                w.Write((ushort)1); w.Write((ushort)32); w.Write((uint)frames[i].Length); w.Write((uint)offset);
                offset += frames[i].Length;
            }
            foreach (byte[] f in frames) w.Write(f);
        }
    }

    // Desenho num tamanho, em pixels BGRA sem pré-multiplicar, linhas de cima para baixo.
    // Geometria na grade de 64 do conceito: fundo com raio 14, olho de (5,32) a (59,32) entre y 17 e 47,
    // pupila de y 15 a 49 com meia largura 5.
    public static byte[] Render(int s)
    {
        int f = Supersample, big = s * f;
        using (var bmp = new Bitmap(big, big, PixelFormat.Format32bppPArgb))
        {
            using (var g = Graphics.FromImage(bmp))
            using (var tile = new SolidBrush(TileColor))
            using (var eye = new SolidBrush(EyeColor))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                g.ScaleTransform(f, f);

                float m = s / 32;                   // margem: 0 até 30 px, 8 no 256
                float t = s - 2 * m, k = t / 64f, c = s / 2f;
                using (var path = RoundRect(m, m, t, t, 14 * k))
                    g.FillPath(tile, path);

                float top = (float)Math.Round(m + 17 * k, MidpointRounding.AwayFromZero), bottom = s - top;
                float tip = m + 5 * k, shoulder = m + 21 * k;
                g.FillPolygon(eye, new[] {
                    new PointF(tip, c), new PointF(shoulder, top), new PointF(s - shoulder, top),
                    new PointF(s - tip, c), new PointF(s - shoulder, bottom), new PointF(shoulder, bottom) });

                float pupilTop = m + 15 * k, pupilBottom = s - pupilTop, w, straight;
                if (s <= 30)
                {
                    w = Math.Max(1f, (float)Math.Round(3.75f * k, MidpointRounding.AwayFromZero));   // fenda reta: um pouco mais fina que o losango
                    straight = (c - top) / 2;
                }
                else
                {
                    w = 5 * k;
                    straight = 0;
                }
                g.FillPolygon(tile, new[] {
                    new PointF(c, pupilTop), new PointF(c + w, c - straight), new PointF(c + w, c + straight),
                    new PointF(c, pupilBottom), new PointF(c - w, c + straight), new PointF(c - w, c - straight) });
            }
            return Downsample(bmp, s, f);
        }
    }

    static GraphicsPath RoundRect(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath();
        float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // Média de cada bloco f x f sobre as cores pré-multiplicadas, que é a cobertura exata de cada pixel.
    static byte[] Downsample(Bitmap bmp, int s, int f)
    {
        int big = s * f;
        BitmapData data = bmp.LockBits(new Rectangle(0, 0, big, big), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        var src = new byte[data.Stride * big];
        Marshal.Copy(data.Scan0, src, 0, src.Length);
        int stride = data.Stride;
        bmp.UnlockBits(data);

        var dst = new byte[s * s * 4];
        int n = f * f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int sb = 0, sg = 0, sr = 0, sa = 0;
                for (int yy = 0; yy < f; yy++)
                {
                    int i = (y * f + yy) * stride + x * f * 4;
                    for (int xx = 0; xx < f; xx++, i += 4)
                    {
                        sb += src[i]; sg += src[i + 1]; sr += src[i + 2]; sa += src[i + 3];
                    }
                }
                int o = (y * s + x) * 4;
                int a = (sa + n / 2) / n;
                if (a == 0) continue;
                dst[o] = Unpremultiply(sb, sa);
                dst[o + 1] = Unpremultiply(sg, sa);
                dst[o + 2] = Unpremultiply(sr, sa);
                dst[o + 3] = (byte)a;
            }
        return dst;
    }

    static byte Unpremultiply(int sum, int sumAlpha)
    {
        return (byte)Math.Min(255, (sum * 255 + sumAlpha / 2) / sumAlpha);
    }

    // BITMAPINFOHEADER com altura dobrada, cores de baixo para cima e a máscara AND de 1 bit no fim.
    static byte[] EncodeBitmap(byte[] pixels, int s)
    {
        int maskStride = (s + 31) / 32 * 4;
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write(40); w.Write(s); w.Write(s * 2); w.Write((ushort)1); w.Write((ushort)32);
            w.Write(0); w.Write(s * s * 4 + maskStride * s); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            for (int y = s - 1; y >= 0; y--)
                w.Write(pixels, y * s * 4, s * 4);
            for (int y = s - 1; y >= 0; y--)
            {
                var row = new byte[maskStride];
                for (int x = 0; x < s; x++)
                    if (pixels[(y * s + x) * 4 + 3] == 0)
                        row[x / 8] |= (byte)(0x80 >> (x % 8));
                w.Write(row);
            }
            return ms.ToArray();
        }
    }

    static byte[] EncodePng(byte[] pixels, int s)
    {
        using (var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb))
        {
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, s, s), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            bmp.UnlockBits(data);
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }
}
'@
$out = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\Interface\app.ico'))
[SnakeEyeIcon]::Write($out)
Write-Output "icone gerado: $out"
