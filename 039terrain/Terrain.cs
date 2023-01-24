// Daniel Volpin

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MathSupport;
using OpenglSupport;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Platform.MacOS;
using TexLib;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;

namespace _039terrain
{
  public partial class Form1
  {
    public static int MAP_WIDTH;
    public static int MAP_HEIGHT;


    public static RandomJames rndJames = new RandomJames();

    /// <summary>
    /// Optional data initialization.
    /// </summary>
    static void InitParams (out int iterations, out float roughness, out string param, out string tooltip, out string name)
    {
      name = "Daniel Volpin";

      iterations = 0;
      roughness = 0.2f;
      param = "";
      tooltip = "tooltip";
    }

    #region GPU data

    /// <summary>
    /// Texture identifier (for one texture only, extend the source code if necessary)
    /// </summary>
    private int textureId = 0;

    private uint[] VBOid = new uint[2];   // [0] .. vertex array, [1] .. index buffer

    // vertex-buffer offsets:
    private int textureCoordOffset = 0;
    private int colorOffset        = 0;
    private int normalOffset       = 0;
    private int vertexOffset       = 0;
    private int stride             = 0;

    #endregion

    #region Lighting data

    // light:
    float[] ambientColor  = {0.1f, 0.1f, 0.1f};
    float[] diffuseColor  = {1.0f, 1.0f, 1.0f};
    float[] specularColor = {1.0f, 1.0f, 1.0f};
    float[] lightPosition = {1.0f, 1.0f, 0.0f};

    // material:
    float[] materialAmbient  = {0.1f, 0.1f, 0.1f};
    float[] materialDiffuse  = {0.8f, 0.8f, 0.8f};
    float[] materialSpecular = {0.5f, 0.5f, 0.5f};
    float  materialShininess = 60.0f;

    /// <summary>
    /// Current light position.
    /// </summary>
    Vector4 lightPos = Vector4.UnitY * 4.0f;

    /// <summary>
    /// Current light angle in radians.
    /// </summary>
    double lightAngle = 0.0;

    #endregion

    /// <summary>
    /// OpenGL init code.
    /// </summary>
    void InitOpenGL ()
    {
      // log OpenGL info just for curiosity:
      GlInfo.LogGLProperties();

      // OpenGL init code:
      glControl1.VSync = true;
      GL.ClearColor(Color.DarkBlue);
      GL.Enable(EnableCap.DepthTest);

      // VBO init:
      GL.GenBuffers(2, VBOid); // two buffers, one for vertex data, one for index data
      if (GL.GetError() != ErrorCode.NoError)
        throw new Exception("Couldn't create VBOs");

      GL.Light(LightName.Light0, LightParameter.Ambient, ambientColor);
      GL.Light(LightName.Light0, LightParameter.Diffuse, diffuseColor);
      GL.Light(LightName.Light0, LightParameter.Specular, specularColor);
    }

    private void glControl1_Load (object sender, EventArgs e)
    {
      // cold start
      InitOpenGL();

      // warm start
      SetupViewport();

      // initialize the scene
      int iterations  = (int)upDownIterations.Value;
      float roughness = (float)upDownRoughness.Value;
      Regenerate(iterations, roughness, textParam.Text);
      labelStatus.Text = "Triangles: " + scene.Triangles;

      // initialize the simulation
      InitSimulation(true);

      loaded = true;
      Application.Idle += new EventHandler(Application_Idle);
    }

    /// <summary>
    /// [Re-]generate the terrain data.
    /// </summary>
    /// <param name="iterations">Number of subdivision iteratons.</param>
    /// <param name="roughness">Roughness parameter.</param>
    /// <param name="param">Optional text parameter.</param>
    private void Regenerate (int iterations, float roughness, string param)
    {
      scene.Reset();


      string[] options;

      Bitmap image = null;
      string biome = "";
      int dim = 0;

      double[,] map;

      if (param.Length > 0)
      {
        options = param.Split(',');

        for (int i = 0; i < options.Length; i++)
        {

          string[] option = options[i].Split('=');

          if (option.Length == 2)
          {
            switch (option[0].Trim())
            {
              case "dim": // dim = dim^2 + 1 -> input values between [0,10]
                dim = int.Parse(option[1]);
                break;
              case "file":
                if (File.Exists(option[1]))
                  image = new Bitmap(option[1]);
                break;
              case "biome":
                switch (option[1])
                {
                  case "desert":
                    biome = option[1];
                    break;
                  case "forest":
                    biome = option[1];
                    break;
                }
                break;
            }
          }
          else
          {
            Console.WriteLine("Information not filled correctly");
          }
        }
      }

      if (image != null)
      {
        map = LoadBitmapImage(image);
      }
      else
      {
        int map_width_height = InitMap(iterations, out map, dim);
        double lower = 0, upper = 1.0;
        DiamondSquare(ref map, map_width_height - 1, ref lower, ref upper, roughness, iterations);
      }

      int[,] scene_vertices = AddVertices(map);
      Vector3[,] normals = SaveNormals(scene_vertices);
      SetNormal(scene_vertices, normals);
      SetColor(scene_vertices, biome);
      AddTriangles(scene_vertices);

      PrepareData();

      if (textureId > 0)
      {
        GL.DeleteTexture(textureId);
        textureId = 0;
      }
      textureId = TexUtil.CreateTextureFromFile("cgg256.png", "../../cgg256.png");

      InitSimulation(false);

    }

    private void FilterMap (ref double[,] map)
    {
      List<double> new_map = new List<double>();

      for (int i = 0; i <= MAP_HEIGHT - 3; i++)
      {
        for (int j = 0; j <= MAP_WIDTH - 3; j++)
        {
          for (int x = i; x <= i + 2; x++)
          {
            for (int y = j; y<= j + 2; y++)
            {
              new_map.Add(map[x,y]);
            }
          }
          double[] terms = new_map.ToArray();
          new_map.Clear();
          Array.Sort<double>(terms);
          Array.Reverse(terms);
          double color = terms[4];
          map[i + 1, j + 1] = color;
        }
      }
    }

    private static double[,] LoadBitmapImage (Bitmap image)
    {
      MAP_WIDTH = image.Width;
      MAP_HEIGHT = image.Height;

      double[,] map = new double[MAP_HEIGHT, MAP_WIDTH];

      for (int z = 0; z <  MAP_HEIGHT; z++)
      {
        for (int x = 0; x < MAP_WIDTH; x++)
        {
          Color pixel_color = image.GetPixel(z, x);
          float avg_color = (pixel_color.R + pixel_color.G + pixel_color.B) / 3;
          map[z, x] = avg_color / 255;
        }
      }

      return map;
    }

    private static int InitMap (int iterations, out double[,] map, int dim)
    {
      int height_map_width = (int)Math.Pow(2, iterations) + 1;

      MAP_WIDTH = height_map_width;
      MAP_HEIGHT = height_map_width;

      if (dim != 0)
      {
        height_map_width = (int)Math.Pow(2, dim) + 1;

        MAP_WIDTH = height_map_width;
        MAP_HEIGHT = height_map_width;

        map = new double[MAP_HEIGHT, MAP_WIDTH];
      }
      else
      {
        map = new double[MAP_HEIGHT, MAP_WIDTH];
      }

      for (int i = 0; i < MAP_HEIGHT; i++)
      {
        for (int j = 0; j < MAP_WIDTH; j++)
        {
          map[i, j] = 0;
        }
      }

      map[0, 0] = 1;
      map[MAP_HEIGHT - 1, 0] = 0;
      map[0, MAP_WIDTH - 1] = 0;
      map[MAP_HEIGHT - 1, MAP_WIDTH - 1] = 0;

      return height_map_width;
    }

    private static void DiamondSquare (ref double[,] map, int step_size, ref double lower, ref double upper, float roughness, float iterations)
    {
      int half_step = step_size / 2;

      if (half_step < 1)
        return;

      //square steps
      for (int z = half_step; z < MAP_HEIGHT; z += step_size)
        for (int x = half_step; x < MAP_WIDTH; x += step_size)
          SquareStep(ref map, x % MAP_WIDTH, z % MAP_HEIGHT, half_step, ref lower, ref upper, roughness, iterations);

      // diamond steps
      int col = 0;
      for (int x = 0; x < MAP_WIDTH; x += half_step)
      {
        col++; // odd and even columns.
        if (col % 2 == 1)
          for (int z = half_step; z < MAP_HEIGHT; z += step_size)
            DiamondStep(ref map, x % MAP_WIDTH, z % MAP_HEIGHT, half_step, ref lower, ref upper, roughness);
        else
          for (int z = 0; z < MAP_HEIGHT; z += step_size)
            DiamondStep(ref map, x % MAP_WIDTH, z % MAP_HEIGHT, half_step, ref lower, ref upper, roughness);
      }

      upper = Math.Max(upper/1.65, 0); //reduce upper range exponentially
      DiamondSquare(ref map, step_size / 2, ref lower, ref upper, roughness, iterations);
    }

    public static void SquareStep (ref double[,] map, int x, int z, int step_size, ref double lower, ref double upper, float roughness, float iterations)
    {
      int count = 0;
      double avg = 0.0f;

      if (x - step_size >= 0 && z - step_size >= 0)
      {
        avg += map[x - step_size, z - step_size];
        count++;
      }
      if (x - step_size >= 0 && z + step_size < MAP_HEIGHT)
      {
        avg += map[x - step_size, z + step_size];
        count++;
      }
      if (x + step_size < MAP_WIDTH && z - step_size >= 0)
      {
        avg += map[x + step_size, z - step_size];
        count++;
      }
      if (x + step_size < MAP_WIDTH && z + step_size < MAP_HEIGHT)
      {
        avg += map[x + step_size, z + step_size];
        count++;
      }

      if (iterations < 6)
        avg += RandRange(lower, upper);

      if (roughness >= 0.1)
        avg += Math.Min(1, roughness / 100);

      avg /= count;
      map[x, z] = avg;
    }

    public static void DiamondStep (ref double[,] map, int x, int z, int reach, ref double lower, ref double upper, float roughness)
    {
      int count = 0;
      double avg = 0.0f;

      if (x - reach >= 0)
      {
        avg += map[x - reach, z];
        count++;
      }
      if (x + reach < MAP_WIDTH)
      {
        avg += map[x + reach, z];
        count++;
      }
      if (z - reach >= 0)
      {
        avg += map[x, z - reach];
        count++;
      }
      if (z + reach < MAP_HEIGHT)
      {
        avg += map[x, z + reach];
        count++;
      }

      avg += RandRange(lower, upper);
      avg /= count;
      map[x, z] = avg;
    }

    private static double RandRange (double min, double max)
    {
      return (rndJames.UniformNumber() - 0.5) * 2 * (max - min);
    }

    private void SetColor (int[,] sceneVertices, string biome)
    {
      Random rnd = new Random();

      Vector3 color_0 = new Vector3(1f, 1f, 1f);
      Vector3 color_1 = new Vector3(1f, 1f, 1f);
      Vector3 color_2 = new Vector3(1f, 1f, 1f);
      Vector3 color_3 = new Vector3(1f, 1f, 1f);
      Vector3 color_4 = new Vector3(1f, 1f, 1f);
      Vector3 color_5 = new Vector3(1f, 1f, 1f);
      Vector3 color_6 = new Vector3(1f, 1f, 1f);
      Vector3 color_7 = new Vector3(1f, 1f, 1f);
      Vector3 color_8 = new Vector3(1f, 1f, 1f);


      // sea to land order
      switch (biome)
      {
        case "desert":

          color_0 = new Vector3(0, 60 / 255F, 148 / 255F);
          color_1 = new Vector3(0, 80 / 255F, 148 / 255F);
          color_2 = new Vector3(218 / 255F, 165 / 255F, 32 / 255F);
          color_3 = new Vector3(205 / 255F, 133 / 255F, 63 / 255F);
          color_4 = new Vector3(136 / 255F, 69 / 255F, 19 / 255F);
          color_5 = new Vector3(120 / 255F, 69 / 255F, 19 / 255F);
          color_6 = new Vector3(100 / 255F, 69 / 255F, 19 / 255F);
          color_7 = new Vector3(27 / 255F, 27 / 255F, 27 / 255F);
          color_8 = new Vector3(1F, 1F, 1F);

          break;

        case "forest":

          color_0 = new Vector3(0 / 255F, 51 / 255F, 102 / 255F);
          color_1 = new Vector3(0 / 255F, 64 / 255F, 128 / 255F);
          color_2 = new Vector3(74 / 255F, 103 / 255F, 65 / 255F);
          color_3 = new Vector3(63 / 255F, 90 / 255F, 54 / 255F);
          color_4 = new Vector3(55 / 255F, 79 / 255F, 47 / 255F);
          color_5 = new Vector3(48 / 255F, 69 / 255F, 41 / 255F);
          color_6 = new Vector3(104 / 255F, 79 / 255F, 75 / 255F);
          color_7 = new Vector3(76 / 255F, 55 / 255F, 50 / 255F);
          color_8 = new Vector3(1F, 1F, 1F);

          break;

      }

      for (int z = 0; z < MAP_HEIGHT; z++)
      {
        for (int x = 0; x < MAP_WIDTH; x++)
        {
          float height = scene.GetVertex(sceneVertices[z, x]).Y;
          if (height < 0.003)
          {
            scene.SetColor(sceneVertices[z, x], color_0);
          }
          else if (height < 0.007)
          {
            scene.SetColor(sceneVertices[z, x], color_1);
          }
          else if (height < 0.01)
          {
            scene.SetColor(sceneVertices[z, x], color_2);
          }
          else if (height < 0.025)
          {
            int rnd_value = rnd.Next(-1, 2);
            if (rnd_value == 0)
              scene.SetColor(sceneVertices[z, x], color_2);
            else
              scene.SetColor(sceneVertices[z, x], color_3);
          }
          else if (height < 0.05)
          {
            scene.SetColor(sceneVertices[z, x], color_3);
          }
          else if (height < 0.1)
          {
            scene.SetColor(sceneVertices[z, x], color_4);
          }
          else if (height < 0.125)
          {
            int rnd_value = rnd.Next(-1, 2);
            if (rnd_value == 0)
              scene.SetColor(sceneVertices[z, x], color_4);
            else
              scene.SetColor(sceneVertices[z, x], color_5);
          }
          else if (height < 0.15)
          {
            scene.SetColor(sceneVertices[z, x], color_5);
          }
          else if (height < 0.175)
          {
            int rnd_value = rnd.Next(-1, 2);
            if (rnd_value == 0)
              scene.SetColor(sceneVertices[z, x], color_5);
            else
              scene.SetColor(sceneVertices[z, x], color_6);
          }
          else if (height < 0.20)
          {
            scene.SetColor(sceneVertices[z, x], color_6);
          }
          else if (height < 0.225)
          {
            int rnd_value = rnd.Next(-1, 2);
            if (rnd_value == 0)
              scene.SetColor(sceneVertices[z, x], color_6);
            else
              scene.SetColor(sceneVertices[z, x], color_7);
          }
          else
          {
            scene.SetColor(sceneVertices[z, x], color_8);
          }
        }
      }
    }

    private void AddTriangles (int[,] sceneVertices)
    {
      for (int z = 0; z < MAP_HEIGHT - 1; z++)
      {
        for (int x = 0; x < MAP_WIDTH - 1; x++)
        {
          int top_left = sceneVertices[z, x];
          int top_right = sceneVertices[z + 1, x];
          int bottom_left = sceneVertices[z, x + 1];
          int bottom_right = sceneVertices[z + 1, x + 1];

          scene.AddTriangle(top_left, top_right, bottom_left);
          scene.AddTriangle(bottom_right, top_right, bottom_left);
        }
      }
    }

    private void SetNormal (int[,] sceneVertices, Vector3[,] normals)
    {
      for (int z = 0; z < MAP_HEIGHT; z++)
      {
        for (int x = 0; x < MAP_WIDTH; x++)
        {
          Vector3 normal_sum = new Vector3(0.0f, 0.0f, 0.0f);

          if (z - 1 >= 0 && x - 1 >= 0) // bottom left face
            normal_sum += normals[z - 1, x - 1];

          if (z < MAP_HEIGHT - 1 && x - 1 >= 0) // bottom right face
            normal_sum += normals[z, x - 1];

          if (z - 1 >= 0 && x < MAP_WIDTH - 1) // top left face
            normal_sum += normals[z - 1, x];

          if (z < MAP_HEIGHT - 1 && x < MAP_WIDTH - 1) // top right face
            normal_sum += normals[z, x];

          Vector3 vec = normal_sum.Normalized();
          scene.SetNormal(sceneVertices[z, x], vec);
        }
      }
    }

    private Vector3[,] SaveNormals (int[,] sceneVertices)
    {
      Vector3[,] normals = new Vector3[MAP_HEIGHT, MAP_WIDTH];
      for (int z = 0; z < MAP_HEIGHT - 1; z++)
      {
        for (int x = 0; x < MAP_WIDTH - 1; x++)
        {
          Vector3 top_left = scene.GetVertex(sceneVertices[z, x]);
          Vector3 bottom_left = scene.GetVertex(sceneVertices[z, x + 1]);
          Vector3 bottom_right = scene.GetVertex(sceneVertices[z + 1, x + 1]);

          Vector3 side1 = top_left - bottom_left;
          Vector3 side2 = bottom_right - bottom_left;

          Vector3 normal =  Vector3.Cross(side2, side1).Normalized();

          normals[z, x] = normal;
        }
      }

      return normals;
    }

    private int[,] AddVertices (double[,] map)
    {
      float minF = -0.5f;
      float maxF = 0.5f;
      float diff_min_max = maxF - minF;

      int[,] sceneVertices = new int[MAP_HEIGHT,MAP_WIDTH];
      for (int z = 0; z < MAP_HEIGHT; z++)
      {
        for (int x = 0; x < MAP_WIDTH; x++)
        {
          float y_coord = (float)map[z, x];
          float x_coord = z * (diff_min_max / (MAP_HEIGHT - 1)) + minF;
          float z_coord = x * (diff_min_max / (MAP_WIDTH - 1)) + minF;
          sceneVertices[z, x] = scene.AddVertex(new Vector3(x_coord, y_coord * 0.3f, z_coord));
        }
      }

      return sceneVertices;
    }

    private static double[,] TestNoiseGenerationPerlin (int height_map_width, ref double[,] map, float roughness)
    {
      int map_width = height_map_width;
      int map_height = height_map_width;

      Random rand = new Random();
      double new_value;
      double top_range = 0;
      double bottom_range = 0;

      for (int z = 0; z < map_height - 1; z += 1)
      {
        for (int x = 0; x < map_width - 1; x += 1)
        {
          if (z == 0 && x == 0)
            continue;

          if (z == 0)
            new_value = (int)map[z, x - 1] + rand.Next(-1000, 1000);
          else if (x == 0)
            new_value = (int)map[z - 1, x] + rand.Next(-1000, 1000);
          else
          {
            double minimum = Math.Min(map[z, x - 1], map[z - 1, x]);
            double maximum = Math.Max(map[z, x - 1], map[z - 1, x]);
            double avg_value = minimum + (maximum - minimum) / 2.0;
            new_value = avg_value + rand.Next(-1000, 1000);
          }

          map[z, x] = new_value;

          if (new_value < bottom_range)
            bottom_range = new_value;
          if (new_value > top_range)
            top_range = new_value;

        }
      }

      double diff = top_range - bottom_range;

      for (int z = 0; z < map_height - 1; z += 1)
      {
        for (int x = 0; x < map_width - 1; x += 1)
        {
          map[z, x] = (map[z, x] - bottom_range) / diff; // normalise the range
        }
      }

      return map;
    }


    /// <summary>
    /// last simulated time in seconds.
    /// </summary>
    double simTime = 0.0;

    /// <summary>
    /// Are we doing the terrain-flyover?
    /// </summary>
    bool hovercraft = false;

    /// <summary>
    /// Init of animation / hovercraft simulation, ...
    /// </summary>
    /// <param name="cold">True for global reset (including light-source/vehicle position..)</param>
    private void InitSimulation (bool cold)
    {
      if (hovercraft)
      {
        // !!! TODO: hovercraft init
      }
      else
        if (cold)
      {
        lightPos = new Vector4(lightPosition[0], lightPosition[1], lightPosition[2], 1.0f);
        lightAngle = 0.0;
      }

      long nowTicks = DateTime.Now.Ticks;
      simTime = nowTicks * 1.0e-7;
    }

    private void glControl1_Paint (object sender, PaintEventArgs e)
    {
      if (checkAnim.Checked)
        Simulate(DateTime.Now.Ticks * 1.0e-7);

      Render();
    }

    /// <summary>
    /// One step of animation / hovercraft simulation.
    /// </summary>
    /// <param name="time"></param>
    private void Simulate (double time)
    {
      if (!loaded ||
          time <= simTime)
        return;

      double dt = time - simTime;   // delta-time in seconds

      if (hovercraft)
      {
        // !!! TODO: hovercraft simulation
      }

      lightAngle += dt;             // one radian per second..
      Matrix4 m;
      Matrix4.CreateRotationY((float)lightAngle, out m);
      lightPos = Vector4.Transform(m, new Vector4(lightPosition[0], lightPosition[1], lightPosition[2], 1.0f));

      simTime = time;
    }

    /// <summary>
    /// Rendering of one frame.
    /// </summary>
    private void Render ()
    {
      if (!loaded)
        return;

      frameCounter++;

      // frame init:
      GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
      GL.ShadeModel(checkSmooth.Checked ? ShadingModel.Smooth : ShadingModel.Flat);
      GL.PolygonMode(MaterialFace.FrontAndBack,
                     checkWireframe.Checked ? PolygonMode.Line : PolygonMode.Fill);

      // camera:
      SetCamera();

      // OpenGL lighting:
      GL.MatrixMode(MatrixMode.Modelview);
      GL.Light(LightName.Light0, LightParameter.Position, lightPos);
      GL.Enable(EnableCap.Light0);
      GL.Enable(EnableCap.Lighting);

      // texturing:
      bool useTexture = scene.HasTxtCoords() &&
                        checkTexture.Checked &&
                        textureId > 0;
      if (useTexture)
      {
        // set up the texture:
        GL.BindTexture(TextureTarget.Texture2D, textureId);
        GL.Enable(EnableCap.Texture2D);
      }
      else
      {
        GL.Disable(EnableCap.Texture2D);

        GL.ColorMaterial(MaterialFace.Front, ColorMaterialParameter.AmbientAndDiffuse);
        GL.Enable(EnableCap.ColorMaterial);
      }

      // common lighting colors/parameters:
      GL.Material(MaterialFace.Front, MaterialParameter.Ambient, materialAmbient);
      GL.Material(MaterialFace.Front, MaterialParameter.Diffuse, materialDiffuse);
      GL.Material(MaterialFace.Front, MaterialParameter.Specular, materialSpecular);
      GL.Material(MaterialFace.Front, MaterialParameter.Shininess, materialShininess);

      // scene -> vertex buffer & index buffer

      // bind the vertex buffer:
      GL.BindBuffer(BufferTarget.ArrayBuffer, VBOid[0]);

      // tell OGL what sort of data we have and where in the buffer they could be found
      // the buffers we get from SceneBrep are interleaved => stride != 0
      if (useTexture)
        GL.TexCoordPointer(2, TexCoordPointerType.Float, stride, textureCoordOffset);

      if (scene.HasColors())
        GL.ColorPointer(3, ColorPointerType.Float, stride, colorOffset);

      if (scene.HasNormals())
        GL.NormalPointer(NormalPointerType.Float, stride, normalOffset);

      GL.VertexPointer(3, VertexPointerType.Float, stride, vertexOffset);

      // bind the index buffer:
      GL.BindBuffer(BufferTarget.ElementArrayBuffer, VBOid[1]);

      // draw the geometry:
      triangleCounter += scene.Triangles;
      GL.DrawElements(BeginMode.Triangles, scene.Triangles * 3, DrawElementsType.UnsignedInt, 0);

      if (useTexture)
        GL.BindTexture(TextureTarget.Texture2D, 0);
      else
        GL.Disable(EnableCap.ColorMaterial);
      GL.Disable(EnableCap.Light0);
      GL.Disable(EnableCap.Lighting);

      // light-source rendering (small white rectangle):
      GL.PointSize(3.0f);
      GL.Begin(PrimitiveType.Points);
      GL.Color3(1.0f, 1.0f, 1.0f);
      GL.Vertex4(lightPos);
      GL.End();

      // swap buffers:
      glControl1.SwapBuffers();
    }

    #region Camera attributes

    /// <summary>
    /// Current "up" vector.
    /// </summary>
    private Vector3 up = Vector3.UnitY;

    /// <summary>
    /// Vertical field-of-view angle in radians.
    /// </summary>
    private float fov = 1.0f;

    /// <summary>
    /// Camera's near point.
    /// </summary>
    private float near = 0.1f;

    /// <summary>
    /// Camera's far point.
    /// </summary>
    private float far = 200.0f;

    /// <summary>
    /// Current elevation angle in radians.
    /// </summary>
    private double elevationAngle = 0.1;

    /// <summary>
    /// Current azimuth angle in radians.
    /// </summary>
    private double azimuthAngle = 0.0;

    /// <summary>
    /// Current zoom factor.
    /// </summary>
    private double zoom = 2.0;

    #endregion

    /// <summary>
    /// Function called whenever the main application is idle..
    /// </summary>
    private void Application_Idle (object sender, EventArgs e)
    {
      while (glControl1.IsIdle)
      {
        glControl1.Invalidate();                // causes the GLcontrol 'repaint' action

        long now = DateTime.Now.Ticks;
        if (now - lastFpsTime > 5000000)        // more than 0.5 sec
        {
          lastFps = 0.5 * lastFps + 0.5 * (frameCounter * 1.0e7 / (now - lastFpsTime));
          lastTps = 0.5 * lastTps + 0.5 * (triangleCounter * 1.0e7 / (now - lastFpsTime));
          lastFpsTime = now;
          frameCounter = 0;
          triangleCounter = 0L;

          if (lastTps < 5.0e5)
            labelFps.Text = string.Format(CultureInfo.InvariantCulture, "Fps: {0:f1}, tps: {1:f0}k",
                                          lastFps, (lastTps * 1.0e-3));
          else
            labelFps.Text = string.Format(CultureInfo.InvariantCulture, "Fps: {0:f1}, tps: {1:f1}m",
                                          lastFps, (lastTps * 1.0e-6));
        }
      }
    }

    /// <summary>
    /// Called in case the GLcontrol geometry changes.
    /// </summary>
    private void SetupViewport ()
    {
      int width  = glControl1.Width;
      int height = glControl1.Height;

      // 1. set ViewPort transform:
      GL.Viewport(0, 0, width, height);

      // 2. set projection matrix
      GL.MatrixMode(MatrixMode.Projection);
      Matrix4 proj = Matrix4.CreatePerspectiveFieldOfView(fov, width / (float)height, near, far);
      GL.LoadMatrix(ref proj);
    }

    private void ResetCamera ()
    {
      elevationAngle = 0.1;
      azimuthAngle = 0.0;
      zoom = 2.0;
    }

    /// <summary>
    /// Camera setup, called for every frame prior to any rendering.
    /// </summary>
    private void SetCamera ()
    {
      if (hovercraft)
      {
        // !!! TODO: hovercraft camera
      }
      else
      {
        Vector3 cameraPosition = new Vector3(0.0f, 0, (float)zoom);

        Matrix4 rotateX = Matrix4.CreateRotationX((float)-elevationAngle);
        Matrix4 rotateY = Matrix4.CreateRotationY((float)azimuthAngle);

        cameraPosition = Vector3.TransformPosition(cameraPosition, rotateX);
        cameraPosition = Vector3.TransformPosition(cameraPosition, rotateY);

        GL.MatrixMode(MatrixMode.Modelview);
        Matrix4 lookAt = Matrix4.LookAt(cameraPosition, Vector3.Zero, up);

        GL.LoadMatrix(ref lookAt);
      }
    }

    /// <summary>
    /// Prepare VBO content and upload it to the GPU.
    /// You probably don't need to change this function..
    /// </summary>
    private void PrepareData ()
    {
      Debug.Assert(scene != null, "Missing scene");

      if (scene.Triangles == 0)
        return;

      // enable the respective client states
      GL.EnableClientState(ArrayCap.VertexArray);   // vertex array (positions?)

      if (scene.HasColors())                        // colors, if any
        GL.EnableClientState(ArrayCap.ColorArray);

      if (scene.HasNormals())                       // normals, if any
        GL.EnableClientState(ArrayCap.NormalArray);

      if (scene.HasTxtCoords())                     // textures, if any
        GL.EnableClientState(ArrayCap.TextureCoordArray);

      // bind the vertex array (interleaved)
      GL.BindBuffer(BufferTarget.ArrayBuffer, VBOid[0]);

      // query the size of the buffer in bytes
      int vertexBufferSize = scene.VertexBufferSize(
          true, // we always have vertex data
          scene.HasTxtCoords(),
          scene.HasColors(),
          scene.HasNormals());

      // fill vertexData with data we will upload to the (vertex) buffer on the graphics card
      float[] vertexData = new float[vertexBufferSize / sizeof(float)];

      // calculate the offsets in the interleaved array
      textureCoordOffset = 0;
      colorOffset = textureCoordOffset + scene.TxtCoordsBytes();
      normalOffset = colorOffset + scene.ColorBytes();
      vertexOffset = normalOffset + scene.NormalBytes();

      // convert data from SceneBrep to float[] (interleaved array)
      unsafe
      {
        fixed (float* fixedVertexData = vertexData)
        {
          stride = scene.FillVertexBuffer(
              fixedVertexData,
              true,
              scene.HasTxtCoords(),
              scene.HasColors(),
              scene.HasNormals());

          // upload vertex data to the graphics card
          GL.BufferData(
              BufferTarget.ArrayBuffer,
              (IntPtr)vertexBufferSize,
              (IntPtr)fixedVertexData,        // still pinned down to fixed address..
              BufferUsageHint.StaticDraw);
        }
      }

      // index buffer:
      GL.BindBuffer(BufferTarget.ElementArrayBuffer, VBOid[1]);

      // convert indices from SceneBrep to uint[]
      uint[] indexData = new uint[scene.Triangles * 3];

      unsafe
      {
        fixed (uint* unsafeIndexData = indexData)
        {
          scene.FillIndexBuffer(unsafeIndexData);

          // upload index data to video memory
          GL.BufferData(
              BufferTarget.ElementArrayBuffer,
              (IntPtr)(scene.Triangles * 3 * sizeof(uint)),
              (IntPtr)unsafeIndexData,        // still pinned down to fixed address..
              BufferUsageHint.StaticDraw);
        }
      }
    }

    private void glControl1_KeyDown (object sender, KeyEventArgs e)
    {
      // !!!{{ TODO: add the event handler here
      // !!!}}
    }

    private void glControl1_KeyUp (object sender, KeyEventArgs e)
    {
      // !!!{{ TODO: add the event handler here
      // !!!}}
    }

    private int dragFromX = 0;
    private int dragFromY = 0;
    private bool dragging = false;

    private void glControl1_MouseDown (object sender, MouseEventArgs e)
    {
      if (hovercraft)
      {
        // !!! TODO: hovercraft
      }
      else
      {
        dragFromX = e.X;
        dragFromY = e.Y;
        dragging = true;
      }
    }

    private void glControl1_MouseUp (object sender, MouseEventArgs e)
    {
      if (hovercraft)
      {
        // !!! TODO: hovercraft
      }
      else
      {
        dragging = false;
      }
    }

    private void glControl1_MouseMove (object sender, MouseEventArgs e)
    {
      if (hovercraft)
      {
        // !!! TODO: hovercraft
      }
      else
      {
        if (!dragging)
          return;

        int delta;
        if (e.X != dragFromX)       // change the azimuth angle
        {
          delta = e.X - dragFromX;
          dragFromX = e.X;
          azimuthAngle -= delta * 4.0 / glControl1.Width;
        }

        if (e.Y != dragFromY)       // change the elevation angle
        {
          delta = e.Y - dragFromY;
          dragFromY = e.Y;
          elevationAngle += delta * 2.0 / glControl1.Height;
          elevationAngle = Arith.Clamp(elevationAngle, -1.0, 1.5);
        }
      }
    }

    private void glControl1_MouseWheel (object sender, MouseEventArgs e)
    {
      if (e.Delta != 0)
        if (hovercraft)
        {
          // !!! TODO: hovercraft
        }
        else
        {
          float change = e.Delta / 120.0f;
          zoom = Arith.Clamp(zoom * Math.Pow(1.05, change), 0.5, 100.0);
        }
    }
  }
}
