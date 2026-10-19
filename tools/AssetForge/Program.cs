using AssetForge.Art;
using AssetForge.Core;

// AssetForge — генератор всей графики игры кодом (пиксель-арт в общей палитре).
//
//   dotnet run --project tools/AssetForge -- [папка assets] [--preview папка]
//
// Без аргументов ищет папку assets/ вверх от текущего каталога. Результат детерминирован:
// повторный запуск даёт те же файлы. Чтобы поправить картинку — правьте её код в Art/.

string? assetsRoot = null;
string? previewDir = null;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--preview" && i + 1 < args.Length)
        previewDir = Path.GetFullPath(args[++i]);
    else
        assetsRoot = Path.GetFullPath(args[i]);
}

assetsRoot ??= FindAssets(Directory.GetCurrentDirectory())
               ?? throw new InvalidOperationException("Не найдена папка assets/ — укажите путь аргументом.");

Console.WriteLine($"[forge] assets = {assetsRoot}");
var output = new Output(assetsRoot, previewDir);

// Материалы (стены, полы, потолки, мебель).
var materials = new List<(string, Canvas)>();
foreach ((string name, Func<Canvas> build) in Materials.All())
{
    Canvas texture = build();
    Palette.Apply(texture, dither: 6);
    output.Save($"textures/{name}", texture);
    materials.Add((name, texture));
}

output.Preview("materials", Sheets.Tiled(materials));
output.SaveText("textures/textures.txt", "# имя  ширина высота — метров на один повтор текстуры (остальные 1×1)\n" +
    string.Join("\n", Materials.WorldSizes.Select(size =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{size.Name} {size.Width} {size.Height}"))) + "\n");

foreach ((string name, Canvas sky) in Skies.All())
{
    Palette.Apply(sky, dither: 10);
    output.Save($"sky/{name}", sky);
    output.Preview($"sky_{name}", Sheets.Scale(sky, 2));
}

// Оружие: кадры анимаций.
var overview = new List<Canvas>();
foreach (var group in Guns.All().GroupBy(frame => frame.Weapon))
{
    overview.Add(group.First().Image);
    overview.Add(group.Skip(1).First().Image);

    foreach ((string weapon, string frame, Canvas image) in group)
        output.Save($"weapons/{weapon}/{frame}", image);

    output.Preview($"weapon_{group.Key}", Sheets.Strip(group.Select(frame => frame.Image).ToList(), 2));
}

for (int row = 0; row < 2; row++)
    output.Preview($"weapons_overview{row}", Sheets.Strip(overview.Skip(row * 8).Take(8).ToList(), 1));

// Эффекты и снаряды: anims/<клип>/<кадр>.png + manifest.json.
foreach ((string clip, IReadOnlyList<Canvas> frames, double frameTime, bool loop) in Effects.All())
{
    for (int i = 0; i < frames.Count; i++)
        output.Save($"anims/{clip}/{i}", frames[i]);

    string time = frameTime.ToString(System.Globalization.CultureInfo.InvariantCulture);
    output.SaveText($"anims/{clip}/manifest.json", $"{{ \"frameTime\": {time}, \"loop\": {(loop ? "true" : "false")} }}");
    output.Preview($"anim_{clip}", Sheets.Strip(frames, 4));
}

// Бонусы и декорации: sprites/<имя>.png и sprites/sprites.txt (пикселей на единицу мира).
var spriteScale = new System.Text.StringBuilder("# имя  пикселей_на_метр (картинки нарисованы в старых единицах: 1 = 2.6 м)\n");
string Metric(int pixelsPerUnit) => (pixelsPerUnit / 2.6).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
var pickupImages = new List<Canvas>();
foreach ((string name, Canvas image) in Items.Pickups())
{
    output.Save($"sprites/{name}", image);
    spriteScale.AppendLine($"{name} {Metric(Items.PickupPixelsPerUnit)}");
    pickupImages.Add(image);
}

var decorImages = new List<Canvas>();
foreach ((string name, Canvas image, int pixelsPerUnit) in Items.Decorations())
{
    output.Save($"sprites/{name}", image);
    spriteScale.AppendLine($"{name} {Metric(pixelsPerUnit)}");
    decorImages.Add(image);
}

output.SaveText("sprites/sprites.txt", spriteScale.ToString());
output.Preview("pickups", Sheets.Strip(pickupImages, 2));
output.Preview("decor", Sheets.Strip(decorImages, 2));

// Строка состояния: панель, цифры, лицо.
Canvas statusBar = Hud.StatusBar();
output.Save("hud/statusbar", statusBar);
output.Preview("hud_bar", Sheets.Scale(statusBar, 3));

var digitImages = new List<Canvas>();
foreach ((string name, Canvas image) in Hud.BigDigits())
{
    output.Save($"hud/big_{name}", image);
    digitImages.Add(image);
}

foreach ((string name, Canvas image) in Hud.SmallDigits())
{
    output.Save($"hud/small_{name}", image);
    digitImages.Add(image);
}

var faceImages = new List<Canvas>();
foreach ((string name, Canvas image) in Hud.Faces())
{
    output.Save($"hud/{name}", image);
    faceImages.Add(image);
}

output.Preview("hud_digits", Sheets.Strip(digitImages, 4));
output.Preview("hud_faces", Sheets.Strip(faceImages, 4));

// Интерфейс: шрифт, заливки, логотип, элементы меню.
(Canvas atlas, string fontDescription) = Ui.FontAtlas();
output.Save("ui/font", atlas);
output.SaveText("ui/font.txt", fontDescription);

var fills = new (string Name, Canvas Fill)[]
{
    ("fill_normal", Ui.Fill(521, Rgb.Hex(0xf48466), Rgb.Hex(0xac2c20), Rgb.Hex(0x601210))),
    ("fill_hover", Ui.Fill(522, Rgb.Hex(0xfff6a8), Rgb.Hex(0xe8b830), Rgb.Hex(0x9c4400))),
    ("fill_title", Ui.Fill(523, Rgb.Hex(0xffd878), Rgb.Hex(0xe46c10), Rgb.Hex(0x7a1408))),
    ("fill_value", Ui.Fill(524, Rgb.Hex(0xdcf0ff), Rgb.Hex(0x8cb0d8), Rgb.Hex(0x34508a))),
    ("fill_dim", Ui.Fill(525, Rgb.Hex(0xb4b4b4), Rgb.Hex(0x787878), Rgb.Hex(0x404040))),
};

foreach ((string name, Canvas fill) in fills)
    output.Save($"ui/{name}", fill);

Canvas logo = Ui.Logo("FUNDOOM");
output.Save("ui/logo", logo);

var uiParts = new (string Name, Canvas Image)[]
{
    ("panel", Ui.Panel()),
    ("skull_0", Ui.Skull(false)),
    ("skull_1", Ui.Skull(true)),
    ("slider_track", Ui.SliderTrack()),
    ("slider_fill", Ui.SliderFill()),
    ("slider_knob", Ui.SliderKnob()),
    ("check_off", Ui.Check(false)),
    ("check_on", Ui.Check(true)),
};

foreach ((string name, Canvas image) in uiParts)
    output.Save($"ui/{name}", image);

output.Preview("ui_font", Sheets.Scale(atlas, 4));
output.Preview("ui_text", Sheets.Scale(FontPreview.Render(atlas, new[]
{
    ("НОВАЯ ИГРА  НАСТРОЙКИ  ВЫХОД", fills[0].Fill),
    ("ПРОДОЛЖИТЬ  ЧИТЫ  СЪЕШЬ ЖЕ ЕЩЁ ЭТИХ", fills[1].Fill),
    ("THE QUICK BROWN FOX JUMPS OVER", fills[2].Fill),
    ("0123456789 %  +-=/()!?.,:;  ЮЯЩШЦФЖЙЫЬЪЭ", fills[3].Fill),
    ("КАЧЕСТВО ГРАФИКИ: ВЫСОКОЕ ◄ ► ✓", fills[4].Fill),
}), 3));
output.Preview("ui_logo", Sheets.Scale(logo, 3));
output.Preview("ui_parts", Sheets.Strip(uiParts.Select(p => p.Image).Concat(fills.Select(f => f.Fill)).ToList(), 4));
output.Preview("palette", Sheets.Scale(Palette.Swatch(), 2));
Console.WriteLine($"[forge] готово: {output.Count} файлов");
return 0;

static string? FindAssets(string start)
{
    for (string? dir = start; dir is not null; dir = Path.GetDirectoryName(dir))
    {
        string candidate = Path.Combine(dir, "assets");
        if (Directory.Exists(candidate) && File.Exists(Path.Combine(dir, "Doom.sln")))
            return candidate;
    }

    return null;
}

/// <summary>Запись файлов: ассеты в assets/, листы предпросмотра — в отдельную папку.</summary>
internal sealed class Output(string assetsRoot, string? previewDir)
{
    public int Count { get; private set; }

    public void Save(string relative, Canvas canvas)
    {
        Png.Save(canvas, Path.Combine(assetsRoot, relative.Replace('/', Path.DirectorySeparatorChar) + ".png"));
        Count++;
    }

    public void SaveText(string relative, string text)
    {
        string path = Path.Combine(assetsRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        Count++;
    }

    public void Preview(string name, Canvas canvas)
    {
        if (previewDir is not null)
            Png.Save(canvas, Path.Combine(previewDir, name + ".png"));
    }
}
