using System;
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

    private bool _mfStarted;
    private bool _disposed;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public bool IsRunning => _captureTask != null && !_captureTask.IsCompleted;

    /// <summary>
    /// 采集到一帧图像时触发。
    /// 数据格式为 RGB32/BGRA，每像素 4 字节。
    /// </summary>
    public event Action<CameraFrame>? FrameArrived;

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

        hr = MFExtern.MFCreateSourceReaderFromMediaSource(
            _mediaSource,
            null,
            out _reader);

        MFError.ThrowExceptionForHR(hr);

        ConfigureVideoFormat(width, height);

        Width = width;
        Height = height;
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
            _captureTask?.Wait(1000);
        }
        catch
        {
            // ignored
        }

        _captureTask = null;

        _cts.Dispose();
        _cts = null;
    }

    public void Close()
    {
        Stop();

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

    private void CaptureLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            CameraFrame? frame = ReadFrame();

            if (frame != null)
            {
                FrameArrived?.Invoke(frame);
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
            hr = sample.ConvertToContiguousBuffer(out buffer);
            MFError.ThrowExceptionForHR(hr);

            IntPtr dataPtr;
            int maxLength;
            int currentLength;

            hr = buffer.Lock(out dataPtr, out maxLength, out currentLength);
            MFError.ThrowExceptionForHR(hr);

            try
            {
                byte[] data = new byte[currentLength];
                Marshal.Copy(dataPtr, data, 0, currentLength);

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

    private void ConfigureVideoFormat(int width, int height)
    {
        if (_reader == null)
            throw new InvalidOperationException("SourceReader is null.");

        HResult hr;

        hr = _reader.SetStreamSelection(
            (int)MF_SOURCE_READER.AllStreams,
            false);

        MFError.ThrowExceptionForHR(hr);

        hr = _reader.SetStreamSelection(
            (int)MF_SOURCE_READER.FirstVideoStream,
            true);

        MFError.ThrowExceptionForHR(hr);

        IMFMediaType? mediaType = null;

        try
        {
            hr = MFExtern.MFCreateMediaType(out mediaType);
            MFError.ThrowExceptionForHR(hr);

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
                width,
                height);

            MFError.ThrowExceptionForHR(hr);

            hr = _reader.SetCurrentMediaType(
                (int)MF_SOURCE_READER.FirstVideoStream,
                null,
                mediaType);

            MFError.ThrowExceptionForHR(hr);
        }
        finally
        {
            if (mediaType != null)
                Marshal.ReleaseComObject(mediaType);
        }
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
}
}
