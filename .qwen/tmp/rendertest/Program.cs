using Doom.Assets;
using Doom.Configuration;
using Doom.Gameplay;
using Doom.Rendering;
using Doom.World;

// Беззвуковой прогон рендера: проверяем пол/потолок, скайбокс, низкие стены, наклон и снаряды.

var checks = new List<string>();
void Check(string name, bool ok, string details = "")
{
    checks.Add($"{(ok ? "OK  " : "FAIL")} {name} {details}");
    if (!ok)
        Environment.ExitCode = 1;
}

AssetStore assets = new();
assets.Load();

// --- Карта ---
Level level = Level.CreateDefault();
Check("карта: периметр — стены", level.TryGetTile(0, 0, out int t0) && t0 == 1);
Check("карта: внутри улья пол-металл", level.FloorAt(4, 5) == 7, $"floor={level.FloorAt(4, 5)}");
Check("карта: внутри улья потолок=2", level.CeilingAt(4, 5) == 2, $"ceil={level.CeilingAt(4, 5)}");
Check("карта: снаружи трава", level.FloorAt(15, 20) == 6, $"floor={level.FloorAt(15, 20)}");
Check("карта: тропа из земли", level.FloorAt(9, 10) == 8, $"floor={level.FloorAt(9, 10)}");
Check("карта: над двором небо", level.CeilingAt(15, 20) == 0);
Check("карта: дверь улья проходима", level.IsWalkable(4.5, 7.5));
Check("карта: низкие коды стен поддерживаются движком", Level.WallHeightOf(11) == 0.5 && Level.WallHeightOf(21) == 0.25);

// На карте нет низких стен: перебираем всё, все стены полные.
bool allFullHeight = true;
for (int row = 0; row < level.Rows && allFullHeight; row++)
    for (int col = 0; col < level.Columns && allFullHeight; col++)
        if (level.TryGetTile(col, row, out int tile) && tile > 0 && Level.WallHeightOf(tile) < 1.0)
            allFullHeight = false;
Check("карта: все стены полной высоты (нет странных объектов)", allFullHeight);

// --- Луч: полная стена даёт один сегмент высотой 1 ---
Span<WallHit> hits = stackalloc WallHit[GameConfig.MaxWallHitsPerRay];
int multi = Raycaster.CastAll(level, 5.5, 15.5, Math.PI / 2, hits);
Check("луч: стена — сегмент полной высоты", multi >= 1 && hits[0].Height == 1.0, $"hits={multi}");

// --- Текстуры ---
Check("ассеты: пол-трава загружен", assets.GetFloor(6) is not null);
Check("ассеты: пол-металл загружен (7.png)", assets.GetFloor(7) is not null);
Check("ассеты: пол-земля загружен", assets.GetFloor(8) is not null);
Check("ассеты: скайбокс загружен", assets.GetSkybox() is not null);
Texture? sky = assets.GetSkybox();
if (sky is not null)
{
    int middle = sky.SamplePixel(sky.Width / 2, sky.Height / 2) & 0xFFFFFF;
    int top = sky.SamplePixel(sky.Width / 2, sky.Height / 8) & 0xFFFFFF;
    Check("скайбокс: горизонт не чёрный", middle != 0, $"#{middle:X6}");
    Check("скайбокс: верх (зенит) не чёрный", top != 0, $"#{top:X6}");
}

// --- Рендер: двор (небо + трава) ---
var player = new Player();
player.Reset();
player.Turn(Math.PI / 2); // на юг, к открытому полю

var framebuffer = new Framebuffer(160, 40);
var viewport = new Viewport(160, 40);
var renderer = new SceneRenderer(assets);
var projectiles = new ProjectileSystem();

renderer.Render(framebuffer, viewport, level, player, Array.Empty<Pickup>(), projectiles, 0.0);

// Цвета фреймбуфера хранятся по ячейкам: foreground = верхний «пиксель» ячейки,
// background = нижний. Берём ячейку заметно выше горизонта и заметно ниже.
int horizonCell = 40 / 2;
int skySample = framebuffer.Foreground[(horizonCell / 2) * 160 + 80];
int floorSample = framebuffer.Background[((40 * 7) / 8) * 160 + 80];
Check("рендер: над горизонтом не чёрное (скайбокс/небо)", (skySample & 0xFFFFFF) != 0, $"#{skySample & 0xFFFFFF:X6}");
Check("рендер: пол внизу не чёрный", (floorSample & 0xFFFFFF) != 0, $"#{floorSample & 0xFFFFFF:X6}");

// --- Рендер: внутри здания (потолок) ---
var inside = new Player();
inside.Reset();
inside.Turn(0); // на восток вдоль коридора улья
var framebuffer2 = new Framebuffer(160, 40);
renderer.Render(framebuffer2, viewport, level, inside, Array.Empty<Pickup>(), projectiles, 0.0);
Check("рендер: внутри здания без исключений", true);

// --- Наклон взгляда ---
var pitched = new Player();
pitched.Reset();
pitched.TurnPitch(1.1);
Check("наклон: достигается максимум", pitched.Pitch >= GameConfig.MaxPitchRadians - 1e-9, $"pitch={pitched.Pitch}");

// На максимуме наклона горизонт уходит за пределы кадра — стены уезжают,
// видно сплошной потолок (вверх) или сплошной пол (вниз).
double horizonAtMax = viewport.HorizonPixels(pitched.Pitch);
Check("наклон: горизонт ушёл за край кадра", horizonAtMax >= viewport.PixelRows * 1.9,
    $"horizon={horizonAtMax:0.0} из {viewport.PixelRows}");

var framebuffer3 = new Framebuffer(160, 40);
renderer.Render(framebuffer3, viewport, level, pitched, Array.Empty<Pickup>(), projectiles, 0.0);
Check("рендер: с наклоном без исключений", true);

// --- Снаряд с наклоном летит по высоте ---
var shoot = new ProjectileSystem();
shoot.Launch(ProjectileKind.Rocket, 2.5, 2.5, 0.0, pitch: 0.5);
var rocket = shoot.Projectiles.First();
double z0 = rocket.Z;
shoot.Update(0.05, level);
Check("снаряд: летит вверх при наклоне", rocket.Alive && rocket.Z > z0, $"z {z0:0.00}->{rocket.Z:0.00}");

// Спрайт рождается на штатной высоте ствола (центр полосы ракеты = 0.40), а не у потолка.
var spawn = new ProjectileSystem();
spawn.Launch(ProjectileKind.Rocket, 2.5, 2.5, 0.0, pitch: 0.0);
var spawned = spawn.Projectiles[0];
Check("снаряд: спавнится у ствола (Z=0.40, полоса 0.32..0.48)",
    Math.Abs(spawned.Z - 0.40) < 1e-9 && Math.Abs(spawned.BandHalf - 0.08) < 1e-9,
    $"z={spawned.Z:0.00} half={spawned.BandHalf:0.00}");

// Взрыв о потолок ВНУТРИ здания (улей, металл-пол, потолок 2).
var shootUp = new ProjectileSystem();
shootUp.Launch(ProjectileKind.Rocket, 4.5, 5.5, 0.0, pitch: 0.55);
for (int i = 0; i < 400 && shootUp.Projectiles.Count > 0; i++)
    shootUp.Update(0.05, level);
Check("снаряд: внутри здания взорвался о потолок", shootUp.Projectiles.Count == 0);

// На открытом небе потолка нет: снаряд улетает вверх и НЕ взрывается сразу
// (1 секунда полёта — до периметра карты он бы не долетел).
var shootSky = new ProjectileSystem();
shootSky.Launch(ProjectileKind.Rocket, 15.5, 10.5, 0.0, pitch: 0.55);
for (int i = 0; i < 20; i++)
    shootSky.Update(0.05, level);
Check("снаряд: в небе летит (не взрывается мгновенно)", shootSky.Projectiles.Count == 1,
    shootSky.Projectiles.Count == 1 ? $"z={shootSky.Projectiles[0].Z:0.0}" : "взорвался рано");

// В конце времени жизни — всё же взрывается (воздушный взрыв).
for (int i = 0; i < 400 && shootSky.Projectiles.Count > 0; i++)
    shootSky.Update(0.05, level);
Check("снаряд: в небе взорвался по времени жизни", shootSky.Projectiles.Count == 0);

// Ровный снаряд летит на высоте запуска и не падает
var shootFlat = new ProjectileSystem();
shootFlat.Launch(ProjectileKind.Rocket, 15.5, 10.5, 0.0, pitch: 0.0);
shootFlat.Update(0.05, level);
Check("снаряд: без наклона держит высоту", Math.Abs(shootFlat.Projectiles[0].Z - 0.40) < 1e-9);

// --- Производительность ---
var bigFrame = new Framebuffer(280, 70);
var bigViewport = new Viewport(280, 70);
var sw = System.Diagnostics.Stopwatch.StartNew();
const int Frames = 30;
for (int i = 0; i < Frames; i++)
    renderer.Render(bigFrame, bigViewport, level, player, Array.Empty<Pickup>(), projectiles, i * 0.016);
sw.Stop();
double ms = sw.Elapsed.TotalMilliseconds / Frames;
Check("перф: кадр 280x70 быстрее 100 мс", ms < 100, $"{ms:0.0} мс/кадр");

Console.WriteLine(string.Join(Environment.NewLine, checks));
