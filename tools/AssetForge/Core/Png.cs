using System.IO.Compression;

namespace AssetForge.Core;

/// <summary>Минимальный PNG-писатель: RGBA, 8 бит, без чересстрочности (то, что читает игра).</summary>
internal static class Png
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static void Save(Canvas canvas, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        int width = canvas.Width;
        int height = canvas.Height;
        byte[] raw = new byte[height * (width * 4 + 1)];

        for (int y = 0; y < height; y++)
        {
            int row = y * (width * 4 + 1);
            raw[row] = 0; // фильтр None

            for (int x = 0; x < width; x++)
            {
                int argb = canvas[x, y];
                int offset = row + 1 + x * 4;
                raw[offset] = (byte)(argb >> 16);
                raw[offset + 1] = (byte)(argb >> 8);
                raw[offset + 2] = (byte)argb;
                raw[offset + 3] = (byte)((uint)argb >> 24);
            }
        }

        using var file = File.Create(path);
        file.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        byte[] header = new byte[13];
        WriteBigEndian(header, 0, width);
        WriteBigEndian(header, 4, height);
        header[8] = 8; // бит на канал
        header[9] = 6; // RGBA
        WriteChunk(file, "IHDR", header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(raw);

        WriteChunk(file, "IDAT", compressed.ToArray());
        WriteChunk(file, "IEND", Array.Empty<byte>());
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        byte[] length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        stream.Write(length);

        byte[] body = new byte[4 + data.Length];
        for (int i = 0; i < 4; i++)
            body[i] = (byte)type[i];
        Buffer.BlockCopy(data, 0, body, 4, data.Length);
        stream.Write(body);

        uint crc = 0xFFFFFFFF;
        foreach (byte value in body)
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);

        byte[] crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, (int)(crc ^ 0xFFFFFFFF));
        stream.Write(crcBytes);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }

        return table;
    }
}
