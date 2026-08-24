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
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

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
        }

        private void InitGLResources()
        {
            _indexCount = indices.Length;

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();

            GL.BindVertexArray(_vao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices,BufferUsage.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(int), indices, BufferUsage.StaticDraw);
        }

        //Step 3:渲染帧
        private void OpenTkControl_OnRender(TimeSpan obj)
        {

        }
    }
}
