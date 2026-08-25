using EEGTool.FrameWork.MediaFoundation;
using FrameWork.Tools;
using OpenTK.Graphics.OpenGL;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace EEGTool.Views.YelloSpot
{
    /// <summary>
    /// YelloSpotCaptureView.xaml 的交互逻辑
    /// </summary>
    public partial class YelloSpotCaptureView : UserControl
    {

        private bool _isShowDetailPanel = true;
        private bool _isAnimationing;
        private Storyboard? _currentAnimation;

        public static readonly DependencyProperty CameraFrameProperty =
            DependencyProperty.Register(
                nameof(CameraFrame),
                typeof(CameraFrame),
                typeof(YelloSpotCaptureView),
                new PropertyMetadata(null, OnCameraFrameChanged));

        public static readonly DependencyProperty IsMagnifierEnabledProperty =
            DependencyProperty.Register(
                nameof(IsMagnifierEnabled),
                typeof(bool),
                typeof(YelloSpotCaptureView),
                new PropertyMetadata(true));

        public static readonly DependencyProperty MagnifierRadiusProperty =
            DependencyProperty.Register(
                nameof(MagnifierRadius),
                typeof(double),
                typeof(YelloSpotCaptureView),
                new PropertyMetadata(28.0));

        public static readonly DependencyProperty CameraZoomProperty =
            DependencyProperty.Register(
                nameof(CameraZoom),
                typeof(double),
                typeof(YelloSpotCaptureView),
                new PropertyMetadata(1.0));

        private readonly float[] vertices = new float[]
        {
            -1f, -1f, 0f, 1f,
            -1f,  1f, 0f, 0f,
             1f,  1f, 1f, 0f,
             1f, -1f, 1f, 1f,
        };

        private readonly int[] indices = new int[]
        {
            0, 1, 2,
            0, 2, 3,
        };

        private readonly object _frameLock = new object();
        private int _vao, _vbo, _ebo, _shaderProgram, _textureId;
        private int _textureLoc;
        private int _scaleLoc;
        private int _magnifierCenterUvLoc;
        private int _magnifierCenterLocalLoc;
        private int _magnifierRadiusLoc;
        private int _magnificationLoc;
        private int _magnifierEnabledLoc;
        private int _viewportAspectLoc;
        private int _indexCount;
        private bool _glResourcesInitialized;
        private byte[]? _pendingFrameData;
        private int _pendingFrameWidth;
        private int _pendingFrameHeight;
        private bool _hasPendingFrame;
        private int _textureWidth;
        private int _textureHeight;
        private bool _hasTextureFrame;
        private float _scaleX = 1f;
        private float _scaleY = 1f;
        private float _magnifierCenterUvX = 0.5f;
        private float _magnifierCenterUvY = 0.5f;
        private float _magnifierCenterLocalX;
        private float _magnifierCenterLocalY;
        private bool _isMagnifierActive;

        public YelloSpotCaptureView()
        {
            InitializeComponent();
            MouseMove += YelloSpotCaptureView_MouseMove;
            MouseLeave += YelloSpotCaptureView_MouseLeave;
            PreviewMouseWheel += YelloSpotCaptureView_PreviewMouseWheel;
        }

        public CameraFrame? CameraFrame
        {
            get => (CameraFrame?)GetValue(CameraFrameProperty);
            set => SetValue(CameraFrameProperty, value);
        }

        public bool IsMagnifierEnabled
        {
            get => (bool)GetValue(IsMagnifierEnabledProperty);
            set => SetValue(IsMagnifierEnabledProperty, value);
        }

        public double MagnifierRadius
        {
            get => (double)GetValue(MagnifierRadiusProperty);
            set => SetValue(MagnifierRadiusProperty, value);
        }

        public double CameraZoom
        {
            get => (double)GetValue(CameraZoomProperty);
            set => SetValue(CameraZoomProperty, value);
        }

        private static void OnCameraFrameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((YelloSpotCaptureView)d).QueueFrame(e.NewValue as CameraFrame);
        }

        private void QueueFrame(CameraFrame? frame)
        {
            lock (_frameLock)
            {
                _pendingFrameData = frame?.Data;
                _pendingFrameWidth = frame?.Width ?? 0;
                _pendingFrameHeight = frame?.Height ?? 0;
                _hasPendingFrame = true;
            }
        }

        private void OpenTkControl_Init()
        {
            InitGLResources();
        }

        private void InitGLResources()
        {
            if (_glResourcesInitialized)
            {
                return;
            }

            _indexCount = indices.Length;

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();
            _textureId = GL.GenTexture();

            GL.BindVertexArray(_vao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(int), indices, BufferUsageHint.StaticDraw);

            int stride = 4 * sizeof(float);

            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            string vertexShaderSource = ShaderTool.LoadShaderSource("screen.vert");
            string fragmentShaderSource = ShaderTool.LoadShaderSource("screen.frag");

            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexShaderSource);
            GL.CompileShader(vertexShader);
            ShaderTool.CheckShaderCompile(vertexShader);

            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentShaderSource);
            GL.CompileShader(fragmentShader);
            ShaderTool.CheckShaderCompile(fragmentShader);

            _shaderProgram = GL.CreateProgram();
            GL.AttachShader(_shaderProgram, vertexShader);
            GL.AttachShader(_shaderProgram, fragmentShader);
            GL.LinkProgram(_shaderProgram);

            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            _textureLoc = GL.GetUniformLocation(_shaderProgram, "uFrameTexture");
            _scaleLoc = GL.GetUniformLocation(_shaderProgram, "uScale");
            _magnifierCenterUvLoc = GL.GetUniformLocation(_shaderProgram, "uMagnifierCenterUv");
            _magnifierCenterLocalLoc = GL.GetUniformLocation(_shaderProgram, "uMagnifierCenterLocal");
            _magnifierRadiusLoc = GL.GetUniformLocation(_shaderProgram, "uMagnifierRadius");
            _magnificationLoc = GL.GetUniformLocation(_shaderProgram, "uMagnification");
            _magnifierEnabledLoc = GL.GetUniformLocation(_shaderProgram, "uMagnifierEnabled");
            _viewportAspectLoc = GL.GetUniformLocation(_shaderProgram, "uViewportAspect");

            GL.BindTexture(TextureTarget.Texture2D, _textureId);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            _glResourcesInitialized = true;
        }

        private void OpenTkControl_OnRender(TimeSpan obj)
        {
            if (!_glResourcesInitialized)
            {
                InitGLResources();
            }

            UploadPendingFrame();

            int viewportWidth = Math.Max(1, (int)ActualWidth);
            int viewportHeight = Math.Max(1, (int)ActualHeight);

            GL.Viewport(0, 0, viewportWidth, viewportHeight);
            GL.ClearColor(0f, 0f, 0f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Disable(EnableCap.DepthTest);

            if (!_hasTextureFrame)
            {
                return;
            }

            GL.UseProgram(_shaderProgram);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _textureId);
            GL.Uniform1(_textureLoc, 0);
            SetUniformScale(viewportWidth, viewportHeight);
            SetMagnifierUniforms(viewportWidth, viewportHeight);
            GL.BindVertexArray(_vao);

            GL.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, 0);
        }

        private void SetUniformScale(int viewportWidth, int viewportHeight)
        {
            float scaleX = 1f;
            float scaleY = 1f;

            if (_textureWidth > 0 && _textureHeight > 0)
            {
                float viewportAspect = (float)viewportWidth / viewportHeight;
                float textureAspect = (float)_textureWidth / _textureHeight;

                if (viewportAspect > textureAspect)
                {
                    scaleX = textureAspect / viewportAspect;
                }
                else
                {
                    scaleY = viewportAspect / textureAspect;
                }
            }

            float zoom = (float)Math.Clamp(CameraZoom, 0.5, 3.0);
            scaleX *= zoom;
            scaleY *= zoom;

            GL.Uniform2(_scaleLoc, scaleX, scaleY);
            _scaleX = scaleX;
            _scaleY = scaleY;
            UpdateMagnifierPosition(Mouse.GetPosition(this));
        }

        private void SetMagnifierUniforms(int viewportWidth, int viewportHeight)
        {
            GL.Uniform2(_magnifierCenterUvLoc, _magnifierCenterUvX, _magnifierCenterUvY);
            GL.Uniform2(_magnifierCenterLocalLoc, _magnifierCenterLocalX, _magnifierCenterLocalY);
            GL.Uniform1(_magnifierRadiusLoc, (float)Math.Clamp(MagnifierRadius / 100.0, 0.0, 0.5));
            GL.Uniform1(_magnificationLoc, 2.2f);
            GL.Uniform1(_magnifierEnabledLoc, IsMagnifierEnabled && _isMagnifierActive ? 1 : 0);
            GL.Uniform1(_viewportAspectLoc, (float)viewportWidth / viewportHeight);
        }

        private void DetailPanel_Click(object sender, RoutedEventArgs e)
        {
            SettingPanelAnimation();
        }

        private void SettingPanelAnimation()
        {
            if (_isAnimationing)
                return;

            _isAnimationing = true;

            _currentAnimation?.Stop();

            _isShowDetailPanel = !_isShowDetailPanel;

            int fromValue = _isShowDetailPanel ? 10 : 343;
            int toValue = _isShowDetailPanel ? 343 : 10;

            var anim = new GridLengthAnimation
            {
                From = new GridLength(fromValue),
                To = new GridLength(toValue),
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            anim.Completed += (_, __) =>
            {
                _isAnimationing = false;
            };

            _currentAnimation = new Storyboard();
            Storyboard.SetTarget(anim, LeftColumn);
            Storyboard.SetTargetProperty(anim, new PropertyPath(ColumnDefinition.WidthProperty));
            _currentAnimation.Children.Add(anim);
            _currentAnimation.Begin();

            DetailPanelBtnArrow.Kind = _isShowDetailPanel
                ? MahApps.Metro.IconPacks.PackIconForkAwesomeKind.AngleDoubleLeft
                : MahApps.Metro.IconPacks.PackIconForkAwesomeKind.AngleDoubleRight;


        }

        private void UploadPendingFrame()
        {
            byte[]? data;
            int width;
            int height;

            lock (_frameLock)
            {
                if (!_hasPendingFrame)
                {
                    return;
                }

                data = _pendingFrameData;
                width = _pendingFrameWidth;
                height = _pendingFrameHeight;
                _hasPendingFrame = false;
            }

            if (data == null || width <= 0 || height <= 0)
            {
                _hasTextureFrame = false;
                _textureWidth = 0;
                _textureHeight = 0;
                return;
            }

            GL.BindTexture(TextureTarget.Texture2D, _textureId);
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);

            if (!_hasTextureFrame || width != _textureWidth || height != _textureHeight)
            {
                GL.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    PixelInternalFormat.Rgba,
                    width,
                    height,
                    0,
                    PixelFormat.Bgra,
                    PixelType.UnsignedByte,
                    data);

                _textureWidth = width;
                _textureHeight = height;
            }
            else
            {
                GL.TexSubImage2D(
                    TextureTarget.Texture2D,
                    0,
                    0,
                    0,
                    width,
                    height,
                    PixelFormat.Bgra,
                    PixelType.UnsignedByte,
                    data);
            }

            _hasTextureFrame = true;
        }

        private void YelloSpotCaptureView_MouseMove(object sender, MouseEventArgs e)
        {
            UpdateMagnifierPosition(e.GetPosition(this));
        }

        private void YelloSpotCaptureView_MouseLeave(object sender, MouseEventArgs e)
        {
            _isMagnifierActive = false;
        }

        private void YelloSpotCaptureView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point position = e.GetPosition(this);
            if (position.X <= LeftColumn.ActualWidth)
            {
                return;
            }

            double step = e.Delta > 0 ? 0.1 : -0.1;
            CameraZoom = Math.Clamp(CameraZoom + step, 0.5, 3.0);
            e.Handled = true;
        }

        private void UpdateMagnifierPosition(Point position)
        {
            double width = ActualWidth;
            double height = ActualHeight;

            if (width <= 0 || height <= 0 || _scaleX <= 0f || _scaleY <= 0f)
            {
                _isMagnifierActive = false;
                return;
            }

            float ndcX = (float)(position.X / width * 2.0 - 1.0);
            float ndcY = (float)(1.0 - position.Y / height * 2.0);

            if (Math.Abs(ndcX) > _scaleX || Math.Abs(ndcY) > _scaleY)
            {
                _isMagnifierActive = false;
                return;
            }

            _magnifierCenterLocalX = ndcX / _scaleX;
            _magnifierCenterLocalY = ndcY / _scaleY;
            _magnifierCenterUvX = (_magnifierCenterLocalX + 1f) * 0.5f;
            _magnifierCenterUvY = (1f - _magnifierCenterLocalY) * 0.5f;
            _isMagnifierActive = true;
        }
    }
}
