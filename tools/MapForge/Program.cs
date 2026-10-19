using System.Globalization;
using System.Text;

// MapForge — собирает карту «Городок у замка» (assets/maps/town.map) кодом: прямоугольники
// стен, этажей, дыр под лестницы и расстановка предметов. Результат — обычный текстовый файл
// карты, его можно дальше править руками.
//
//   dotnet run --project tools/MapForge [-- путь/к/town.map]

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

const int W = 84, H = 78, Storeys = 4;
var map = new MapGrid(W, H, Storeys);

// ============================================================
//   Земля, ограда, дороги
// ============================================================

map.Floors(0).Fill(1, 1, W - 2, H - 2, 'g');
map.Walls(0).Ring(0, 0, W - 1, H - 1, 'H');
map.Floors(0).Fill(40, 35, 43, H - 2, 'r');      // дорога от ворот на юг
map.Floors(0).Fill(1, 50, W - 2, 52, 'r');       // поперечная дорога
map.Floors(0).Fill(44, 58, 51, 59, 'v');         // дорожка к дому Б
map.Floors(0).Fill(25, 65, 39, 65, 'v');         // дорожка к дому А (боковая дверь)
map.Floors(0).Fill(14, 68, 15, 73, 'v');         // от парадной двери дома А
map.Floors(0).Fill(14, 73, 39, 74, 'v');
map.Floors(0).Fill(72, 48, 73, 49, 'r');         // к амбару
map.Floors(0).Fill(28, 40, 34, 45, 'x');         // пруд

// ============================================================
//   Замок: стены, ворота, башни, донжон
// ============================================================

map.Floors(0).Fill(20, 3, 63, 34, 'k');
for (int s = 0; s <= 1; s++)
    map.Walls(s).Ring(20, 3, 63, 34, 'C', thickness: 2);

// Ворота: решётка в наружном ряду, проход во внутреннем.
map.Walls(0).Set(41, 34, 'G');
map.Walls(0).Set(42, 34, 'G');
map.Walls(0).Set(41, 33, ' ');
map.Walls(0).Set(42, 33, ' ');

var towers = new[]
{
    // угол, дверь во двор, клетка лестницы-люка, лестница (x y угол), проходы на стену (этаж 2)
    (X: 20, Y: 3, Door: (23, 8), Hole: (24, 6), Ladder: (25.0, 6.5, 90), Openings: new[] { (25, 3), (25, 4), (20, 8), (21, 8) }),
    (X: 58, Y: 3, Door: (60, 8), Hole: (59, 6), Ladder: (59.0, 6.5, 270), Openings: new[] { (58, 3), (58, 4), (62, 8), (63, 8) }),
    (X: 20, Y: 29, Door: (23, 29), Hole: (24, 31), Ladder: (25.0, 31.5, 90), Openings: new[] { (25, 33), (25, 34), (20, 29), (21, 29) }),
    (X: 58, Y: 29, Door: (60, 29), Hole: (59, 31), Ladder: (59.0, 31.5, 270), Openings: new[] { (58, 33), (58, 34), (62, 29), (63, 29) }),
};

// Зубцы стены — по наружному краю, кроме башен.
for (int x = 20; x <= 63; x++)
for (int y = 3; y <= 34; y++)
{
    bool outer = x == 20 || x == 63 || y == 3 || y == 34;
    bool inTower = towers.Any(t => x >= t.X && x <= t.X + 5 && y >= t.Y && y <= t.Y + 5);
    if (outer && !inTower && (x + y) % 2 == 0)
        map.Walls(2).Set(x, y, 'M');
}

foreach (var tower in towers)
{
    int x0 = tower.X, y0 = tower.Y, x1 = x0 + 5, y1 = y0 + 5;
    for (int s = 0; s <= 2; s++)
    {
        map.Walls(s).Ring(x0, y0, x1, y1, 'C');
        map.Walls(s).Fill(x0 + 1, y0 + 1, x1 - 1, y1 - 1, ' ');
        map.Ceilings(s).Fill(x0 + 1, y0 + 1, x1 - 1, y1 - 1, 'u');
        if (s > 0)
            map.Floors(s).Fill(x0 + 1, y0 + 1, x1 - 1, y1 - 1, 'z');
    }

    map.Floors(3).Fill(x0, y0, x1, y1, 'k');
    for (int x = x0; x <= x1; x++)
    for (int y = y0; y <= y1; y++)
        if ((x == x0 || x == x1 || y == y0 || y == y1) && (x + y) % 2 == 0)
            map.Walls(3).Set(x, y, 'M');

    map.Walls(0).Set(tower.Door.Item1, tower.Door.Item2, 'O');
    foreach ((int ox, int oy) in tower.Openings)
        map.Walls(2).Set(ox, oy, ' ');

    // Сквозной люк для лестницы с земли до крыши.
    for (int boundary = 1; boundary <= 3; boundary++)
        map.Hole(boundary, tower.Hole.Item1, tower.Hole.Item2);
    map.Thing(0, $"prop ladder {tower.Ladder.Item1} {tower.Ladder.Item2} {tower.Ladder.Item3} 9");
}

// Лестницы во дворе на стену (6 м, 30 ступеней).
map.Thing(0, "prop stairs 61.25 21.5 0 1.5 9 6 castle_stone");
map.Thing(0, "prop stairs 22.75 21.5 0 1.5 9 6 castle_stone");

// Донжон: три этажа и крыша с зубцами.
const int kx0 = 33, ky0 = 9, kx1 = 50, ky1 = 22;
for (int s = 0; s <= 2; s++)
{
    map.Walls(s).Ring(kx0, ky0, kx1, ky1, 'K');
    map.Ceilings(s).Fill(kx0 + 1, ky0 + 1, kx1 - 1, ky1 - 1, s == 1 ? 'c' : 'u');
}

map.Floors(0).Fill(kx0 + 1, ky0 + 1, kx1 - 1, ky1 - 1, 'h');
map.Floors(1).Fill(kx0 + 1, ky0 + 1, kx1 - 1, ky1 - 1, 'a');
map.Floors(2).Fill(kx0 + 1, ky0 + 1, kx1 - 1, ky1 - 1, 'a');
map.Floors(3).Fill(kx0, ky0, kx1, ky1, 'k');
for (int x = kx0; x <= kx1; x++)
for (int y = ky0; y <= ky1; y++)
    if ((x == kx0 || x == kx1 || y == ky0 || y == ky1) && (x + y) % 2 == 0)
        map.Walls(3).Set(x, y, 'M');

for (int s = 1; s <= 2; s++)
{
    foreach (int x in new[] { 36, 39, 44, 47 })
    {
        map.Walls(s).Set(x, ky0, 'S');
        map.Walls(s).Set(x, ky1, 'S');
    }

    foreach (int y in new[] { 12, 16, 19 })
    {
        map.Walls(s).Set(kx0, y, 'S');
        map.Walls(s).Set(kx1, y, 'S');
    }
}

map.Walls(0).Set(41, ky1, 'D');
map.Walls(0).Set(42, ky1, 'D');

// Этаж 1: перегородка с дверью (оружейная и покои).
map.Walls(1).Fill(42, ky0 + 1, 42, ky1 - 1, 'K');
map.Walls(1).Set(42, 15, 'O');

// Лестница 0→1 (запад), 1→2 (восток), люк на крышу.
map.Thing(0, "prop stairs 34.6 16 0 1.2 6 3 wood_planks");
map.HoleRect(1, 34, 13, 35, 18);
map.Thing(1, "prop rail 36.05 16 90 6");
map.Thing(1, "prop stairs 48.4 16 0 1.2 6 3 wood_planks");
map.HoleRect(2, 47, 13, 48, 18);
map.Thing(2, "prop rail 46.95 16 90 6");
map.Thing(2, "prop ladder 40.5 10.0 0 3");
map.Hole(3, 40, 10);

// Большой зал.
map.Thing(0, "prop platform 41.5 11.1 0 6 1.8 0.4 wood_planks");
map.Thing(0, "prop armchair 41.5 11.0 180 fabric_red z=0.4");
map.Thing(0, "prop longtable 39 16.5 0 5");
map.Thing(0, "prop longtable 44 16.5 0 5");
foreach (double y in new[] { 14.5, 16.5, 18.5 })
{
    map.Thing(0, $"prop bench 37.9 {y} 90");
    map.Thing(0, $"prop bench 45.1 {y} 270");
}

map.Thing(0, "prop banner 37 10.05 180 2.2 banner_red");
map.Thing(0, "prop banner 46 10.05 180 2.2 banner_red");
map.Thing(0, "prop barrel 48.5 20.5");
map.Thing(0, "prop barrel 47.8 21.2");

// Оружейная и покои (этаж 1), библиотека (этаж 2).
map.Thing(1, "prop crate 38 11 0 0.9");
map.Thing(1, "prop crate 39.2 11 0 0.9");
map.Thing(1, "prop crate 38.6 11 0 0.7 crate z=0.9");
map.Thing(1, "prop bed 46 11.1 0");
map.Thing(1, "prop wardrobe 49.69 19 270");
map.Thing(1, "prop rug 45.5 15 0 1 0.7");
map.Thing(2, "prop bookshelf 36 10.2 180");
map.Thing(2, "prop bookshelf 44 10.2 180");
map.Thing(2, "prop bookshelf 46 21.8 0");
map.Thing(2, "prop table 41 17 0");
map.Thing(2, "prop chair 41 17.9 0");
map.Thing(2, "prop banner 41.5 21.95 0 2.2 banner_blue");

// Двор: колодец, бочки, ящики, сено, знамёна над воротами.
map.Thing(0, "prop well 29 26");
foreach ((double x, double y) in new[] { (55.0, 10.0), (55.8, 10.6), (54.9, 11.3), (27.0, 31.0), (27.7, 31.6) })
    map.Thing(0, $"prop barrel {x} {y}");
map.Thing(0, "prop crate 27 12 0 1");
map.Thing(0, "prop crate 28.05 12 0 1");
map.Thing(0, "prop crate 27.5 12 15 0.8 crate z=1");
map.Thing(0, "prop hay 54 26 0");
map.Thing(0, "prop hay 55.3 26.2 10");
map.Thing(0, "prop hay 54.6 26.1 80 1.2 0.8 0.6 z=0.6");
map.Thing(1, "prop banner 37.5 35.05 180 2.2 banner_red");
map.Thing(1, "prop banner 46.5 35.05 180 2.2 banner_red");

// ============================================================
//   Дом А: одноэтажный дом из прошлой карты (на новом месте)
// ============================================================

string[] houseA =
{
    "BBBwwBBBBBwBBBBwwBBB",
    "B      I    I      B",
    "w      I    I      w",
    "w      I    I      B",
    "B      IIIdII      B",
    "B                  B",
    "BIIIIIII    IIIIIIIB",
    "B      I    I      B",
    "w      I    I      w",
    "w                  B",
    "B      I    I      E",
    "B      I    I      B",
    "BBwwBwBBBEEBBBBwwBBB",
};
const int ax = 5, ay = 55;
map.Walls(0).Stamp(ax, ay, houseA);
map.Floors(0).Fill(ax + 1, ay + 1, ax + 6, ay + 5, 'e');    // спальня
map.Floors(0).Fill(ax + 8, ay + 1, ax + 11, ay + 3, 'b');   // ванная
map.Floors(0).Fill(ax + 8, ay + 4, ax + 11, ay + 12, 'o');  // коридор
map.Floors(0).Fill(ax + 7, ay + 5, ax + 12, ay + 5, 'o');
map.Floors(0).Fill(ax + 7, ay + 9, ax + 12, ay + 9, 'o');
map.Floors(0).Fill(ax + 13, ay + 1, ax + 18, ay + 5, 'n');  // кабинет
map.Floors(0).Fill(ax + 1, ay + 7, ax + 6, ay + 11, 'p');   // гостиная
map.Floors(0).Fill(ax + 13, ay + 7, ax + 19, ay + 11, 't'); // кухня
map.Ceilings(0).Fill(ax + 1, ay + 1, ax + 18, ay + 11, 'c');
map.Ceilings(0).Fill(ax + 1, ay + 7, ax + 6, ay + 11, 'u');
map.Ceilings(0).Fill(ax + 9, ay + 12, ax + 10, ay + 12, 'c');
map.Thing(1, "prop roof 15 61.5 0 20 13 2.4 0.4 roof_tiles brick_red");

map.Thing(0, "prop bed 8.5 57.1 0");
map.Thing(0, "prop wardrobe 11.69 57.5 270");
map.Thing(0, "prop rug 8.5 59.4 0 0.8 0.5");
map.Thing(0, "prop bathtub 13.95 56.45 90");
map.Thing(0, "prop sink 16.69 56.5 270");
map.Thing(0, "prop toilet 16.79 57.9 270");
map.Thing(0, "prop bookshelf 19 56.2 180");
map.Thing(0, "prop bookshelf 21.5 56.2 180");
map.Thing(0, "prop table 20.5 58.6 0");
map.Thing(0, "prop chair 20.5 59.5 0");
map.Thing(0, "prop sofa 8.5 62.42 180");
map.Thing(0, "prop coffeetable 8.5 63.9 0");
map.Thing(0, "prop rug 8.5 64.2 0 0.9 0.6");
map.Thing(0, "prop tv 8.5 66.75 0");
map.Thing(0, "prop armchair 11.55 64 270 fabric_green");
map.Thing(0, "prop bookshelf 6.2 64.6 90");
map.Thing(0, "prop counter 18.55 62.34 180");
map.Thing(0, "prop sink 19.6 62.34 180");
map.Thing(0, "prop stove 20.45 62.34 180");
map.Thing(0, "prop counter 21.5 62.34 180");
map.Thing(0, "prop fridge 23.6 62.34 180");
map.Thing(0, "prop table 20.5 64.9 90");
map.Thing(0, "prop chair 19.4 64.9 90");
map.Thing(0, "prop chair 21.6 64.9 270");
map.Thing(0, "decor decor_plant 13.4 59.4");
map.Thing(0, "decor decor_plant 16.6 66.5");
map.Thing(0, "decor decor_floorlamp 6.5 62.4");
map.Thing(0, "decor decor_floorlamp 23.4 56.5");

// ============================================================
//   Дом Б: два этажа, лестница внутри, балкон с наружной лестницей
// ============================================================

const int bx0 = 52, by0 = 55, bx1 = 65, by1 = 66;
for (int s = 0; s <= 1; s++)
{
    map.Walls(s).Ring(bx0, by0, bx1, by1, 'Y');
    map.Walls(s).Fill(bx0 + 1, 61, bx1 - 1, 61, 'I');
    map.Ceilings(s).Fill(bx0 + 1, by0 + 1, bx1 - 1, by1 - 1, 'c');
}

foreach (int x in new[] { 54, 55, 62, 63 }) map.Walls(0).Set(x, by0, 'y');
foreach (int x in new[] { 55, 56, 61 }) map.Walls(0).Set(x, by1, 'y');
foreach (int y in new[] { 57, 63 }) map.Walls(0).Set(bx1, y, 'y');
map.Walls(0).Set(bx0, 63, 'y');
map.Walls(0).Set(bx0, 58, 'e');
map.Walls(0).Set(bx0, 59, 'e');
map.Walls(0).Set(55, 61, 'd');
foreach (int x in new[] { 54, 55, 59, 62, 63 }) map.Walls(1).Set(x, by0, 'y');
foreach (int x in new[] { 55, 63 }) map.Walls(1).Set(x, by1, 'y');
foreach (int y in new[] { 57, 58, 63, 64 }) map.Walls(1).Set(bx1, y, 'y');
foreach (int y in new[] { 57, 63 }) map.Walls(1).Set(bx0, y, 'y');
map.Walls(1).Fill(58, 62, 58, 65, 'I');
map.Walls(1).Set(60, 61, 'd');
map.Walls(1).Set(58, 64, 'd');
map.Walls(1).Set(61, by1, 'e');

map.Floors(0).Fill(bx0 + 1, by0 + 1, bx1 - 1, 60, 'n');
map.Floors(0).Fill(bx0 + 1, 62, bx1 - 1, by1 - 1, 't');
map.Floors(0).Set(55, 61, 'n');
map.Floors(0).Fill(bx0, 58, bx0, 59, 'n');
map.Floors(1).Fill(bx0 + 1, by0 + 1, bx1 - 1, 60, 'q');
map.Floors(1).Fill(bx0 + 1, 62, bx1 - 1, by1 - 1, 'e');
map.Floors(1).Set(60, 61, 'q');
map.Floors(1).Fill(55, 67, 64, 68, 'z');                      // балкон (без отделки: снаружи кирпич)
map.Floors(1).Set(61, by1, 'e');

map.Thing(0, "prop stairs 59.5 56.6 90 1.2 5 3 wood_planks");
map.HoleRect(1, 57, 56, 61, 57);
map.Thing(1, "prop rail 59.5 57.3 0 5");
map.Thing(1, "prop rail 58.85 68.95 0 7.7");
map.Thing(1, "prop rail 55.05 68 90 2");
map.Thing(0, "prop platform 55.2 68.8 0 0.2 0.2 2.8 wood_dark");
map.Thing(0, "prop platform 62.6 68.8 0 0.2 0.2 2.8 wood_dark");
map.Thing(0, "prop stairs 63.6 71.5 0 1.2 5 3 wood_planks");
map.Thing(2, "prop roof 58.5 60.5 0 14 12 3 0.4 roof_slate brick_yellow");

map.Thing(0, "prop sofa 56 60.58 0 fabric_blue");
map.Thing(0, "prop armchair 64.55 59.3 270 fabric_red");
map.Thing(0, "prop bookshelf 53.2 56.9 90");
map.Thing(0, "prop rug 58.5 59.0 0 1.0 0.6");
map.Thing(0, "prop counter 60.0 65.66 0");
map.Thing(0, "prop stove 61.05 65.66 0");
map.Thing(0, "prop sink 61.95 65.66 0");
map.Thing(0, "prop fridge 64.55 65.66 0");
map.Thing(0, "prop table 56.5 63.6 0");
map.Thing(0, "prop chair 56.5 64.5 0");
map.Thing(0, "prop chair 56.5 62.7 180");
map.Thing(0, "decor decor_plant 53.5 60.4");
map.Thing(1, "prop bookshelf 64.8 59 270");
map.Thing(1, "prop bed 55.2 63.07 0");
map.Thing(1, "prop wardrobe 53.31 64.6 90");
map.Thing(1, "prop bed 62.5 63.07 0");
map.Thing(1, "prop armchair 59.6 65.4 0");
map.Thing(1, "decor decor_plant 53.5 56.5");

// ============================================================
//   Амбар: высокий, с сеновалом и лестницей
// ============================================================

const int cx0 = 66, cy0 = 38, cx1 = 79, cy1 = 47;
for (int s = 0; s <= 1; s++)
    map.Walls(s).Ring(cx0, cy0, cx1, cy1, 'P');
map.Walls(0).Set(72, cy1, 'Q');
map.Walls(0).Set(73, cy1, 'Q');
map.Floors(0).Fill(cx0 + 1, cy0 + 1, cx1 - 1, cy1 - 1, 'r');
map.Floors(1).Fill(cx0 + 1, cy0 + 1, cx1 - 1, 42, 'l');
map.Ceilings(0).Fill(cx0 + 1, cy0 + 1, cx1 - 1, 42, 'u');
map.Ceilings(1).Fill(cx0 + 1, cy0 + 1, cx1 - 1, cy1 - 1, 'u');
map.Thing(0, "prop ladder 72.5 43.0 0 3");
map.Thing(1, "prop rail 69.4 42.95 0 4.6");
map.Thing(1, "prop rail 75.6 42.95 0 4.6");
map.Thing(2, "prop roof 72.5 42.5 0 14 10 3 0.4 roof_tiles wood_siding");
map.Thing(0, "prop hay 68 45.6 0");
map.Thing(0, "prop hay 69.3 45.6 0");
map.Thing(0, "prop hay 68.6 45.6 0 1.2 0.8 0.6 z=0.6");
map.Thing(0, "prop hay 77.5 40 90");
map.Thing(0, "prop crate 77.6 45.6 0 1");
map.Thing(0, "prop crate 76.5 45.7 20 0.8");
map.Thing(1, "prop hay 69 40 0");
map.Thing(1, "prop hay 70.3 40.1 5");
map.Thing(1, "prop hay 77 40.5 90");

// ============================================================
//   Улица: машина, скамейки, фонари, деревья, заборы
// ============================================================

map.Thing(0, "prop car 37.5 60 0");
map.Thing(0, "prop bench 45.5 47 270");
map.Thing(0, "prop bench 38.5 55 90");
map.Walls(0).Fill(4, 69, 13, 69, 'F');
map.Walls(0).Fill(16, 69, 25, 69, 'F');
map.Walls(0).Fill(4, 54, 4, 69, 'F');
map.Walls(0).Fill(25, 66, 25, 69, 'F');
for (int y = 38; y <= 74; y += 8)
{
    map.Thing(0, $"decor decor_streetlamp 39.4 {y} solid 0.15");
    map.Thing(0, $"decor decor_streetlamp 44.6 {y + 4} solid 0.15");
}

foreach ((double x, double y) in new[]
         {
             (3.0, 3.0), (9.0, 2.5), (3.5, 10.0), (12.0, 7.0), (4.0, 18.0), (11.0, 15.0), (6.0, 26.0), (13.0, 30.0),
             (3.0, 36.0), (10.0, 40.0), (75.0, 3.0), (80.0, 9.0), (71.0, 12.0), (79.0, 20.0), (70.0, 26.0), (80.0, 31.0),
             (3.0, 47.0), (80.0, 55.0), (80.0, 70.0), (70.0, 73.0), (30.0, 70.0), (47.0, 72.0), (27.0, 48.0)
         })
    map.Thing(0, $"decor decor_tree {x} {y} solid 0.35");

foreach ((double x, double y) in new[]
         {
             (15.0, 4.0), (16.0, 12.0), (15.5, 21.0), (16.0, 28.0), (68.0, 6.0), (67.5, 16.0), (68.0, 30.0), (8.0, 33.0),
             (76.0, 26.0), (35.0, 38.0), (48.0, 38.0)
         })
    map.Thing(0, $"decor decor_pine {x} {y} solid 0.3");

foreach ((double x, double y) in new[]
         {
             (4.5, 53.0), (24.5, 53.5), (26.5, 63.0), (51.0, 54.0), (51.0, 67.5), (66.5, 56.0), (66.5, 66.0), (34.5, 46.5),
             (27.5, 39.0), (65.0, 49.0)
         })
    map.Thing(0, $"decor decor_bush {x} {y}");

foreach ((double x, double y) in new[] { (8.0, 71.0), (11.0, 71.5), (19.0, 71.0), (22.0, 71.5), (57.0, 72.0), (60.0, 72.5) })
    map.Thing(0, $"decor decor_flowers {x} {y}");

// ============================================================
//   Бонусы: оружие спрятано по зданиям и высотам
// ============================================================

map.Thing(0, "pickup clip 41.5 66");
map.Thing(0, "pickup shotgun 9.5 65");
map.Thing(0, "pickup shells 22 64.6");
map.Thing(0, "pickup bulletbox 30 14");
map.Thing(0, "pickup shellbox 55.5 20");
map.Thing(0, "pickup rocket 45 30");
map.Thing(0, "pickup cell 27.5 12 z=1.9");
map.Thing(2, "pickup clip 62.5 27");
map.Thing(1, "pickup chainsaw 70 40.6");
map.Thing(1, "pickup cellpack 76 41");
map.Thing(1, "pickup chaingun 38.6 11 z=1.6");
map.Thing(1, "pickup bulletbox 36.5 20");
map.Thing(1, "pickup plasmarifle 63 59.5");
map.Thing(1, "pickup shells 62 67.8");
map.Thing(1, "pickup cell 22.5 31.5");
map.Thing(2, "pickup rocketbox 45 12");
map.Thing(2, "pickup shellbox 35.5 4");
map.Thing(2, "pickup clip 61.5 31.5");
map.Thing(3, "pickup rocketlauncher 41.5 15");
map.Thing(3, "pickup bfg9000 60.5 5.5");
map.Thing(3, "pickup cellpack 22.5 5.5");

// ============================================================
//   Запись
// ============================================================

string output = args.Length > 0 ? args[0] : FindMap();
File.WriteAllText(output, map.Write(
    header: new[]
    {
        "name = Городок у замка",
        "sky = day",
        "start = 41.5 74 -90",
        "storey = 3",
    },
    walls: new[]
    {
        "H = hedge 0-2.5 keep            # живая изгородь по краю мира",
        "F = fence_wood 0-1.0 keep       # забор (перепрыгивается)",
        "C = castle_stone keep           # крепостная стена и башни",
        "M = castle_stone 0-1.2 keep     # зубцы",
        "K = castle_stone                # донжон (изнутри — отделка комнаты)",
        "S = castle_stone 0-1.0 2.0-3    # окно-бойница донжона",
        "G = gate gate_iron castle_stone 3   # подъёмная решётка ворот (E)",
        "D = door door_leaf castle_stone 2.4 # двери донжона",
        "O = door door_leaf castle_stone 2.2 # двери башен и покоев",
        "B = brick_red                   # дом А",
        "w = brick_red 0-0.9 2.1-3       # окно дома А",
        "E = door door_leaf brick_red 2.2",
        "I = plaster_cream               # перегородки",
        "d = door door_leaf plaster_cream 2.2",
        "Y = brick_yellow                # дом Б",
        "y = brick_yellow 0-0.9 2.1-3",
        "e = door door_leaf brick_yellow 2.2",
        "P = wood_siding keep            # амбар",
        "Q = door door_leaf wood_siding 2.8",
    },
    floors: new[]
    {
        "g = grass", "r = dirt", "v = paving", "x = water", "k = castle_floor", "z = wood_planks",
        "h = castle_floor castle_stone   # большой зал",
        "a = wood_planks wood_panel      # донжон, этажи 1–2",
        "q = wood_planks plaster_cream",
        "l = wood_planks wood_siding     # сеновал",
        "p = parquet wallpaper_beige", "e = parquet wallpaper_blue", "n = parquet wallpaper_green",
        "o = parquet wood_panel", "t = tiles_checker tiles_wall", "b = tiles_bath tiles_wall",
    },
    ceilings: new[]
    {
        "c = ceiling_plaster concrete",
        "u = ceiling_wood castle_floor",
    }));

Console.WriteLine($"[mapforge] {output}");
return 0;

static string FindMap()
{
    for (string? dir = Directory.GetCurrentDirectory(); dir is not null; dir = Path.GetDirectoryName(dir))
    {
        if (File.Exists(Path.Combine(dir, "Doom.sln")))
            return Path.Combine(dir, "assets", "maps", "town.map");
    }

    throw new InvalidOperationException("Не найден корень репозитория (Doom.sln)");
}

/// <summary>Сетки этажей и предметы карты.</summary>
internal sealed class MapGrid
{
    private readonly int _width, _height;
    private readonly Layer[] _walls, _floors, _ceilings;
    private readonly List<string>[] _things;

    public MapGrid(int width, int height, int storeys)
    {
        _width = width;
        _height = height;
        _walls = Enumerable.Range(0, storeys).Select(_ => new Layer(width, height)).ToArray();
        _floors = Enumerable.Range(0, storeys).Select(_ => new Layer(width, height)).ToArray();
        _ceilings = Enumerable.Range(0, storeys).Select(_ => new Layer(width, height)).ToArray();
        _things = Enumerable.Range(0, storeys).Select(_ => new List<string>()).ToArray();
    }

    public Layer Walls(int storey) => _walls[storey];

    public Layer Floors(int storey) => _floors[storey];

    public Layer Ceilings(int storey) => _ceilings[storey];

    public void Thing(int storey, string line) => _things[storey].Add(line);

    /// <summary>Дыра в перекрытии между этажами boundary−1 и boundary (люк, лестничный проём).</summary>
    public void Hole(int boundary, int x, int y)
    {
        _floors[boundary].Set(x, y, ' ');
        _ceilings[boundary - 1].Set(x, y, ' ');
    }

    public void HoleRect(int boundary, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            Hole(boundary, x, y);
    }

    public string Write(string[] header, string[] walls, string[] floors, string[] ceilings)
    {
        var text = new StringBuilder();
        text.AppendLine("# Городок у замка — сгенерировано tools/MapForge (правьте код генератора или этот файл).");
        text.AppendLine("# Формат — Doom/World/MapLoader.cs. Клетка 1×1 м, этаж 3 м. Символ «#» в легендах не используется.");
        text.AppendLine();
        Section(text, "map", header);
        Section(text, "walls", walls);
        Section(text, "floors", floors);
        Section(text, "ceilings", ceilings);

        for (int s = 0; s < _walls.Length; s++)
        {
            text.AppendLine($"[grid walls {s}]").AppendLine(_walls[s].ToString());
            text.AppendLine($"[grid floors {s}]").AppendLine(_floors[s].ToString());
            text.AppendLine($"[grid ceilings {s}]").AppendLine(_ceilings[s].ToString());
        }

        for (int s = 0; s < _things.Length; s++)
            Section(text, s == 0 ? "things" : $"things {s}", _things[s].ToArray());

        return text.ToString();
    }

    private static void Section(StringBuilder text, string name, string[] lines)
    {
        text.AppendLine($"[{name}]");
        foreach (string line in lines)
            text.AppendLine(line);
        text.AppendLine();
    }
}

/// <summary>Одна сетка символов.</summary>
internal sealed class Layer(int width, int height)
{
    private readonly char[,] _cells = Init(width, height);

    private static char[,] Init(int width, int height)
    {
        var cells = new char[height, width];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            cells[y, x] = '.';
        return cells;
    }

    public void Set(int x, int y, char c)
    {
        if (x >= 0 && y >= 0 && x < width && y < height)
            _cells[y, x] = c == ' ' ? '.' : c;
    }

    public void Fill(int x0, int y0, int x1, int y1, char c)
    {
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            Set(x, y, c);
    }

    public void Ring(int x0, int y0, int x1, int y1, char c, int thickness = 1)
    {
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            if (x < x0 + thickness || x > x1 - thickness || y < y0 + thickness || y > y1 - thickness)
                Set(x, y, c);
    }

    public void Stamp(int x0, int y0, string[] rows)
    {
        for (int y = 0; y < rows.Length; y++)
        for (int x = 0; x < rows[y].Length; x++)
            Set(x0 + x, y0 + y, rows[y][x]);
    }

    public override string ToString()
    {
        var text = new StringBuilder();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                text.Append(_cells[y, x]);
            text.AppendLine();
        }

        return text.ToString();
    }
}
