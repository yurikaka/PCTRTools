using NARCFileReadingDLL;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace PCTRTools;

internal class DrawChar
{
  public enum StyleType
  {
    BOTTOM_RIGHT = 0,
    TOP_LEFT = 1,
    ROUND = 2,
    BOTTOM_RIGHT_5 = 3
  }

  public enum FontType : int
  {
    SONG_TI = 0,
    HEI_TI = 1,
    MS_GOTHIC = 2,
    PIXEL_9 = 3
  }

  static readonly SKColor[] Colors =
  {
    new(255, 255, 0, 0),
    new(0, 0, 0, 255),
    new(128, 128, 128, 255),
    new(255, 0, 0, 128),
  };

  static readonly Dictionary<FontType, string> FontFiles = new()
  {
    { FontType.SONG_TI, "NotoSerifSC-VF.ttf" },
    { FontType.HEI_TI, "NotoSansMonoCJKjp-VF.ttf" },
    { FontType.MS_GOTHIC, "NotoSansMonoCJKjp-VF.ttf" },
    { FontType.PIXEL_9, "NotoSansMonoCJKjp-VF.ttf" },
  };

  static readonly Dictionary<FontType, SKTypeface> Typefaces = new();
  static Dictionary<FontType, System.Drawing.Font> WindowsFonts;

  static SKTypeface GetTypeface(FontType fontType)
  {
    if (Typefaces.TryGetValue(fontType, out var typeface))
    {
      return typeface;
    }

    var fontDirectory = Environment.GetEnvironmentVariable("PCTR_FONT_DIR");
    if (string.IsNullOrEmpty(fontDirectory))
    {
      throw new InvalidOperationException("PCTR_FONT_DIR is not set.");
    }
    var fontPath = Path.Combine(fontDirectory, FontFiles[fontType]);
    if (!File.Exists(fontPath))
    {
      throw new FileNotFoundException($"Font not found: {fontPath}", fontPath);
    }
    typeface = SKTypeface.FromFile(fontPath) ?? throw new InvalidOperationException($"Cannot load font: {fontPath}");
    Typefaces.Add(fontType, typeface);
    return typeface;
  }

  static public void SaveValuesToPng(VALUE[,] values, string path)
  {
    using var bitmap = ValuesToBitmap(values);
    using var image = SKImage.FromBitmap(bitmap);
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    using var output = File.Create(path);
    data.SaveTo(output);
  }

  static SKBitmap ValuesToBitmap(VALUE[,] values)
  {
    int width = values.GetLength(1), height = values.GetLength(0);
    var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    for (int x = 0; x < width; x++)
    {
      for (int y = 0; y < height; y++)
      {
        bitmap.SetPixel(x, y, Colors[(int)values[y, x]]);
      }
    }
    return bitmap;
  }

  static byte[,] RenderGlyph(char c, FontType fontType, int posX, int posY, int width, int height)
  {
    var alpha = new byte[height, width];
    if (OperatingSystem.IsWindowsVersionAtLeast(6, 1) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PCTR_FONT_DIR")))
    {
      WindowsFonts ??= new()
      {
        { FontType.SONG_TI, new System.Drawing.Font("新宋体", 12, System.Drawing.GraphicsUnit.Pixel) },
        { FontType.HEI_TI, new System.Drawing.Font("黑体", 12, System.Drawing.GraphicsUnit.Pixel) },
        { FontType.MS_GOTHIC, new System.Drawing.Font("MS Gothic", 12, System.Drawing.GraphicsUnit.Pixel) },
        { FontType.PIXEL_9, new System.Drawing.Font("Zfull-GB", 9, System.Drawing.GraphicsUnit.Pixel) },
      };
      using var bitmap = new System.Drawing.Bitmap(width, height);
      using var graphics = System.Drawing.Graphics.FromImage(bitmap);
      using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);
      graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
      graphics.DrawString(c.ToString(), WindowsFonts[fontType], brush, new System.Drawing.Point(posX, posY));
      for (int x = 0; x < width; x++)
      {
        for (int y = 0; y < height; y++)
        {
          alpha[y, x] = bitmap.GetPixel(x, y).A;
        }
      }
      return alpha;
    }

    using var skBitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var canvas = new SKCanvas(skBitmap);
    canvas.Clear(SKColors.Transparent);
    using var paint = new SKPaint
    {
      Color = SKColors.Black,
      IsAntialias = true,
    };
    using var font = new SKFont(GetTypeface(fontType), fontType == FontType.PIXEL_9 ? 9 : 12)
    {
      Edging = SKFontEdging.Antialias,
    };
    var baseline = posY - font.Metrics.Ascent;
    canvas.DrawText(c.ToString(), posX, baseline, SKTextAlign.Left, font, paint);
    canvas.Flush();
    for (int x = 0; x < width; x++)
    {
      for (int y = 0; y < height; y++)
      {
        alpha[y, x] = skBitmap.GetPixel(x, y).Alpha;
      }
    }
    return alpha;
  }

  static public VALUE[,] CharToValues(char c, StyleType type = StyleType.BOTTOM_RIGHT, FontType fontType = FontType.SONG_TI, int posX = -2, int posY = 1, int w = 16, int h = 16)
  {
    var alpha = RenderGlyph(c, fontType, posX, posY, w, h);

    int x, y;
    VALUE[,] v = new VALUE[w, h];
    for (x = 0; x < w; x++)
    {
      for (y = 0; y < h; y++)
      {
        v[y, x] = type == StyleType.BOTTOM_RIGHT ? VALUE.VALUE_3 : VALUE.VALUE_0;
      }
    }
    switch (type)
    {
      case StyleType.BOTTOM_RIGHT:
      case StyleType.BOTTOM_RIGHT_5:
        for (x = 0; x < w - 1; x++)
        {
          for (y = 0; y < h - 1; y++)
          {
            if (alpha[y, x] > 200)
            {
              v[y, x] = VALUE.VALUE_1;
              v[y + 1, x] = VALUE.VALUE_2;
              v[y, x + 1] = VALUE.VALUE_2;
              v[y + 1, x + 1] = VALUE.VALUE_2;
            }
          }
        }
        break;
      case StyleType.TOP_LEFT:
        for (x = w - 1; x > -1; x--)
        {
          for (y = h - 2; y > -1; y--)
          {
            if (alpha[y, x] > 200)
            {
              v[y + 1, x + 1] = VALUE.VALUE_1;
              v[y, x + 1] = VALUE.VALUE_3;
              v[y + 1, x] = VALUE.VALUE_3;
              v[y, x] = VALUE.VALUE_3;
            }
          }
        }
        break;
      case StyleType.ROUND:
        for (x = 0; x < w - 2; x++)
        {
          for (y = 0; y < h - 2; y++)
          {
            if (alpha[y, x] > 200)
            {
              v[y + 1, x + 1] = VALUE.VALUE_1;
              v[y, x] = v[y, x] == VALUE.VALUE_0 ? VALUE.VALUE_2 : v[y, x];
              v[y + 1, x] = v[y + 1, x] == VALUE.VALUE_0 ? VALUE.VALUE_2 : v[y + 1, x];
              v[y + 2, x] = v[y + 2, x] == VALUE.VALUE_0 ? VALUE.VALUE_2 : v[y + 2, x];
              v[y, x + 1] = v[y, x + 1] == VALUE.VALUE_0 ? VALUE.VALUE_2 : v[y, x + 1];
              v[y + 2, x + 1] = VALUE.VALUE_2;
              v[y, x + 2] = v[y, x + 2] == VALUE.VALUE_0 ? VALUE.VALUE_2 : v[y, x + 2];
              v[y + 1, x + 2] = VALUE.VALUE_2;
              v[y + 2, x + 2] = VALUE.VALUE_2;
            }
          }
        }
        break;
    }
    return v;
  }
}
