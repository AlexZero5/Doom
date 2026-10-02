namespace Doom.Assets;

/// <summary>
///     Анимационный клип: кадры из папки assets/anims/&lt;имя&gt;/ и длительность кадра
///     из manifest.json (или значением по умолчанию). Зацикленный клип (ходьба) идёт
///     по кругу, незацикленный (вспышка) замирает на последнем кадре.
/// </summary>
internal sealed class AnimationClip
{
    private readonly Texture[] _frames;

    public AnimationClip(string name, Texture[] frames, double frameTime, bool loop)
    {
        Name = name;
        _frames = frames;
        FrameTime = frameTime > 0.001 ? frameTime : 0.001;
        Loop = loop;
    }

    public string Name { get; }

    public double FrameTime { get; }

    public bool Loop { get; }

    public int FrameCount => _frames.Length;

    /// <summary>Кадр для момента <paramref name="seconds" /> от начала клипа.</summary>
    public Texture? Sample(double seconds)
    {
        if (_frames.Length == 0)
            return null;

        int index = (int)(seconds / FrameTime);

        if (Loop)
        {
            index %= _frames.Length;
            if (index < 0)
                index += _frames.Length;
        }
        else if (index >= _frames.Length)
        {
            index = _frames.Length - 1;
        }

        return index < 0 ? _frames[0] : _frames[index];
    }

    /// <summary>Кадр по прогрессу 0..1 (для эффектов, привязанных ко времени жизни, — взрывы).</summary>
    public Texture? SampleProgress(double progress)
    {
        if (_frames.Length == 0)
            return null;

        int index = (int)(Math.Clamp(progress, 0, 1) * _frames.Length);
        return _frames[Math.Min(index, _frames.Length - 1)];
    }
}
