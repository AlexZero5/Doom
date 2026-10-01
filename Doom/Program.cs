using Doom.Core;

namespace Doom;

/// <summary>Точка входа: создаёт игру и запускает главный цикл.</summary>
internal static class Program
{
    private static void Main() => new DoomGame().Run();
}
