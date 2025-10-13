namespace Nuotti.AudioEngine.Playback.Decoding;

public interface IAudioDecoder
{
    // Open the specified file and prepare for sequential decoding.
    void Open(string filePath);

    // PCM format of the decoded stream.
    int SampleRate { get; }
    int Channels { get; }

    // Read up to framesToRead frames into the provided buffer (interleaved float PCM, length must be >= framesToRead * Channels).
    // Returns number of frames actually read; 0 means end of stream.
    int Read(float[] buffer, int framesToRead);

    void Close();
}
