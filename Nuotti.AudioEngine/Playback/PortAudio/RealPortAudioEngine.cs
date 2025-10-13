using System.Reflection;
using System.Runtime.Loader;
namespace Nuotti.AudioEngine.Playback.PortAudio;

/// <summary>
/// Best-effort PortAudioSharp2-backed engine. Uses reflection to avoid a hard compile-time
/// dependency on specific APIs and to gracefully fall back when native deps are missing.
/// If initialization fails, delegates to SimulatedPortAudioEngine to preserve behavior.
/// </summary>
public sealed class RealPortAudioEngine : IPortAudioEngine
{
    private readonly SimulatedPortAudioEngine _fallback = new();

    public double ReportedLatencyMs => 0d;

    // Reflection-captured members
    private Assembly? _paAssembly;
    private Type? _paType; // e.g. PortAudioSharp.PortAudio
    private object? _stream; // native/managed stream handle

    private MethodInfo? _initialize;
    private MethodInfo? _terminate;
    private MethodInfo? _openDefaultStream;
    private MethodInfo? _startStream;
    private MethodInfo? _stopStream;
    private MethodInfo? _closeStream;
    private MethodInfo? _writeStream;

    private int _sampleRate;
    private int _channels;
    private bool _started;

    public void Open(int sampleRate, int channels)
    {
        _sampleRate = sampleRate > 0 ? sampleRate : 48000;
        _channels = Math.Max(1, channels);

        try
        {
            // Try to load the PortAudioSharp assembly that the NuGet package provides
            _paAssembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => a.GetName().Name == "PortAudioSharp")
                           ?? AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName("PortAudioSharp"));

            _paType = _paAssembly.GetType("PortAudioSharp.PortAudio", throwOnError: false)
                      ?? _paAssembly.GetTypes().FirstOrDefault(t => t.Name.Contains("PortAudio"));
            if (_paType is null)
                throw new InvalidOperationException("PortAudioSharp type not found");

            // Common P/Invoke style wrappers often expose static methods like Pa_Initialize, Pa_Terminate, etc.
            _initialize = _paType.GetMethod("Pa_Initialize", BindingFlags.Public | BindingFlags.Static)
                          ?? _paType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
            _terminate = _paType.GetMethod("Pa_Terminate", BindingFlags.Public | BindingFlags.Static)
                          ?? _paType.GetMethod("Terminate", BindingFlags.Public | BindingFlags.Static);
            _openDefaultStream = _paType.GetMethod("Pa_OpenDefaultStream", BindingFlags.Public | BindingFlags.Static)
                               ?? _paType.GetMethod("OpenDefaultStream", BindingFlags.Public | BindingFlags.Static);
            _startStream = _paType.GetMethod("Pa_StartStream", BindingFlags.Public | BindingFlags.Static)
                          ?? _paType.GetMethod("StartStream", BindingFlags.Public | BindingFlags.Static);
            _stopStream = _paType.GetMethod("Pa_StopStream", BindingFlags.Public | BindingFlags.Static)
                         ?? _paType.GetMethod("StopStream", BindingFlags.Public | BindingFlags.Static);
            _closeStream = _paType.GetMethod("Pa_CloseStream", BindingFlags.Public | BindingFlags.Static)
                          ?? _paType.GetMethod("CloseStream", BindingFlags.Public | BindingFlags.Static);
            _writeStream = _paType.GetMethod("Pa_WriteStream", BindingFlags.Public | BindingFlags.Static)
                          ?? _paType.GetMethod("WriteStream", BindingFlags.Public | BindingFlags.Static);

            if (_initialize is null || _openDefaultStream is null || _startStream is null || _stopStream is null || _closeStream is null || _writeStream is null)
                throw new InvalidOperationException("PortAudioSharp essential methods not found");

            // Initialize PortAudio
            _initialize.Invoke(null, Array.Empty<object?>());

            // Many wrappers use: Pa_OpenDefaultStream(out IntPtr stream, inputChannels, outputChannels, sampleFormat, sampleRate, framesPerBuffer, callback, userData)
            // We will attempt to open a blocking output stream with float32 samples.
            // Sample format constant varies; try to find a field/property named paFloat32 or Float32.
            object? fmt = null;
            var fmtField = _paType.GetField("paFloat32") ?? _paType.GetField("Float32") ?? _paType.GetField("PaFloat32");
            if (fmtField != null) fmt = fmtField.GetValue(null);
            var fmtProp = fmt is null ? _paType.GetProperty("paFloat32") ?? _paType.GetProperty("Float32") : null;
            if (fmt is null && fmtProp is not null) fmt = fmtProp.GetValue(null);
            // Fallback to integer 1 (commonly paFloat32 == 1) if not found; harmless if wrapper maps enums differently
            fmt ??= 1;

            // We don't know exact signature, try common signature lengths.
            object?[]? args = null;
            // Try: (out IntPtr stream, int inCh, int outCh, int sampleFormat, double sampleRate, uint framesPerBuffer, IntPtr callback, IntPtr userData)
            try
            {
                var streamBox = new object?[] { IntPtr.Zero, 0, _channels, fmt, (double)_sampleRate, (uint)0, IntPtr.Zero, IntPtr.Zero };
                _openDefaultStream.Invoke(null, streamBox);
                _stream = (IntPtr)streamBox[0]!;
            }
            catch
            {
                // Try another variant with ref object or out object
                try
                {
                    object? streamObj = null;
                    args = new object?[] { streamObj, 0, _channels, fmt, (double)_sampleRate, (uint)0, null, null };
                    _openDefaultStream.Invoke(null, args);
                    _stream = args[0];
                }
                catch
                {
                    // Give up on real engine; fall back
                    _stream = null;
                }
            }

            if (_stream is null)
                throw new InvalidOperationException("Failed to open PortAudio stream");
        }
        catch
        {
            // Fall back to simulation
            _fallback.Open(_sampleRate, _channels);
        }
    }

    public void Start()
    {
        try
        {
            if (_stream is not null)
            {
                _startStream!.Invoke(null, new[] { _stream });
                _started = true;
            }
            else
            {
                _fallback.Start();
            }
        }
        catch
        {
            _fallback.Start();
        }
    }

    public async Task WriteAsync(float[] buffer, int frames, int channels, int sampleRate, CancellationToken cancellationToken)
    {
        if (!_started)
        {
            return;
        }

        if (_stream is null || _writeStream is null)
        {
            await _fallback.WriteAsync(buffer, frames, channels, sampleRate, cancellationToken);
            return;
        }

        try
        {
            // Many wrappers want a pointer; some accept arrays. We'll try array first.
            var args = new object?[] { _stream, buffer, (ulong)frames };
            _writeStream.Invoke(null, args);
        }
        catch
        {
            // As a fallback, simulate timing so we keep real-time behavior
            await _fallback.WriteAsync(buffer, frames, channels, sampleRate, cancellationToken);
        }
    }

    public void Stop()
    {
        try
        {
            if (_stream is not null && _stopStream is not null)
            {
                _stopStream.Invoke(null, new[] { _stream });
            }
            else
            {
                _fallback.Stop();
            }
        }
        catch
        {
            _fallback.Stop();
        }
    }

    public void Close()
    {
        try
        {
            if (_stream is not null)
            {
                _closeStream?.Invoke(null, new[] { _stream });
                _stream = null;
            }
            _terminate?.Invoke(null, Array.Empty<object?>());
        }
        catch
        {
            // ignore
        }
        finally
        {
            _fallback.Close();
            _started = false;
        }
    }
}