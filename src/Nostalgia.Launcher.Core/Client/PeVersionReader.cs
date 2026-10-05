using System.Buffers.Binary;

namespace Nostalgia.Launcher.Core.Client;

/// <summary>
/// Reads the fixed file version (VS_FIXEDFILEINFO) of a PE file without OS APIs, so it works on Linux too
/// (FileVersionInfo only reads PE resources on Windows). game.dll 1.127 carries 1.1.2.7.
/// </summary>
public static class PeVersionReader
{
    private const uint Signature = 0xFEEF04BD;

    public static Version? ReadFileVersion(string path)
    {
        byte[] data;
        try { data = File.ReadAllBytes(path); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        return ReadFileVersion(data);
    }

    public static Version? ReadFileVersion(ReadOnlySpan<byte> data)
    {
        if (data.Length < 64 || data[0] != 'M' || data[1] != 'Z') return null;
        Span<byte> sig = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(sig, Signature);
        int i = data.IndexOf(sig);
        // dwSignature, dwStrucVersion, dwFileVersionMS, dwFileVersionLS
        if (i < 0 || i + 16 > data.Length) return null;
        uint ms = BinaryPrimitives.ReadUInt32LittleEndian(data[(i + 8)..]);
        uint ls = BinaryPrimitives.ReadUInt32LittleEndian(data[(i + 12)..]);
        return new Version((int)(ms >> 16), (int)(ms & 0xFFFF), (int)(ls >> 16), (int)(ls & 0xFFFF));
    }

    /// <summary>DAoC numbering: file version 1.1.2.7 = client 1.127 = 1127.</summary>
    public static int ToClientNumber(Version v) => v.Major * 1000 + v.Minor * 100 + v.Build * 10 + v.Revision;

    public static string ToClientString(int number) => $"{number / 1000}.{number % 1000:000}";
}
