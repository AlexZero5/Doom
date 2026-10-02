using System.Text;
using System.Text.Json;

// ============================================================================
//  ImgGen — генератор картинок через routerai.ru с сохранением прямо в проект.
//
//  Использование:
//    dotnet run --project tools/ImgGen -- --out assets/walls/1.png
//        --prompt "..." [опции]
//
//  Опции:
//    --out <путь>          куда сохранить PNG (папки создаются сами)
//    --prompt <текст>      промпт; либо --prompt-file <файл>, либо stdin
//    --model <id>          модель routerai (по умолчанию openai/gpt-image-1)
//    --size ШxВ            точный размер, напр. 1024x1536
//    --aspect 1:1          соотношение сторон (вместо --size)
//    --res 1K|2K|4K        тир разрешения
//    --quality auto|low|medium|high
//    --background transparent|opaque|auto   (прозрачный фон: gpt-image-*)
//    --ref <файл.png>      референс для img2img (можно несколько раз)
//    --seed <число>        воспроизводимая генерация
//    --endpoint images|chat  принудительный выбор API (по умолчанию авто:
//                          google/* gemini -> chat, остальные -> images)
//    --key <ключ>          иначе: env ROUTERAI_API_KEY, иначе ~/.qwen/settings.json
// ============================================================================

string? model = null;
string? outPath = null;
string? prompt = null;
string? promptFile = null;
string? size = null;
string? aspect = null;
string? resolution = null;
string? quality = null;
string? background = null;
string? endpointOverride = null;
int? seed = null;
var references = new List<string>();

for (int i = 0; i < args.Length; i++)
{
    string value() => ++i < args.Length ? args[i] : throw new ArgumentException($"Нет значения для {args[i - 1]}");

    switch (args[i])
    {
        case "--model": model = value(); break;
        case "--out": outPath = value(); break;
        case "--prompt": prompt = value(); break;
        case "--prompt-file": promptFile = value(); break;
        case "--size": size = value(); break;
        case "--aspect": aspect = value(); break;
        case "--res": resolution = value(); break;
        case "--quality": quality = value(); break;
        case "--background": background = value(); break;
        case "--endpoint": endpointOverride = value(); break;
        case "--seed": seed = int.Parse(value()); break;
        case "--ref": references.Add(value()); break;
        default: throw new ArgumentException($"Неизвестный аргумент {args[i]}");
    }
}

model ??= "openai/gpt-image-1";

if (outPath is null)
    throw new ArgumentException("Укажи --out <путь.png>");

if (prompt is null && promptFile is not null)
    prompt = File.ReadAllText(promptFile);

if (prompt is null && Console.IsInputRedirected)
    prompt = Console.In.ReadToEnd();

if (string.IsNullOrWhiteSpace(prompt))
    throw new ArgumentException("Пустой промпт: задай --prompt, --prompt-file или stdin");

string endpoint = endpointOverride ??
    (model.Contains("gemini", StringComparison.OrdinalIgnoreCase) ? "chat" : "images");

string apiKey = ResolveApiKey();
string baseUri = "https://routerai.ru/api/v1";

Console.WriteLine($"[imggen] model={model} endpoint={endpoint} refs={references.Count}");

using var http = new HttpClient();
http.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
http.Timeout = TimeSpan.FromMinutes(5);

byte[] imageBytes = Array.Empty<byte>();

try
{
    imageBytes = endpoint == "images"
        ? await GenerateViaImagesEndpoint(prompt!)
        : await GenerateViaChatEndpoint(prompt!);
}
catch (HttpRequestException ex) when (ex.Data.Contains("Body"))
{
    Console.Error.WriteLine($"[imggen] ошибка API: {ex.Message}\n{ex.Data["Body"]}");
    return 2;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath!))!);
await File.WriteAllBytesAsync(outPath!, imageBytes);

var dims = PngSize(imageBytes);
string dimsText = dims is { } d ? $" {d.width}x{d.height}" : "";
Console.WriteLine($"[imggen] готово: {outPath} ({imageBytes.Length / 1024.0:0} КБ{dimsText})");

// Игра умеет только PNG: JPEG/WEBP от моделей вроде gemini в assets класть нельзя.
bool isPng = PngSize(imageBytes) is not null;

if (!isPng && outPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine(
        "[imggen] ПРЕДУПРЕЖДЕНИЕ: модель вернула не-PNG (JPEG/WEBP). " +
        "В assets/ такой файл не годится — используй openai/gpt-image-* (по умолчанию) " +
        "или другой PNG-рендер.");
    return 3;
}

return 0;
// ============================================================================
//   POST /images — gpt-image, flux, seedream, recraft
// ============================================================================

async Task<byte[]> GenerateViaImagesEndpoint(string promptText)
{
    var body = new Dictionary<string, object?>
    {
        ["model"] = model,
        ["prompt"] = promptText,
        ["n"] = 1
    };

    if (size is not null) body["size"] = size;
    else if (aspect is not null) body["aspect_ratio"] = aspect;

    if (resolution is not null) body["resolution"] = resolution;
    if (quality is not null) body["quality"] = quality;
    if (background is not null) body["background"] = background;
    if (seed is not null) body["seed"] = seed;

    if (references.Count > 0)
        body["input_references"] = references
            .Select(path => new { type = "image_url", image_url = new { url = ToDataUri(path) } })
            .ToList();

    using JsonDocument response = await PostJson($"{baseUri}/images", body);
    JsonElement root = response.RootElement;
    JsonElement first = root.GetProperty("data")[0];

    if (first.TryGetProperty("b64_json", out JsonElement b64))
        return Convert.FromBase64String(b64.GetString()!);

    if (first.TryGetProperty("url", out JsonElement url))
        return await http.GetByteArrayAsync(url.GetString());

    throw new InvalidOperationException("В ответе /images нет ни b64_json, ни url");
}

// ============================================================================
//   POST /chat/completions + modalities:[image,text] — gemini
// ============================================================================

async Task<byte[]> GenerateViaChatEndpoint(string promptText)
{
    var parts = new List<object> { new { type = "text", text = promptText } };
    parts.AddRange(references.Select(path => (object)new
    {
        type = "image_url",
        image_url = new { url = ToDataUri(path) }
    }));

    var body = new Dictionary<string, object?>
    {
        ["model"] = model,
        ["messages"] = new[] { new { role = "user", content = parts } },
        ["modalities"] = new[] { "image", "text" }
    };

    if (aspect is not null || resolution is not null)
    {
        body["image_config"] = new Dictionary<string, object?>
        {
            ["aspect_ratio"] = aspect ?? "1:1",
            ["image_size"] = resolution ?? "1K"
        };
    }

    if (seed is not null) body["seed"] = seed;

    using JsonDocument response = await PostJson($"{baseUri}/chat/completions", body);
    JsonElement message = response.RootElement.GetProperty("choices")[0].GetProperty("message");

    if (message.TryGetProperty("images", out JsonElement images) && images.GetArrayLength() > 0)
    {
        JsonElement first = images[0];
        string? dataUri = null;

        if (first.TryGetProperty("image_url", out JsonElement imageUrl) &&
            imageUrl.TryGetProperty("url", out JsonElement url))
            dataUri = url.GetString();
        else if (first.ValueKind == JsonValueKind.String)
            dataUri = first.GetString();

        if (dataUri is not null)
            return DecodeDataPayload(dataUri);
    }

    if (message.TryGetProperty("content", out JsonElement content) &&
        content.ValueKind == JsonValueKind.String)
    {
        string text = content.GetString() ?? string.Empty;
        int marker = text.IndexOf("base64,", StringComparison.Ordinal);

        if (marker > 0)
            return Convert.FromBase64String(text[(marker + 7)..].Trim());
    }

    throw new InvalidOperationException("В ответе chat/completions не найдено изображение");
}

// ============================================================================
//   Помощники
// ============================================================================

async Task<JsonDocument> PostJson(string uri, object payload)
{
    string json = JsonSerializer.Serialize(payload);
    using var content = new StringContent(json, Encoding.UTF8, "application/json");

    using HttpResponseMessage response = await http.PostAsync(uri, content);
    string text = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        var ex = new HttpRequestException($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
        ex.Data["Body"] = text.Length > 1200 ? text[..1200] + "…" : text;
        throw ex;
    }

    return JsonDocument.Parse(text);
}

static string ToDataUri(string path)
{
    string mime = Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        _ => "image/png"
    };

    return $"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(path))}";
}

static byte[] DecodeDataPayload(string payload)
{
    int marker = payload.IndexOf("base64,", StringComparison.Ordinal);
    return marker >= 0
        ? Convert.FromBase64String(payload[(marker + 7)..])
        : Convert.FromBase64String(payload);
}

static (int width, int height)? PngSize(byte[] bytes)
{
    if (bytes.Length < 24 || bytes[0] != 0x89 || bytes[1] != 0x50)
        return null;

    int width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
    int height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
    return (width, height);
}

static string ResolveApiKey()
{
    foreach (string name in new[]
             {
                 "ROUTERAI_API_KEY",
                 "QWEN_CUSTOM_API_KEY_OPENAI_HTTPS_ROUTERAI_RU_API_V1_96301C392009"
             })
    {
        if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } fromEnv)
            return fromEnv;
    }

    string settings = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qwen", "settings.json");

    if (File.Exists(settings))
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(settings));

        if (document.RootElement.TryGetProperty("env", out JsonElement env))
        {
            foreach (JsonProperty property in env.EnumerateObject())
            {
                if (property.Name.Contains("ROUTERAI", StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString()!;
                }
            }
        }
    }

    throw new InvalidOperationException(
        "Ключ routerai не найден: задай переменную окружения ROUTERAI_API_KEY");
}
