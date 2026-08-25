using FrameWork.Tools;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EEGTool.Views.YelloSpot
{
    /// <summary>
    /// YelloSpotCaptureView.xaml 的交互逻辑
    /// </summary>
    public partial class YelloSpotCaptureView : UserControl
    {
        //1.顶点数据:矩形屏幕,顶点数据/索引
        private float[] vertices = new float[]
        {
            0,0,0,
            0,1,0,
            1,1,0,
            1,0,0,
        };
        private int[] indices = new int[]
        {
            0,1,2,
            0,2,3,
        };

        private int _vao, _vbo, _ebo, _shaderProgram; //VAO,VBO,EBO,Shader
        private int _modelLoc, _viewLoc, _projLoc;    //M-V-P 模型-观察-投影矩阵的句柄
        private int _indexCount;                      //索引的数量
        private bool _glResourcesInitialized = false; //资源是否进行了初始化


        public YelloSpotCaptureView()
        {
            InitializeComponent();
        }

        //Step 2: 初始化绘制所需要的资源
        private void OpenTkControl_Init()
        {
            InitGLResources();
        }

        private void InitGLResources()
        {
            _indexCount = indices.Length;

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();

            GL.BindVertexArray(_vao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(int), indices, BufferUsageHint.StaticDraw);


            int stride = 3 * sizeof(float);

            // 位置
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(0);

            string vertexShaderSource = ShaderTool.LoadShaderSource("screen.vert");
            string fragmentShaderSource = ShaderTool.LoadShaderSource("screen.frag");

            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexShaderSource);
            GL.CompileShader(vertexShader);
            var isVertOK = ShaderTool.CheckShaderCompile(vertexShader);

            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentShaderSource);
            GL.CompileShader(fragmentShader);
            var isFragOK = ShaderTool.CheckShaderCompile(fragmentShader);

            _shaderProgram = GL.CreateProgram();
            GL.AttachShader(_shaderProgram, vertexShader);
            GL.AttachShader(_shaderProgram, fragmentShader);
            GL.LinkProgram(_shaderProgram);

            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            _modelLoc = GL.GetUniformLocation(_shaderProgram, "model");
            _viewLoc = GL.GetUniformLocation(_shaderProgram, "view");
            _projLoc = GL.GetUniformLocation(_shaderProgram, "projection");

            _glResourcesInitialized = true;

        }

        //Step 3:渲染帧
        private void OpenTkControl_OnRender(TimeSpan obj)
        {
            if (!_glResourcesInitialized)
            {
                InitGLResources(); // 初始化一次
            }

            GL.Viewport(0, 0, (int)ActualWidth, (int)ActualHeight);
            GL.ClearColor(1f, 1f, 1f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);

            GL.UseProgram(_shaderProgram);
            GL.BindVertexArray(_vao);

            Matrix4 model = Matrix4.Identity * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(0));
            Vector3 camPos = new Vector3(0, 0, 0.5f);
            Matrix4 view = Matrix4.LookAt(camPos, Vector3.Zero, Vector3.UnitY);
            Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, (float)ActualWidth / (float)ActualHeight, 0.1f, 100.0f);

            GL.UniformMatrix4(_modelLoc, false, ref model);
            GL.UniformMatrix4(_viewLoc, false, ref view);
            GL.UniformMatrix4(_projLoc, false, ref projection);

            GL.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, 0);
        }
    }
}
