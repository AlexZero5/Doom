using System.IO.Compression;
using System.Text;

namespace Doom.Assets;

/// <summary>
///     Самодостаточный декодер PNG без внешних пакетов: 8-битные изображения без
///     interlacing, цветовые типы 0 (градации серого), 2 (RGB), 3 (палитра + tRNS),
///     4 (серый + альфа) и 6 (RGBA). Распаковка — штатный DeflateStream.
///     Именно такие PNG сохраняют графические редакторы по умолчанию.
/// </summary>
internal static class PngDecoder
{
    private const int MaxDimension = 8192;

    public static bool TryDecode(byte[] data, out Texture? texture, out string error)
    {
        texture = null;

        if (data.Length < 8 || !HasSignature(data))
        {
            error = "это не PNG-файл";
            return false;
        }

        // ============================================================
        //   Разбор чанков
        // ============================================================
        int width = 0, height = 0, colorType = 0, bitDepth = 0;
        byte[]? palette = null;
        byte[]? transparency = null;

        using var idat = new MemoryStream();

        int offset = 8;
        bool headerRead = false;

        while (offset + 8 <= data.Length)
        {
            int chunkLength = BigEndian(data, offset);
            int dataStart = offset + 8;

            if (chunkLength < 0 || dataStart + chunkLength + 4 > data.Length)
            {
                error = "файл повреждён (обрезан чанк)";
                return false;
            }

            string type = Encoding.ASCII.GetString(data, offset + 4, 4);

            switch (type)
            {
                case "IHDR":
                    width = BigEndian(data, dataStart);
                    height = BigEndian(data, dataStart + 4);
                    bitDepth = data[dataStart + 8];
                    colorType = data[dataStart + 9];
                    headerRead = true;
                    break;

                case "PLTE":
                    palette = data[dataStart..(dataStart + chunkLength)];
                    break;

                case "tRNS" when colorType == 3:
                    transparency = data[dataStart..(dataStart + chunkLength)];
                    break;

                case "IDAT":
                    idat.Write(data, dataStart, chunkLength);
                    break;

                case "IEND":
                    offset = data.Length; // выходим из цикла
                    continue;
            }

            offset = dataStart + chunkLength + 4;
        }

        if (!headerRead)
        {
            error = "нет заголовка IHDR";
            return false;
        }

        if (width < 1 || height < 1 || width > MaxDimension || height > MaxDimension)
        {
            error = $"недопустимый размер {width}x{height}";
            return false;
        }

        if (bitDepth != 8)
        {
            error = $"глубина {bitDepth} бит не поддерживается — пересохраните как 8 бит";
            return false;
        }

        if (colorType is not (0 or 2 or 3 or 4 or 6))
        {
            error = $"тип цвета {colorType} не поддерживается";
            return false;
        }

        if (colorType == 3 && palette is null)
        {
            error = "палитровый PNG без таблицы PLTE";
            return false;
        }

        // ============================================================
        //   Распаковка zlib (2 байта заголовка + deflate + adler32)
        // ============================================================
        int bytesPerPixel = colorType switch
        {
            0 => 1,
            2 => 3,
            3 => 1,
            4 => 2,
            _ => 4
        };

        int stride = width * bytesPerPixel;
        byte[] raw = new byte[height * (1 + stride)];

        byte[] compressed = idat.ToArray();
        if (compressed.Length < 3)
        {
            error = "нет данных изображения";
            return false;
        }

        try
        {
            using var source = new MemoryStream(compressed, 2, compressed.Length - 2);
            using var deflate = new DeflateStream(source, CompressionMode.Decompress);

            int total = 0;
            while (total < raw.Length)
            {
                int read = deflate.Read(raw, total, raw.Length - total);
                if (read <= 0)
                    break;

                total += read;
            }

            if (total != raw.Length)
            {
                error = "данные изображения повреждены";
                return false;
            }
        }
        catch (InvalidDataException)
        {
            error = "блок zlib повреждён";
            return false;
        }

        // ============================================================
        //   Обратные фильтры сканлайнов
        // ============================================================
        for (int row = 0; row < height; row++)
        {
            int rowStart = row * (1 + stride);
            byte filter = raw[rowStart];

            for (int i = 0; i < stride; i++)
            {
                // Данные строки начинаются на 1 позже байта фильтра; предыдущая
                // восстановленная строка — на stride раньше начала данных.
                int rawValue = raw[rowStart + 1 + i];
                int left = i >= bytesPerPixel ? raw[rowStart + 1 + i - bytesPerPixel] : 0;
                int up = row > 0 ? raw[rowStart - stride + i] : 0;
                int upperLeft = row > 0 && i >= bytesPerPixel ? raw[rowStart - stride + i - bytesPerPixel] : 0;

                int value = filter switch
                {
                    0 => rawValue,
                    1 => rawValue + left,
                    2 => rawValue + up,
                    3 => rawValue + ((left + up) >> 1),
                    4 => rawValue + Paeth(left, up, upperLeft),
                    _ => rawValue
                };

                raw[rowStart + 1 + i] = (byte)value;
            }
        }

        // ============================================================
        //   Конвертация в ARGB
        // ============================================================
        var pixels = new int[width * height];

        for (int y = 0; y < height; y++)
        {
            int lineStart = y * (1 + stride) + 1;

            for (int x = 0; x < width; x++)
            {
                int p = lineStart + x * bytesPerPixel;
                int argb;

                switch (colorType)
                {
                    case 6:
                        argb = Pack(raw[p + 3], raw[p], raw[p + 1], raw[p + 2]);
                        break;

                    case 2:
                        argb = Pack(255, raw[p], raw[p + 1], raw[p + 2]);
                        break;

                    case 4:
                        argb = Pack(raw[p + 1], raw[p], raw[p], raw[p]);
                        break;

                    case 3:
                    {
                        int index = raw[p];
                        int entry = index * 3;
                        byte alpha = transparency is not null && index < transparency.Length
                            ? transparency[index]
                            : (byte)255;
                        argb = entry + 2 < palette!.Length
                            ? Pack(alpha, palette[entry], palette[entry + 1], palette[entry + 2])
                            : Pack(255, 255, 0, 255);
                        break;
                    }

                    default:
                        argb = Pack(255, raw[p], raw[p], raw[p]);
                        break;
                }

                pixels[y * width + x] = argb;
            }
        }

        texture = new Texture(string.Empty, width, height, pixels);
        error = string.Empty;
        return true;
    }

    private static int Pack(int alpha, int r, int g, int b) =>
        (alpha << 24) | (r << 16) | (g << 8) | b;

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);

        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static int BigEndian(byte[] data, int offset) =>
        (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

    private static bool HasSignature(byte[] data)
    {
        ReadOnlySpan<byte> signature = stackalloc byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        return data.AsSpan(0, 8).SequenceEqual(signature);
    }
}
