using Doom.Assets;
using Doom.Configuration;
using Doom.Gameplay;
using Doom.Rendering;
using Doom.World;

// Proverki karty "Gorodok u zamka" (assets/maps/town.map): zagruzka, 3D-fizika, lestnicy,
// dveri, vystrely po vzglyadu, rendering i skorost.

var checks = new List<string>();
void Check(string name, bool ok, string details = "")
{
    checks.Add($"{(ok ? "OK  " : "FAIL")} {name} {details}");
    if (!ok)
        Environment.ExitCode = 1;
}

AssetStore assets = new();
assets.Load();

string? mapText = assets.ReadText("maps/town.map");
Check("karta: fail naiden", mapText is not null);
if (mapText is null)
{
    Console.WriteLine(string.Join(Environment.NewLine, checks));
    return;
}

Level level = MapLoader.Parse(mapText, TextureMetrics.Parse(assets.ReadText("textures/textures.txt")));
var renderer = new SceneRenderer(assets);
var projectiles = new ProjectileSystem();
double D(double degrees) => degrees * Math.PI / 180;

// --- Karta ---
Check("karta: vse teksturi est", level.TextureNames.Skip(1).All(name => assets.GetTexture(name) is not null),
    string.Join(",", level.TextureNames.Skip(1).Where(name => assets.GetTexture(name) is null)));
Check("karta: dveri, vorota, lestnicy", level.Doors.Count >= 15 && level.Ladders.Count >= 5,
    $"doors={level.Doors.Count} ladders={level.Ladders.Count}");
Check("karta: start na zemle", Math.Abs(level.StartZ) < 0.01, $"z={level.StartZ:0.00}");

List<Pickup> pickups = level.CreatePickups();
Check("bonusy: est vse stvoly", Enum.GetValues<PickupKind>().Count(kind => pickups.Any(p => p.Kind == kind)) >= 14);
Check("bonusy: est na vysote (bashnya 9 m)", pickups.Any(p => p.Z > 8.9), $"max z={pickups.Max(p => p.Z):0.0}");

// --- Fizika ---
Player Walk(double x, double y, double z, double angle, double forward, double seconds, bool jump = false)
{
    var player = new Player();
    player.Reset(level);
    player.PlaceAt(x, y, z, angle);
    for (double t = 0; t < seconds; t += 0.016)
        player.Simulate(level, forward, 0, jump && t < 0.05, 5.5, 0.016);
    return player;
}

Check("lestnica donzhona: 0 -> 3 m", Math.Abs(Walk(34.6, 20.5, 0, D(-90), 1, 3).Z - 3) < 0.01);
Check("lestnica na stenu: 6 m", Walk(61.25, 27.5, 0, D(-90), 1, 1.9).Z > 5.5);
Check("vertikalnaya lestnica: na senoval", Math.Abs(Walk(72.5, 43.4, 0, D(-90), 1, 3).Z - 3) < 0.01);
Check("prizhok na yaschik 1 m", Math.Abs(Walk(26, 12, 0, 0, 0.0001, 0.3, jump: true) is var p && Walk(26, 12, 0, 0, 1, 1).Z < 0.01 ? 0 : 1) < 0.01);
var jumper = new Player();
jumper.Reset(level);
jumper.PlaceAt(26, 12, 0, 0);
for (double t = 0; t < 0.3; t += 0.016) jumper.Simulate(level, 0, 0, t < 0.05, 5.5, 0.016);
for (double t = 0; t < 1.5; t += 0.016) jumper.Simulate(level, 1, 0, false, 5.5, 0.016);
Check("prizhok: zaprygnul na yaschik", Math.Abs(jumper.Z - 1) < 0.01, $"z={jumper.Z:0.00}");
Check("zakrytye vorota ne proyti", Walk(41.5, 74, 0, D(-90), 1, 8).Y > 34.5);

// --- Dveri ---
Door gate = level.Doors.First(d => Math.Abs(d.Center.Y - 34.5) < 0.6 && Math.Abs(d.Center.X - 41.5) < 0.6);
foreach (Door door in level.Doors.Where(d => d.Group == gate.Group)) door.Toggle(41.5, 37);
for (int i = 0; i < 120; i++) foreach (Door door in level.Doors) door.Update(0.016);
Check("vorota otkrylis: proyti vo dvor", Walk(41.5, 40, 0, D(-90), 1, 3).Y < 33);

// --- Vystrely ---
var shooter = new Player();
shooter.Reset(level);
shooter.PlaceAt(41.5, 30, 0, D(-90), D(-60));
var eye = new Vec3(shooter.X, shooter.Y, shooter.EyeZ);
var direction = Vec3.FromAngles(shooter.Angle, shooter.Pitch);
level.RayCast(eye, direction, 100, out RayHit hit);
Check("vystrel vniz popadaet v pol pod pricelom", Math.Abs(hit.Point.Z) < 0.01 && Math.Abs(hit.Distance - 1.6 / Math.Sin(D(60))) < 0.01,
    $"z={hit.Point.Z:0.00} d={hit.Distance:0.00}");
projectiles.Launch(ProjectileKind.Rocket, eye, direction);
for (int i = 0; i < 100 && projectiles.Projectiles.Count > 0; i++) projectiles.Update(0.016, level);
Impact boom = projectiles.Impacts.First(impact => impact.Kind == ImpactKind.RocketExplosion);
Check("raketa vniz vzryvaetsya v polu po pricelu", boom.Z < 0.3 && Math.Abs(boom.Y - (30 - 1.6 / Math.Tan(D(60)))) < 0.2,
    $"{boom.X:0.00} {boom.Y:0.00} z={boom.Z:0.00}");

// --- Rendering i skorost ---
var player = new Player();
player.Reset(level);
var viewport = new Viewport(280, 70);
var frame = new Framebuffer(280, 80);
renderer.Render(frame, viewport, level, player, pickups, projectiles, 0);
Check("kadr: nebo naverhu ne chernoe", (frame.Foreground[5 * 280 + 140] & 0xFFFFFF) != 0);

foreach (double scale in new[] { 1.0, 2.0 })
{
    GameSettings.Current.RenderScale = scale;
    var sw = System.Diagnostics.Stopwatch.StartNew();
    for (int i = 0; i < 30; i++)
    {
        player.Turn(0.05);
        renderer.Render(frame, viewport, level, player, pickups, projectiles, i * 0.016);
    }

    double ms = sw.Elapsed.TotalMilliseconds / 30;
    Check($"perf: razreshenie 1/{scale} bystree 8 ms", ms < 8.0, $"{ms:0.00} ms/kadr");
}

Console.WriteLine(string.Join(Environment.NewLine, checks));
