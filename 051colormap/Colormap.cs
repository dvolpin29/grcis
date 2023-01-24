// Daniel Volpin

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace _051colormap
{
  class Colormap
  {
    /// <summary>
    /// Form data initialization.
    /// </summary>
    public static void InitForm (out string author)
    {
      author = "Daniel Volpin";
    }

    /// <summary>
    /// Generate a colormap based on input image.
    /// </summary>
    /// <param name="input">Input raster image.</param>
    /// <param name="numCol">Required colormap size (ignore it if you must).</param>
    /// <param name="colors">Output palette (array of colors).</param>
    public static void Generate (Bitmap input, int numCol, out Color[] colors)
    {
      int width  = input.Width;
      int height = input.Height;

      int low_r = input.GetPixel(0, 0).R;
      int low_g = input.GetPixel(0, 0).G;
      int low_b = input.GetPixel(0, 0).B;

      int up_r = 0;
      int up_g = 0;
      int up_b = 0;

      // initialize all the pixels
      HashSet<Color> pixels = InitializePixels (input, width, height, ref low_r, ref low_g, ref low_b, ref up_r, ref up_g, ref up_b);

      // find which RGB value has the widest range
      int red = up_r - low_r;
      int green = up_g - low_g;
      int blue = up_b - low_b;

      int max_bound = Math.Max(Math.Max(red, green), blue);

      Console.WriteLine("red: " + red + ", green: " + green + ", blue: " + blue);

      // sorting the list based on the widest individual RGB value
      List<Color> sortedList = SortList(pixels, red, green, max_bound);

      // cutting the list (cube) based on the required palette size 
      int colorPaletteSize = numCol;
      List<List<Color>> colorLists = CuttingList(sortedList, colorPaletteSize);

      // in the case that the image has less than 3 distinct RGB colors
      float totalValue = 0;
      float stepsize = 0;

      if (sortedList.Count < 3)
      {
        colors = new Color[colorPaletteSize];
        for (int i = 0; i < sortedList.Count; i++)
        {
          colors[i] = sortedList[i];
          totalValue += sortedList[i].GetBrightness();
        }

        stepsize = totalValue / colorPaletteSize;
        Console.WriteLine(stepsize);

        for (int i = sortedList.Count; i < colorPaletteSize; i++)
        {
          colors[i] = Color.FromArgb(255, (int)(colors[i-1].R * (1-stepsize)), (int)(colors[i - 1].G * (1 - stepsize)), (int)(colors[i - 1].B * (1 - stepsize)));
        }
      }
      else
      {
        // take the median color of each list (cube)
        colors = colorLists.Select(colorList => colorList.ElementAt(colorList.Count / 2)).ToArray();
      }
    }

    private static HashSet<Color> InitializePixels (Bitmap input, int width, int height, ref int low_r, ref int low_g, ref int low_b, ref int up_r, ref int up_g, ref int up_b)
    {
      HashSet<Color> initializedPixelsList = new HashSet<Color>();
      for (int x = 0; x < width; x++)
      {
        for (int y = 0; y < height; y++)
        {
          Color RGB = input.GetPixel(x,y);
          if (!initializedPixelsList.Contains(RGB))
          {
            low_r = Math.Min(low_r, RGB.R);
            low_g = Math.Min(low_g, RGB.G);
            low_b = Math.Min(low_b, RGB.B);

            up_r = Math.Max(up_r, RGB.R);
            up_g = Math.Max(up_g, RGB.G);
            up_b = Math.Max(up_b, RGB.B);

            initializedPixelsList.Add(RGB);
          }
        }
      }
      return initializedPixelsList;
    }
    private static List<Color> SortList (HashSet<Color> pixels, int red, int green, int max_bound)
    {
      List<Color> sortedList = new List<Color>();

      if (max_bound == red)
      {
        sortedList = pixels.OrderBy(kvp => kvp.R).ToList();
      }
      else if (max_bound == green)
      {
        sortedList = pixels.OrderBy(kvp => kvp.G).ToList();
      }
      else
      {
        sortedList = pixels.OrderBy(kvp => kvp.B).ToList();
      }

      return sortedList;
    }

    private static List<List<Color>> CuttingList (List<Color> sortedList, int colorPaletteSize)
    {
      List<List<Color>> colorLists = new List<List<Color>>();
      int listSize = sortedList.Count / colorPaletteSize;
      Console.WriteLine("size of the list: " + listSize);
      for (int i = 0; i < colorPaletteSize; i++)
      {
        var colorList = new List<Color>();
        for (int j = listSize * i; j < listSize * i + listSize; j++)
        {
          colorList.Add(sortedList.ElementAt(j));
        }
        colorLists.Add(colorList);
      }

      return colorLists;
    }

  }



}
