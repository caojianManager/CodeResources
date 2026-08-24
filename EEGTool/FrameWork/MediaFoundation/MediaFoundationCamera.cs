using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MediaFoundation;
using MediaFoundation.Misc;
using MediaFoundation.ReadWrite;

namespace EEGTool.FrameWork.MediaFoundation
{


public sealed class MediaFoundationCamera : IDisposable
{
    private IMFSourceReader? _reader;
    private IMFMediaSource? _mediaSource;
    private IMFActivate[]? _devices;

    private CancellationTokenSource? _cts;
    private Task? _captureTask;
    private Thread? _captureThread;

    private bool _mfStarted;
    private bool _disposed;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public Guid CurrentSubtype { get; private set; }

    public bool IsRunning =>
        (_captureThread != null && _captureThread.IsAlive) ||
        (_captureTask != null && !_captureTask.IsCompleted);

    /// <summary>
    /// 采集到一帧图像时触发。
    /// 数据格式为 RGB32/BGRA，每像素 4 字节。
    /// </summary>
    public event Action<CameraFrame>? FrameArrived;

    public event Action<Exception>? CaptureFailed;

    public void StartCapture(int cameraIndex = 0, int width = 1280, int height = 720)
    {
        ThrowIfDisposed();

        if (IsRunning)
            return;

        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;

        _captureThread = new Thread(() => CaptureThreadLoop(cameraIndex, width, height, token))
        {
            IsBackground = true,
            Name = "MediaFoundationCameraCapture"
        };

        _captureThread.SetApartmentState(ApartmentState.MTA);
        _captureThread.Start();
    }

    public void Open(int cameraIndex = 0, int width = 1280, int height = 720)
    {
        ThrowIfDisposed();

        if (_reader != null)
            throw new InvalidOperationException("Camera already opened.");

        StartMediaFoundation();

        _devices = EnumerateVideoDevices();

        if (_devices.Length == 0)
            throw new InvalidOperationException("No camera device found.");

        if (cameraIndex < 0 || cameraIndex >= _devices.Length)
            throw new ArgumentOutOfRangeException(nameof(cameraIndex), "Invalid camera index.");

        HResult hr = _devices[cameraIndex].ActivateObject(
            typeof(IMFMediaSource).GUID,
            out object sourceObject);

        MFError.ThrowExceptionForHR(hr);

        _mediaSource = (IMFMediaSource)sourceObject;

        IMFAttributes? readerAttributes = null;

        try
        {
            hr = MFExtern.MFCreateAttributes(out readerAttributes, 4);
            MFError.ThrowExceptionForHR(hr);

            hr = readerAttributes.SetUINT32(
                MFAttributesClsid.MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING,
                1);
            MFError.ThrowExceptionForHR(hr);

            hr = readerAttributes.SetUINT32(
                MFAttributesClsid.MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS,
                1);
            MFError.ThrowExceptionForHR(hr);

            hr = readerAttributes.SetUINT32(
                MFAttributesClsid.MF_SOURCE_READER_ENABLE_TRANSCODE_ONLY_TRANSFORMS,
                1);
            MFError.ThrowExceptionForHR(hr);

            hr = readerAttributes.SetUINT32(
                MFAttributesClsid.MF_SOURCE_READER_DISCONNECT_MEDIASOURCE_ON_SHUTDOWN,
                1);
            MFError.ThrowExceptionForHR(hr);

            hr = MFExtern.MFCreateSourceReaderFromMediaSource(
                _mediaSource,
                readerAttributes,
                out _reader);

            MFError.ThrowExceptionForHR(hr);
        }
        finally
        {
            if (readerAttributes != null)
                Marshal.ReleaseComObject(readerAttributes);
        }

        ConfigureVideoFormat(width, height);
    }

    public void Start()
    {
        ThrowIfDisposed();

        if (_reader == null)
            throw new InvalidOperationException("Camera is not opened.");

        if (IsRunning)
            return;

        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;

        _captureTask = Task.Run(() => CaptureLoop(token), token);
    }

    public void Stop()
    {
        if (_cts == null)
            return;

        _cts.Cancel();

        try
        {
            if (_captureThread != null && _captureThread != Thread.CurrentThread)
            {
                _captureThread.Join(1000);
            }

            _captureTask?.Wait(1000);
        }
        catch
        {
            // ignored
        }

        _captureThread = null;
        _captureTask = null;

        _cts.Dispose();
        _cts = null;
    }

    public void Close()
    {
        Stop();
        ReleaseResources();
    }

    private void CaptureThreadLoop(int cameraIndex, int width, int height, CancellationToken token)
    {
        try
        {
            Open(cameraIndex, width, height);
            CaptureLoop(token);
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            CaptureFailed?.Invoke(ex);
        }
        finally
        {
            ReleaseResources();
            ShutdownMediaFoundation();
        }
    }

    private void CaptureLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                CameraFrame? frame = ReadFrame();

                if (frame != null)
                {
                    FrameArrived?.Invoke(frame);
                }
            }
            catch (COMException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                CaptureFailed?.Invoke(ex);
                break;
            }
        }
    }

    private CameraFrame? ReadFrame()
    {
        if (_reader == null)
            return null;

        int actualStreamIndex;
        MF_SOURCE_READER_FLAG flags;
        long timestamp;
        IMFSample? sample;

        HResult hr = _reader.ReadSample(
            (int)MF_SOURCE_READER.FirstVideoStream,
            0,
            out actualStreamIndex,
            out flags,
            out timestamp,
            out sample);

        MFError.ThrowExceptionForHR(hr);

        if ((flags & MF_SOURCE_READER_FLAG.EndOfStream) != 0)
            return null;

        if ((flags & MF_SOURCE_READER_FLAG.StreamTick) != 0)
            return null;

        if (sample == null)
            return null;

        IMFMediaBuffer? buffer = null;

        try
        {
            hr = sample.GetTotalLength(out int totalLength);
            MFError.ThrowExceptionForHR(hr);

            int expectedLength = Width * Height * 4;
            int bufferLength = totalLength > 0 ? totalLength : expectedLength;

            hr = MFExtern.MFCreateMemoryBuffer(bufferLength, out buffer);
            MFError.ThrowExceptionForHR(hr);

            hr = sample.CopyToBuffer(buffer);
            MFError.ThrowExceptionForHR(hr);

            IntPtr dataPtr;
            int maxLength;
            int currentLength;

            hr = buffer.Lock(out dataPtr, out maxLength, out currentLength);
            MFError.ThrowExceptionForHR(hr);

            try
            {
                int copyLength = expectedLength > 0
                    ? Math.Min(currentLength, expectedLength)
                    : currentLength;

                byte[] data = expectedLength > 0
                    ? new byte[expectedLength]
                    : new byte[copyLength];

                Marshal.Copy(dataPtr, data, 0, copyLength);

                return new CameraFrame(data, Width, Height, timestamp);
            }
            finally
            {
                buffer.Unlock();
            }
        }
        finally
        {
            if (buffer != null)
                Marshal.ReleaseComObject(buffer);

            Marshal.ReleaseComObject(sample);
        }
    }

    private void ReleaseResources()
    {
        if (_reader != null)
        {
            Marshal.ReleaseComObject(_reader);
            _reader = null;
        }

        if (_mediaSource != null)
        {
            _mediaSource.Shutdown();
            Marshal.ReleaseComObject(_mediaSource);
            _mediaSource = null;
        }

        if (_devices != null)
        {
            foreach (IMFActivate device in _devices)
            {
                Marshal.ReleaseComObject(device);
            }

            _devices = null;
        }
    }

    private void ConfigureVideoFormat(int width, int height)
    {
        if (_reader == null)
            throw new InvalidOperationException("SourceReader is null.");

        HResult hr = _reader.SetStreamSelection(
            (int)MF_SOURCE_READER.AllStreams,
            false);

        MFError.ThrowExceptionForHR(hr);

        hr = _reader.SetStreamSelection(
            (int)MF_SOURCE_READER.FirstVideoStream,
            true);

        MFError.ThrowExceptionForHR(hr);

        foreach (VideoFormat nativeFormat in GetNativeVideoFormats(width, height))
        {
            Debug.WriteLine(
                $"[MediaFoundationCamera] Try {nativeFormat.Width}x{nativeFormat.Height} {GetSubtypeName(nativeFormat.Subtype)} {nativeFormat.FrameRateNumerator}/{nativeFormat.FrameRateDenominator}");

            if (TrySetVideoFormat(nativeFormat, out int actualWidth, out int actualHeight, out Guid subtype))
            {
                Width = actualWidth;
                Height = actualHeight;
                CurrentSubtype = subtype;

                Debug.WriteLine(
                    $"[MediaFoundationCamera] Selected {Width}x{Height} {GetSubtypeName(CurrentSubtype)}");

                return;
            }

            Debug.WriteLine(
                $"[MediaFoundationCamera] Rejected {nativeFormat.Width}x{nativeFormat.Height} {GetSubtypeName(nativeFormat.Subtype)}");
        }

        throw new InvalidOperationException(
            $"Camera does not support RGB32 output near {width}x{height}.");
    }

    private bool TrySetVideoFormat(int width, int height, out int actualWidth, out int actualHeight, out Guid subtype)
    {
        return TrySetVideoFormat(
            new VideoFormat(width, height, Guid.Empty, 0, 0, -1),
            out actualWidth,
            out actualHeight,
            out subtype);
    }

    private bool TrySetVideoFormat(VideoFormat format, out int actualWidth, out int actualHeight, out Guid subtype)
    {
        if (_reader == null)
            throw new InvalidOperationException("SourceReader is null.");

        actualWidth = format.Width;
        actualHeight = format.Height;
        subtype = MFMediaType.RGB32;

        IMFMediaType? mediaType = null;
        IMFMediaType? nativeType = null;

        try
        {
            HResult hr = MFExtern.MFCreateMediaType(out mediaType);
            MFError.ThrowExceptionForHR(hr);

            if (format.NativeTypeIndex >= 0)
            {
                hr = _reader.GetNativeMediaType(
                    (int)MF_SOURCE_READER.FirstVideoStream,
                    format.NativeTypeIndex,
                    out nativeType);

                MFError.ThrowExceptionForHR(hr);

                hr = nativeType.CopyAllItems(mediaType);
                MFError.ThrowExceptionForHR(hr);
            }

            hr = mediaType.SetGUID(
                MFAttributesClsid.MF_MT_MAJOR_TYPE,
                MFMediaType.Video);

            MFError.ThrowExceptionForHR(hr);

            // RGB32 在内存里通常就是 BGRA，适合 OpenGL 用 GL_BGRA 上传
            hr = mediaType.SetGUID(
                MFAttributesClsid.MF_MT_SUBTYPE,
                MFMediaType.RGB32);

            MFError.ThrowExceptionForHR(hr);

            hr = MFExtern.MFSetAttributeSize(
                mediaType,
                MFAttributesClsid.MF_MT_FRAME_SIZE,
                format.Width,
                format.Height);

            MFError.ThrowExceptionForHR(hr);

            if (format.FrameRateNumerator > 0 && format.FrameRateDenominator > 0)
            {
                hr = MFExtern.MFSetAttributeRatio(
                    mediaType,
                    MFAttributesClsid.MF_MT_FRAME_RATE,
                    format.FrameRateNumerator,
                    format.FrameRateDenominator);

                MFError.ThrowExceptionForHR(hr);
            }

            hr = _reader.SetCurrentMediaType(
                (int)MF_SOURCE_READER.FirstVideoStream,
                null,
                mediaType);

            if (MFError.Failed(hr))
                return false;

            IMFMediaType? currentMediaType = null;

            try
            {
                hr = _reader.GetCurrentMediaType(
                    (int)MF_SOURCE_READER.FirstVideoStream,
                    out currentMediaType);

                if (MFError.Succeeded(hr) && currentMediaType != null)
                {
                    MFExtern.MFGetAttributeSize(
                        currentMediaType,
                        MFAttributesClsid.MF_MT_FRAME_SIZE,
                        out actualWidth,
                        out actualHeight);

                    currentMediaType.GetGUID(
                        MFAttributesClsid.MF_MT_SUBTYPE,
                        out subtype);
                }
            }
            finally
            {
                if (currentMediaType != null)
                    Marshal.ReleaseComObject(currentMediaType);
            }

            return actualWidth == format.Width && actualHeight == format.Height;
        }
        finally
        {
            if (nativeType != null)
                Marshal.ReleaseComObject(nativeType);

            if (mediaType != null)
                Marshal.ReleaseComObject(mediaType);
        }
    }

    private List<VideoFormat> GetNativeVideoFormats(int preferredWidth, int preferredHeight)
    {
        if (_reader == null)
            throw new InvalidOperationException("SourceReader is null.");

        var formats = new List<VideoFormat>();

        for (int index = 0; ; index++)
        {
            IMFMediaType? nativeType = null;

            try
            {
                HResult hr = _reader.GetNativeMediaType(
                    (int)MF_SOURCE_READER.FirstVideoStream,
                    index,
                    out nativeType);

                if (hr == HResult.MF_E_NO_MORE_TYPES)
                    break;

                if (MFError.Failed(hr) || nativeType == null)
                    continue;

                int nativeWidth = 0;
                int nativeHeight = 0;
                MFExtern.MFGetAttributeSize(
                    nativeType,
                    MFAttributesClsid.MF_MT_FRAME_SIZE,
                    out nativeWidth,
                    out nativeHeight);

                if (nativeWidth <= 0 || nativeHeight <= 0)
                    continue;

                nativeType.GetGUID(
                    MFAttributesClsid.MF_MT_SUBTYPE,
                    out Guid nativeSubtype);

                int frameRateNumerator = 0;
                int frameRateDenominator = 0;

                MFExtern.MFGetAttributeRatio(
                    nativeType,
                    MFAttributesClsid.MF_MT_FRAME_RATE,
                    out frameRateNumerator,
                    out frameRateDenominator);

                formats.Add(new VideoFormat(
                    nativeWidth,
                    nativeHeight,
                    nativeSubtype,
                    frameRateNumerator,
                    frameRateDenominator,
                    index));
            }
            finally
            {
                if (nativeType != null)
                    Marshal.ReleaseComObject(nativeType);
            }
        }

        if (formats.Count == 0)
            throw new InvalidOperationException("No video format found for camera.");

        formats.Sort((left, right) =>
            GetFormatScore(left, preferredWidth, preferredHeight)
                .CompareTo(GetFormatScore(right, preferredWidth, preferredHeight)));

        return formats;
    }

    private static long GetFormatScore(VideoFormat format, int preferredWidth, int preferredHeight)
    {
        long widthDelta = format.Width - preferredWidth;
        long heightDelta = format.Height - preferredHeight;
        return widthDelta * widthDelta + heightDelta * heightDelta;
    }

    private static string GetSubtypeName(Guid subtype)
    {
        if (subtype == MFMediaType.RGB32)
            return "RGB32";

        if (subtype == MFMediaType.MJPG)
            return "MJPG";

        if (subtype == MFMediaType.NV12)
            return "NV12";

        if (subtype == MFMediaType.YUY2)
            return "YUY2";

        if (subtype == MFMediaType.UYVY)
            return "UYVY";

        return subtype.ToString();
    }

    private IMFActivate[] EnumerateVideoDevices()
    {
        IMFAttributes? attributes = null;

        try
        {
            HResult hr = MFExtern.MFCreateAttributes(out attributes, 1);
            MFError.ThrowExceptionForHR(hr);

            hr = attributes.SetGUID(
                MFAttributesClsid.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE,
                CLSID.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID);

            MFError.ThrowExceptionForHR(hr);

            hr = MFExtern.MFEnumDeviceSources(
                attributes,
                out IMFActivate[] devices,
                out int count);

            MFError.ThrowExceptionForHR(hr);

            return devices;
        }
        finally
        {
            if (attributes != null)
                Marshal.ReleaseComObject(attributes);
        }
    }

    private void StartMediaFoundation()
    {
        if (_mfStarted)
            return;

        HResult hr = MFExtern.MFStartup(0x00020070, MFStartup.Full);
        MFError.ThrowExceptionForHR(hr);

        _mfStarted = true;
    }

    private void ShutdownMediaFoundation()
    {
        if (!_mfStarted)
            return;

        MFExtern.MFShutdown();
        _mfStarted = false;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MediaFoundationCamera));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Close();
        ShutdownMediaFoundation();

        _disposed = true;
    }

    private readonly struct VideoFormat
    {
        public VideoFormat(
            int width,
            int height,
            Guid subtype,
            int frameRateNumerator,
            int frameRateDenominator,
            int nativeTypeIndex)
        {
            Width = width;
            Height = height;
            Subtype = subtype;
            FrameRateNumerator = frameRateNumerator;
            FrameRateDenominator = frameRateDenominator;
            NativeTypeIndex = nativeTypeIndex;
        }

        public int Width { get; }

        public int Height { get; }

        public Guid Subtype { get; }

        public int FrameRateNumerator { get; }

        public int FrameRateDenominator { get; }

        public int NativeTypeIndex { get; }
    }
}
}
