// Daniel Volpin

using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;
using System.Text;
using MathSupport;
using OpenTK;
using OpenTK.Graphics.ES20;
using OpenTK.Graphics.OpenGL;
using OpenTK.Platform.Windows;
using Utilities;

namespace _098svg
{
  public class CmdOptions : Options
  {
    /// <summary>
    /// Put your name here.
    /// </summary>
    public string name = "Daniel Volpin";

    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static new CmdOptions options = (CmdOptions)(Options.options = new CmdOptions());

    public override void StringStatistics (long[] result)
    {
      if (result == null || result.Length < 4)
        return;

      Util.StringStat(commands, result);
    }

    static CmdOptions ()
    {
      project = "svg098";
      TextPersistence.Register(new CmdOptions(), 0);

      RegisterMsgModes("debug");
    }

    public CmdOptions ()
    {
      // default values of structured members.
      baseDir = @"./";
    }

    public static void Touch ()
    {
      if (options == null)
        Util.Log("CmdOptions not initialized!");
    }

    //--- project-specific options ---

    /// <summary>
    /// Output directory with trailing dir separator.
    /// </summary>
    public string outDir = @"./";

    /// <summary>
    /// Number of maze columns (horizontal size in cells).
    /// </summary>
    public int columns = 12;

    /// <summary>
    /// Number of maze rows (vertical size in cells).
    /// </summary>
    public int rows = 8;

    /// <summary>
    /// Difficulty coefficient (optional).
    /// </summary>
    public double difficulty = 1.0;

    /// <summary>
    /// Maze width in SVG units (for SVG header).
    /// </summary>
    public double width = 600.0;

    /// <summary>
    /// Maze height in SVG units (for SVG header).
    /// </summary>
    public double height = 400.0;

    /// <summary>
    /// RandomJames generator seed, 0 for randomize.
    /// </summary>
    public long seed = 0L;

    /// <summary>
    /// Generate HTML5 file? (else - direct SVG format)
    /// </summary>
    public bool html = false;

    /// <summary>
    /// Start position in array Grid[]
    /// Positions are determined by x, y positions
    /// </summary>
    public (int,int) startPos = (0,0);

    /// <summary>
    /// Start position in array Grid[]
    /// Positions are determined by x, y positions
    /// </summary>
    public (int,int) endPos = (0,0);

    /// <summary>
    /// Parse additional keys.
    /// </summary>
    /// <param name="key">Key string (non-empty, trimmed).</param>
    /// <param name="value">Value string (non-null, trimmed).</param>
    /// <returns>True if recognized.</returns>
    public override bool AdditionalKey (string key, string value, string line)
    {
      if (base.AdditionalKey(key, value, line))
        return true;

      int newInt = 0;
      long newLong;
      double newDouble = 0.0;

      switch (key)
      {
        case "outDir":
          outDir = value;
          break;

        case "name":
          name = value;
          break;

        case "columns":
          if (int.TryParse(value, out newInt) &&
              newInt > 0)
            columns = newInt;
          break;

        case "rows":
          if (int.TryParse(value, out newInt) &&
              newInt > 0)
            rows = newInt;
          break;

        case "difficulty":
          if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out newDouble) &&
              newDouble > 0.0)
            difficulty = newDouble;
          break;

        case "width":
          if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out newDouble) &&
              newDouble > 0)
            width = newDouble;
          break;

        case "height":
          if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out newDouble) &&
              newDouble > 0)
            height = newDouble;
          break;

        case "seed":
          if (long.TryParse(value, out newLong) &&
              newLong >= 0L)
            seed = newLong;
          break;

        case "html":
          html = Util.positive(value);
          break;

        case "start":
          string[] start = value.Split(',');
          int startX = 0, startY = 0;
          if (int.TryParse(start[0], out newInt) && newInt >= 0)
            startX = newInt;
          if (int.TryParse(start[1], out newInt) && newInt >= 0)
            startY = newInt;
          startPos = (startX, startY);
          break;

        case "end":
          string[] end = value.Split(',');
          int endX = 0, endY = 0;
          if (int.TryParse(end[0], out newInt) && newInt >= 0)
            endX = newInt;
          if (int.TryParse(end[1], out newInt) && newInt >= 0)
            endY = newInt;
          endPos = (endX, endY);
          break;

        default:
          return false;
      }

      return true;
    }

    /// <summary>
    /// How to handle the "key=" config line?
    /// </summary>
    /// <returns>True if config line was handled.</returns>
    public override bool HandleEmptyValue (string key)
    {
      switch (key)
      {
        case "seed":
          seed = 0L;
          return true;
      }

      return false;
    }

    /// <summary>
    /// How to handle the non-key-value config line?
    /// </summary>
    /// <param name="line">The nonempty config line.</param>
    /// <returns>True if config line was handled.</returns>
    public override bool HandleCommand (string line)
    {
      switch (line)
      {
        case "generate":
          Program.Generate();
          return true;
      }

      return false;
    }
  }


  class Program
  {
    class Cell
    {
      public int row;
      public int col;
      public bool[] walls;
      public bool visited;
      public List<Cell> neighbors;

      public Cell (int x, int y) {
        row = x;
        col = y;
        walls = new bool[] { true, true, true, true };
        visited = false;
        neighbors = new List<Cell>();
      }

      public void findAdjacent (ref List<Cell> grid, int columns, int rows)
      {
        if (row > 0) // top neighbor
          neighbors.Add(grid[(row - 1) * columns + col]);
        if (col < columns - 1) // left neighbor
          neighbors.Add(grid[row * columns + (col + 1)]);
        if (row < rows - 1) // bottom neighbor
          neighbors.Add(grid[(row + 1) * columns + col]);
        if (col > 0) // right neighbor
          neighbors.Add(grid[row * columns + (col - 1)]);
      }
    }

    static void makeGrid (ref List<Cell> grid, int columns, int rows)
    {
      for (int y = 0; y < rows; y++)
      {
        for (int x = 0; x < columns; x++)
        {
          grid.Add(new Cell(y, x));
        }
      }

      for (int y = 0; y < rows; y++)
      {
        for (int x = 0; x < columns; x++)
        {
          grid[y * columns + x].findAdjacent(ref grid, columns, rows);
        }
      }
    }

    static Cell mazeGenerator (ref List<Cell> grid, int columns, (int, int) start)
    {
      Stack<Cell> stack = new Stack<Cell>();
      Cell current = grid[start.Item2 * columns + start.Item1];
      Cell last = current;

      if (!current.visited)
      {
        current.visited = true;
        stack.Push(current);
      }

      Random rnd = new Random();

      while (stack.Count > 0)
      {
        current = stack.Pop();

        List<Cell> unvisited = new List<Cell>();
        foreach (Cell neighbor in current.neighbors)
        {
          if (!neighbor.visited)
          {
            unvisited.Add(neighbor);
          }
        }

        if (unvisited.Count > 0)
        {
          stack.Push(current);

          Cell next = unvisited[rnd.Next(unvisited.Count)];

          int x = current.row - next.row;
          if (x == 1) // remove current top wall
          {
            current.walls[0] = false;
            next.walls[2] = false;
          }
          else if (x == -1) // remove current bottom wall
          {
            current.walls[2] = false;
            next.walls[0] = false;
          }

          int y = current.col - next.col;
          if (y == 1) // remove current left wall
          {
            current.walls[3] = false;
            next.walls[1] = false;
          }
          else if (y == -1) // remove current right wall
          {
            current.walls[1] = false;
            next.walls[3] = false;
          }

          next.visited = true;
          stack.Push(next);

          last = next;
        }
      }
      return last;
    }

    static void drawMaze (StreamWriter wri, List<Cell> grid, int columns, int rows, (int, int) start, (int, int) end)
    {
      Random rnd = new Random();

      for (int x = 0; x < columns; x++)
      {
        for (int y = 0; y < rows; y++)
        {
          bool top_wall = grid[y * columns + x].walls[0];
          bool right_wall = grid[y * columns + x].walls[1];
          bool bottom_wall = grid[y * columns + x].walls[2];
          bool left_wall = grid[y * columns + x].walls[3];

          // translate x,y coordinate input to grid coordinates
          (int, int) startCoordinate = (start.Item2 * columns + start.Item1, 0);
          (int, int) endCoordinate = (end.Item2 * columns + end.Item1 , 1);

          // only allow to put a start/end coordiante on the edges of the maze
          draw(wri, columns, rows, rnd, x, y, top_wall, right_wall, bottom_wall, left_wall, startCoordinate, endCoordinate);
        }
      }
    }

    private static void draw (StreamWriter wri, int columns, int rows, Random rnd, int x, int y, bool top_wall, bool right_wall, bool bottom_wall, bool left_wall, (int, int) startCoordinate, (int, int) endCoordinate)
    {
      if (y * columns + x == startCoordinate.Item1 || y * columns + x == endCoordinate.Item1)
      {
        drawStartEndPoint(wri, columns, rows, x, y, startCoordinate, endCoordinate);

        // open a random wall at the starting coordinate
        int value = rnd.Next(2);
        if (x == 0 && y == 0)
        {
          if (value == 0)
            drawPath(wri, x, y, x + 1, y);
          else
            drawPath(wri, x, y, x, y + 1);
        }
        if (x == columns - 1 && y == 0)
        {
          if (value == 0)
            drawPath(wri, x, y, x + 1, y);
          else
            drawPath(wri, x + 1, y, x + 1, y + 1);
        }
        if (y == rows - 1 && x == 0)
        {
          if (value == 0)
            drawPath(wri, x, y, x, y + 1);
          else
            drawPath(wri, x, y + 1, x + 1, y + 1);
        }
        if (y == rows - 1 && x == columns - 1)
        {
          if (value == 0)
            drawPath(wri, x, y + 1, x + 1, y + 1);
          else
            drawPath(wri, x + 1, y, x + 1, y + 1);
        }

      }
      else
      {
        if (top_wall)
          drawPath(wri, x, y, x + 1, y);
        if (right_wall)
          drawPath(wri, x + 1, y, x + 1, y + 1);
        if (bottom_wall)
          drawPath(wri, x, y + 1, x + 1, y + 1);
        if (left_wall)
          drawPath(wri, x, y, x, y + 1);
      }
    }

    private static void drawStartEndPoint (StreamWriter wri, int columns, int rows, int x, int y, (int, int) startCoordinate, (int, int) endCoordinate)
    {
      if (y == 0)
        drawDot(wri, x, y, columns, startCoordinate, endCoordinate);
      else if (x == 0)
        drawDot(wri, x, y, columns, startCoordinate, endCoordinate);
      else if (x == columns - 1)
        drawDot(wri, x, y, columns, startCoordinate, endCoordinate);
      else if (y == rows - 1)
        drawDot(wri, x, y, columns, startCoordinate, endCoordinate);
    }

    static void solveMaze (List<Cell> grid, int columns, int rows, (int, int) start, (int, int) end)
    {
      bool[] visited = new bool[rows * columns];
      for (int x = 0; x < columns; x++)
      {
        for (int y = 0; y < rows; y++)
        {
          visited[y * columns + x] = false;
        }
      }
      Stack<Cell> path = solvingMaze(grid, columns, start, end, ref visited);
      Console.WriteLine("x: " + path.Peek().row + ", y:" + path.Peek().col);
    }

    static Stack<Cell> solvingMaze (List<Cell> grid, int columns, (int, int) start, (int, int) end, ref bool[] visited)
    {
      int startX = start.Item1, startY = start.Item2;
      int endRow = end.Item1, endCol = end.Item2;

      Stack<Cell> path = new Stack<Cell>();
      Cell current = grid[startY * columns + startX];

      path.Push(current);
      visited[startY * columns + startX] = true;

      Random rand = new Random();

      while (grid[startY * columns + startX].row != endRow && grid[startY * columns + startX].col != endCol)
      {
        List<int> noWallIndex = new List<int>();
        for (int i = 0; i < 4; i++)
        {
          if (!grid[startY * columns + startX].walls[i])
          {
            noWallIndex.Add(i);
          }
        }

        int r = rand.Next(noWallIndex.Count);

        if (noWallIndex.Count > 0)
        {
          Cell nextCell;
          if (noWallIndex[r] == 0)
          {
            startY -= 1;
            nextCell = grid[startY * columns + startX];
            visited[startY * columns + startX] = true;
            path.Push(nextCell);
          }
          if (noWallIndex[r] == 1)
          {
            startX += 1;
            nextCell = grid[startY * columns + startX];
            visited[startY * columns + startX] = true;
            path.Push(nextCell);

          }
          if (noWallIndex[r] == 2)
          {
            startY += 1;
            nextCell = grid[startY * columns + startX];
            visited[startY * columns + startX] = true;
            path.Push(nextCell);

          }
          if (noWallIndex[r] == 3)
          {
            startX -= 1;
            nextCell = grid[startY * columns + startX];
            visited[startY * columns + startX] = true;
            path.Push(nextCell);
          }
        }
        else
        {
          path.Pop();
        } 

      } 

      return path;
    }

    /// <summary>
    /// The 'generate' command was executed at least once..
    /// </summary>
    static bool wasGenerated = false;

    static void Main (string[] args)
    {
      CmdOptions.Touch();

      if (args.Length < 1)
<<<<<<< Updated upstream
        Console.WriteLine( "Warning: no command-line options, using default values!" );
=======
        Console.WriteLine("Warning: no command-line options, using default values!");
>>>>>>> Stashed changes
      else
        for (int i = 0; i < args.Length; i++)
          if (!string.IsNullOrEmpty(args[i]))
          {
<<<<<<< Updated upstream
            string opt = args[i];
=======
            string opt = args[ i ];
>>>>>>> Stashed changes
            if (!CmdOptions.options.ParseOption(args, ref i))
              Console.WriteLine($"Warning: invalid option '{opt}'!");
          }

      if (!wasGenerated)
        Generate();
    }

<<<<<<< Updated upstream
    /// <summary>
    /// Writes one polyline in SVG format to the given output stream.
    /// </summary>
    /// <param name="wri">Opened output stream (must be left open).</param>
    /// <param name="workList">List of vertices.</param>
    /// <param name="x0">Origin - x-coord (will be subtracted from all x-coords).</param>
    /// <param name="y0">Origin - y-coord (will be subtracted from all y-coords)</param>
    /// <param name="color">Line color (default = black).</param>
    static void drawCurve (StreamWriter wri, List<Vector2> workList, double x0, double y0, string color = "#000")
=======
    static void drawPath (StreamWriter wri, double x0, double y0, double x1, double y1)
>>>>>>> Stashed changes
    {
      string color = "#000";
      StringBuilder sb = new StringBuilder();
<<<<<<< Updated upstream
      sb.AppendFormat(CultureInfo.InvariantCulture, "M{0:f2},{1:f2}",
                      workList[ 0 ].X - x0, workList[ 0 ].Y - y0);
      for (int i = 1; i < workList.Count; i++)
        sb.AppendFormat(CultureInfo.InvariantCulture, "L{0:f2},{1:f2}",
                        workList[ i ].X - x0, workList[ i ].Y - y0);

=======

      sb.Append("M " + (x0 * 4) + " " + (y0 * 4) + " ").Append("L " + (x1 * 4) + " " + (y1 * 4));

>>>>>>> Stashed changes
      wri.WriteLine("<path d=\"{0}\" stroke=\"{1}\" fill=\"none\"/>", sb.ToString(), color);
    }

    static void drawDot (StreamWriter wri, double x, double y, int columns, (int, int) startCoordinate, (int, int) endCoordinate)
    {
      if (y * columns + x == startCoordinate.Item1)
        wri.WriteLine("<circle cx=\"{0}\" cy=\"{1}\" r=\"1\" fill=\"red\"/>", 4 * x + 2 , 4 * y + 2);

      if (y * columns + x == endCoordinate.Item1)
        wri.WriteLine("<circle cx=\"{0}\" cy=\"{1}\" r=\"1\" fill=\"blue\"/>", 4 * x + 2, 4 * y + 2);
    }


    static public void Generate ()
    {

      List<Cell> grid = new List<Cell>();
      int columns = CmdOptions.options.columns;
      int rows = CmdOptions.options.rows;

      (int, int) startPos = CmdOptions.options.startPos;
      (int, int) endPos = CmdOptions.options.endPos;

      if (startPos.Item1 == 0 && startPos.Item2 == 0)
      {
        startPos = (0, 0);
      }

      if (endPos.Item1 == 0 && endPos.Item2 == 0)
      { 
        endPos = (columns-1, rows-1);
      }

      makeGrid(ref grid, columns, rows);
      mazeGenerator(ref grid, columns, startPos);
      solveMaze(grid, columns, rows, startPos, endPos);

      wasGenerated = true;

      string fileName = CmdOptions.options.outputFileName;
      if (string.IsNullOrEmpty(fileName))
        fileName = CmdOptions.options.html ? "out.html" : "out.svg";
      string outFn = Path.Combine(CmdOptions.options.outDir, fileName);

      // SVG output.
      using (StreamWriter wri = new StreamWriter(outFn))
      {
        if (CmdOptions.options.html)
        {
          wri.WriteLine("<!DOCTYPE html>");
          wri.WriteLine("<meta charset=\"utf-8\">");
          wri.WriteLine($"<title>SVG test ({CmdOptions.options.name})</title>");
          wri.WriteLine(string.Format(CultureInfo.InvariantCulture, "<svg width=\"{0:f0}\" height=\"{1:f0}\">",
                                      CmdOptions.options.width, CmdOptions.options.height));
        }
        else
          wri.WriteLine(string.Format(CultureInfo.InvariantCulture, "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{0:f0}\" height=\"{1:f0}\">",
                                      CmdOptions.options.width, CmdOptions.options.height));

<<<<<<< Updated upstream
        List<Vector2> workList = new List<Vector2>();
        RandomJames rnd = new RandomJames();
        if (CmdOptions.options.seed > 0L)
          rnd.Reset(CmdOptions.options.seed);
        else
          rnd.Randomize();

        for (int i = 0; i < CmdOptions.options.columns; i++)
          workList.Add(new Vector2(rnd.RandomFloat(0.0f, (float)CmdOptions.options.width),
                                   rnd.RandomFloat(0.0f, (float)CmdOptions.options.height)));

        drawCurve(wri, workList, 0, 0, string.Format("#{0:X2}{0:X2}{0:X2}", 0));

        wri.WriteLine("</svg>");

        // !!!}}
=======
        drawMaze(wri, grid, columns, rows, startPos, endPos);

        wri.WriteLine( "</svg>" );
>>>>>>> Stashed changes
      }
    }
  }
}
