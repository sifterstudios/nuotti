using System.Buffers.Binary;
using System.Text;
namespace Nuotti.AudioEngine.Playback.Decoding;

// Minimal WAV PCM16 decoder sufficient for tests/integration.
public sealed class WavPcm16Decoder : IAudioDecoder
{
    private FileStream? _fs;
    private BinaryReader? _br;
    private int _dataBytesRemaining;

    public int SampleRate { get; private set; }
    public int Channels { get; private set; }

    public void Open(string filePath)
    {
        Close();
        _fs = File.OpenRead(filePath);
        _br = new BinaryReader(_fs);
        ParseHeader();
    }

    public void Close()
    {
        try { _br?.Dispose(); } catch { }
        try { _fs?.Dispose(); } catch { }
        _br = null;
        _fs = null;
        _dataBytesRemaining = 0;
        SampleRate = 0;
        Channels = 0;
    }

    public int Read(float[] buffer, int framesToRead)
    {
        if (_br is null) throw new InvalidOperationException("Decoder not opened");
        if (Channels <= 0) return 0;
        int samplesToRead = Math.Min(framesToRead, int.MaxValue / Channels) * Channels;
        int bytesToRead = samplesToRead * 2; // 16-bit
        if (_dataBytesRemaining <= 0) return 0;
        if (bytesToRead > _dataBytesRemaining) bytesToRead = _dataBytesRemaining - (_dataBytesRemaining % (Channels * 2));
        if (bytesToRead <= 0) return 0;

        Span<byte> tmp = stackalloc byte[Math.Min(bytesToRead, 4096)];
        int bytesRemaining = bytesToRead;
        int bufIndex = 0;
        while (bytesRemaining > 0)
        {
            int chunk = Math.Min(bytesRemaining, tmp.Length);
            int read = _fs!.Read(tmp.Slice(0, chunk));
            if (read <= 0) break;
            for (int i = 0; i < read; i += 2)
            {
                short s = BinaryPrimitives.ReadInt16LittleEndian(tmp.Slice(i, 2));
                buffer[bufIndex++] = s / 32768f;
            }
            bytesRemaining -= read;
            _dataBytesRemaining -= read;
        }
        int samplesRead = bufIndex;
        int framesRead = samplesRead / Channels;
        return framesRead;
    }

    private void ParseHeader()
    {
        if (_br is null) throw new InvalidOperationException();
        var br = _br;
        // RIFF header
        var riff = br.ReadBytes(4);
        if (riff.Length < 4 || riff[0] != 'R' || riff[1] != 'I' || riff[2] != 'F' || riff[3] != 'F')
            throw new InvalidDataException("Not a RIFF file");
        br.ReadInt32(); // chunk size
        var wave = br.ReadBytes(4);
        if (wave.Length < 4 || wave[0] != 'W' || wave[1] != 'A' || wave[2] != 'V' || wave[3] != 'E')
            throw new InvalidDataException("Not a WAVE file");

        bool fmtFound = false;
        bool dataFound = false;
        int bitsPerSample = 0;
        while (_fs!.Position < _fs.Length)
        {
            var id = br.ReadBytes(4);
            int size = br.ReadInt32();
            if (id.Length < 4) throw new InvalidDataException("Unexpected EOF");
            string chunkId = Encoding.ASCII.GetString(id);
            if (chunkId == "fmt ")
            {
                int audioFormat = br.ReadInt16();
                Channels = br.ReadInt16();
                SampleRate = br.ReadInt32();
                int byteRate = br.ReadInt32();
                int blockAlign = br.ReadInt16();
                bitsPerSample = br.ReadInt16();
                int remaining = size - 16;
                if (remaining > 0) br.ReadBytes(remaining);
                if (audioFormat != 1 || bitsPerSample != 16)
                    throw new InvalidDataException("Only PCM16 WAV supported in this minimal decoder");
                fmtFound = true;
            }
            else if (chunkId == "data")
            {
                _dataBytesRemaining = size;
                dataFound = true;
                break;
            }
            else
            {
                // skip other chunks
                if (size > 0)
                {
                    br.ReadBytes(size);
                }
            }
        }
        if (!fmtFound || !dataFound)
            throw new InvalidDataException("Invalid WAV: missing fmt or data chunk");
    }
}
