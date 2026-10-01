using Doom.Configuration;
using Doom.Platform;

namespace Doom.Input;

/// <summary>
///     Состояние клавиш. На Windows используется <c>GetAsyncKeyState</c> (плавное удержание),
///     на других платформах — время последнего нажатия, полученного из <see cref="Console" />.
/// </summary>
internal sealed class KeyboardState
{
    private readonly Dictionary<ConsoleKey, long> _lastSeen = new();

    /// <summary>Запоминает момент нажатия клавиши (нужно для не-Windows платформ).</summary>
    public void MarkPressed(ConsoleKey key, long nowMs) => _lastSeen[key] = nowMs;

    public bool IsHeld(ConsoleKey key, long nowMs)
    {
        if (OperatingSystem.IsWindows())
            return (NativeMethods.GetAsyncKeyState((int)key) & 0x8000) != 0;

        return _lastSeen.TryGetValue(key, out long pressedAt)
               && nowMs - pressedAt < GameConfig.HoldTimeoutMs;
    }
}
