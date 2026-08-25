using EEGTool.FrameWork.MediaFoundation;
using FrameWork.Tools;
using OpenTK.Graphics.OpenGL;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
        private Window? _ownerWindow;
        private long _lastCameraMoveTimestamp;
        private bool _isCameraKeyboardControlActive;

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

        public static readonly DependencyProperty CameraDepthRangeProperty =
            DependencyProperty.Register(
                nameof(CameraDepthRange),
                typeof(double),
                typeof(YelloSpotCaptureView),
                new PropertyMetadata(3.0, OnCameraDepthRangeChanged));

        public static readonly DependencyProperty CameraOffsetXProperty =
            DependencyProperty.Register(
                nameof(CameraOffsetX),
                typeof(double),
                typeof(YelloSpotCaptureView),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty CameraOffsetYProperty =
            DependencyProperty.Register(
                nameof(CameraOffsetY),
                typeof(double),
                typeof(YelloSpotCaptureView),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty ImageBrightnessProperty =
            DependencyProperty.Register(
                nameof(ImageBrightness),
                typeof(double),
                typeof(YelloSpotCaptureView),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty ImageContrastProperty =
            DependencyProperty.Register(
                nameof(ImageContrast),
                typeof(double),
                typeof(YelloSpotCaptureView),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty ImageSaturationProperty =
            DependencyProperty.Register(
                nameof(ImageSaturation),
                typeof(double),
                typeof(YelloSpotCaptureView),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty ImageSharpnessProperty =
            DependencyProperty.Register(
                nameof(ImageSharpness),
                typeof(double),
                typeof(YelloSpotCaptureView),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty ImageOutlineProperty =
            DependencyProperty.Register(
                nameof(ImageOutline),
                typeof(double),
                typeof(YelloSpotCaptureView),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty ImageEmbossProperty =
            DependencyProperty.Register(
                nameof(ImageEmboss),
                typeof(double),
                typeof(YelloSpotCaptureView),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty ImageMosaicProperty =
            DependencyProperty.Register(
                nameof(ImageMosaic),
                typeof(double),
                typeof(YelloSpotCaptureView),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

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
        private int _offsetLoc;
        private int _magnifierCenterUvLoc;
        private int _magnifierCenterLocalLoc;
        private int _magnifierRadiusLoc;
        private int _magnificationLoc;
        private int _magnifierEnabledLoc;
        private int _viewportAspectLoc;
        private int _brightnessLoc;
        private int _contrastLoc;
        private int _saturationLoc;
        private int _sharpnessLoc;
        private int _outlineLoc;
        private int _embossLoc;
        private int _mosaicLoc;
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
        private float _offsetX;
        private float _offsetY;
        private float _magnifierCenterUvX = 0.5f;
        private float _magnifierCenterUvY = 0.5f;
        private float _magnifierCenterLocalX;
        private float _magnifierCenterLocalY;
        private bool _isMagnifierActive;

        public YelloSpotCaptureView()
        {
            InitializeComponent();
            Focusable = true;
            MouseMove += YelloSpotCaptureView_MouseMove;
            MouseLeave += YelloSpotCaptureView_MouseLeave;
            PreviewMouseWheel += YelloSpotCaptureView_PreviewMouseWheel;
            PreviewMouseDown += YelloSpotCaptureView_PreviewMouseDown;
            Loaded += YelloSpotCaptureView_Loaded;
            Unloaded += YelloSpotCaptureView_Unloaded;
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

        public double CameraDepthRange
        {
            get => (double)GetValue(CameraDepthRangeProperty);
            set => SetValue(CameraDepthRangeProperty, value);
        }

        public double CameraOffsetX
        {
            get => (double)GetValue(CameraOffsetXProperty);
            set => SetValue(CameraOffsetXProperty, value);
        }

        public double CameraOffsetY
        {
            get => (double)GetValue(CameraOffsetYProperty);
            set => SetValue(CameraOffsetYProperty, value);
        }

        public double ImageBrightness
        {
            get => (double)GetValue(ImageBrightnessProperty);
            set => SetValue(ImageBrightnessProperty, value);
        }

        public double ImageContrast
        {
            get => (double)GetValue(ImageContrastProperty);
            set => SetValue(ImageContrastProperty, value);
        }

        public double ImageSaturation
        {
            get => (double)GetValue(ImageSaturationProperty);
            set => SetValue(ImageSaturationProperty, value);
        }

        public double ImageSharpness
        {
            get => (double)GetValue(ImageSharpnessProperty);
            set => SetValue(ImageSharpnessProperty, value);
        }

        public double ImageOutline
        {
            get => (double)GetValue(ImageOutlineProperty);
            set => SetValue(ImageOutlineProperty, value);
        }

        public double ImageEmboss
        {
            get => (double)GetValue(ImageEmbossProperty);
            set => SetValue(ImageEmbossProperty, value);
        }

        public double ImageMosaic
        {
            get => (double)GetValue(ImageMosaicProperty);
            set => SetValue(ImageMosaicProperty, value);
        }

        private static void OnCameraDepthRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((YelloSpotCaptureView)d).LimitCameraZoom();
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
            _offsetLoc = GL.GetUniformLocation(_shaderProgram, "uOffset");
            _magnifierCenterUvLoc = GL.GetUniformLocation(_shaderProgram, "uMagnifierCenterUv");
            _magnifierCenterLocalLoc = GL.GetUniformLocation(_shaderProgram, "uMagnifierCenterLocal");
            _magnifierRadiusLoc = GL.GetUniformLocation(_shaderProgram, "uMagnifierRadius");
            _magnificationLoc = GL.GetUniformLocation(_shaderProgram, "uMagnification");
            _magnifierEnabledLoc = GL.GetUniformLocation(_shaderProgram, "uMagnifierEnabled");
            _viewportAspectLoc = GL.GetUniformLocation(_shaderProgram, "uViewportAspect");
            _brightnessLoc = GL.GetUniformLocation(_shaderProgram, "uBrightness");
            _contrastLoc = GL.GetUniformLocation(_shaderProgram, "uContrast");
            _saturationLoc = GL.GetUniformLocation(_shaderProgram, "uSaturation");
            _sharpnessLoc = GL.GetUniformLocation(_shaderProgram, "uSharpness");
            _outlineLoc = GL.GetUniformLocation(_shaderProgram, "uOutline");
            _embossLoc = GL.GetUniformLocation(_shaderProgram, "uEmboss");
            _mosaicLoc = GL.GetUniformLocation(_shaderProgram, "uMosaic");

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
            UpdateCameraOffsetByKeyboard();

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
            SetImageAdjustmentUniforms();
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

            float zoom = (float)Math.Clamp(CameraZoom, 0.5, GetMaximumCameraZoom());
            scaleX *= zoom;
            scaleY *= zoom;

            _scaleX = scaleX;
            _scaleY = scaleY;
            _offsetX = (float)Math.Clamp(CameraOffsetX, -2.0, 2.0);
            _offsetY = (float)Math.Clamp(CameraOffsetY, -2.0, 2.0);
            GL.Uniform2(_scaleLoc, scaleX, scaleY);
            GL.Uniform2(_offsetLoc, _offsetX, _offsetY);
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

        private void SetImageAdjustmentUniforms()
        {
            GL.Uniform1(_brightnessLoc, (float)Math.Clamp(ImageBrightness / 100.0, -1.0, 1.0));
            GL.Uniform1(_contrastLoc, (float)Math.Clamp(ImageContrast / 100.0, 0.0, 2.0));
            GL.Uniform1(_saturationLoc, (float)Math.Clamp(ImageSaturation / 100.0, 0.0, 2.0));
            GL.Uniform1(_sharpnessLoc, (float)Math.Clamp(ImageSharpness / 20.0, 0.0, 5.0));
            GL.Uniform1(_outlineLoc, (float)Math.Clamp(ImageOutline / 100.0, 0.0, 1.0));
            GL.Uniform1(_embossLoc, (float)Math.Clamp(ImageEmboss / 100.0, 0.0, 1.0));
            GL.Uniform1(_mosaicLoc, (float)Math.Clamp(ImageMosaic / 100.0, 0.0, 1.0));
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
            if (IsPointInVideoArea(e.GetPosition(this)))
            {
                _isCameraKeyboardControlActive = true;
            }

            UpdateMagnifierPosition(e.GetPosition(this));
        }

        private void YelloSpotCaptureView_MouseLeave(object sender, MouseEventArgs e)
        {
            _isMagnifierActive = false;
            _isCameraKeyboardControlActive = false;
        }

        private void YelloSpotCaptureView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point position = e.GetPosition(this);
            if (position.X <= LeftColumn.ActualWidth)
            {
                return;
            }

            Focus();
            _isCameraKeyboardControlActive = true;
            double step = e.Delta > 0 ? 0.1 : -0.1;
            CameraZoom = Math.Clamp(CameraZoom + step, 0.5, GetMaximumCameraZoom());
            e.Handled = true;
        }

        private void YelloSpotCaptureView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (IsPointInVideoArea(e.GetPosition(this)))
            {
                Focus();
                _isCameraKeyboardControlActive = true;
                return;
            }

            _isCameraKeyboardControlActive = false;
        }

        private void YelloSpotCaptureView_Loaded(object sender, RoutedEventArgs e)
        {
            Window? window = Window.GetWindow(this);
            if (window == null || ReferenceEquals(_ownerWindow, window))
            {
                return;
            }

            _ownerWindow = window;
        }

        private void YelloSpotCaptureView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_ownerWindow == null)
            {
                return;
            }

            _ownerWindow = null;
        }

        private void UpdateCameraOffsetByKeyboard()
        {
            long now = Stopwatch.GetTimestamp();
            if (_lastCameraMoveTimestamp == 0)
            {
                _lastCameraMoveTimestamp = now;
                return;
            }

            double elapsedSeconds = (double)(now - _lastCameraMoveTimestamp) / Stopwatch.Frequency;
            _lastCameraMoveTimestamp = now;

            if (!_isCameraKeyboardControlActive || _ownerWindow?.IsActive != true)
            {
                return;
            }

            double xDirection = 0.0;
            double yDirection = 0.0;

            if (IsKeyPressed(Key.A))
            {
                xDirection -= 1.0;
            }
            if (IsKeyPressed(Key.D))
            {
                xDirection += 1.0;
            }
            if (IsKeyPressed(Key.W))
            {
                yDirection += 1.0;
            }
            if (IsKeyPressed(Key.S))
            {
                yDirection -= 1.0;
            }

            if (xDirection == 0.0 && yDirection == 0.0)
            {
                return;
            }

            double length = Math.Sqrt(xDirection * xDirection + yDirection * yDirection);
            double distance = Math.Min(elapsedSeconds, 0.05) * 0.85;
            CameraOffsetX = Math.Clamp(CameraOffsetX + xDirection / length * distance, -2.0, 2.0);
            CameraOffsetY = Math.Clamp(CameraOffsetY + yDirection / length * distance, -2.0, 2.0);
        }

        private static bool IsKeyPressed(Key key)
        {
            int virtualKey = KeyInterop.VirtualKeyFromKey(key);
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private bool IsMouseOverVideoArea()
        {
            return IsPointInVideoArea(Mouse.GetPosition(this));
        }

        private bool IsPointInVideoArea(Point position)
        {
            return position.X > LeftColumn.ActualWidth
                   && position.X <= ActualWidth
                   && position.Y >= 0
                   && position.Y <= ActualHeight;
        }

        private double GetMaximumCameraZoom()
        {
            return Math.Clamp(CameraDepthRange, 1.0, 5.0);
        }

        private void LimitCameraZoom()
        {
            CameraZoom = Math.Clamp(CameraZoom, 0.5, GetMaximumCameraZoom());
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

            float localX = (ndcX - _offsetX) / _scaleX;
            float localY = (ndcY - _offsetY) / _scaleY;

            if (Math.Abs(localX) > 1f || Math.Abs(localY) > 1f)
            {
                _isMagnifierActive = false;
                return;
            }

            _magnifierCenterLocalX = localX;
            _magnifierCenterLocalY = localY;
            _magnifierCenterUvX = (_magnifierCenterLocalX + 1f) * 0.5f;
            _magnifierCenterUvY = (1f - _magnifierCenterLocalY) * 0.5f;
            _isMagnifierActive = true;
        }
    }
}
