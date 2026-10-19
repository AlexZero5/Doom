namespace Doom.Rendering;

/// <summary>
///     Отправка кадров в терминал отдельным потоком. Запись в stdout блокируется, пока
///     терминал не разберёт предыдущий кадр (на больших кадрах — десятки миллисекунд),
///     и раньше на это время замирал весь главный цикл: ввод, симуляция и рендер.
///     Теперь главный цикл только кладёт копию кадра в «почтовый ящик» и идёт дальше,
///     а поток вывода берёт самый свежий кадр, как только терминал освободится.
///     Неуспевшие кадры просто заменяются новыми — терминал всегда показывает
///     актуальную картинку с минимальной задержкой.
/// </summary>
internal sealed class FramePipeline : IDisposable
{
    private readonly ConsolePresenter _presenter;
    private readonly Thread _thread;
    private readonly object _gate = new();

    private Framebuffer _pending = new(1, 1);
    private Framebuffer _working = new(1, 1);
    private bool _hasPending;
    private bool _busy;
    private bool _stopping;

    private long _presentedFrames;

    public FramePipeline(ConsolePresenter presenter)
    {
        _presenter = presenter;
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "Doom frame output",
            Priority = ThreadPriority.AboveNormal
        };
        _thread.Start();
    }

    /// <summary>Сколько кадров реально отправлено в терминал (для счётчика FPS).</summary>
    public long PresentedFrames => Interlocked.Read(ref _presentedFrames);

    /// <summary>
    ///     Ставит кадр в очередь на вывод (копией — исходный буфер можно сразу рисовать
    ///     заново). Если предыдущий кадр ещё не ушёл, он заменяется этим.
    /// </summary>
    public void Submit(Framebuffer frame)
    {
        lock (_gate)
        {
            if (_pending.Columns != frame.Columns || _pending.Rows != frame.Rows)
                _pending.Resize(frame.Columns, frame.Rows);

            int cells = frame.Columns * frame.Rows;
            frame.Chars.AsSpan(0, cells).CopyTo(_pending.Chars);
            frame.Foreground.AsSpan(0, cells).CopyTo(_pending.Foreground);
            frame.Background.AsSpan(0, cells).CopyTo(_pending.Background);

            _hasPending = true;
            Monitor.PulseAll(_gate);
        }
    }

    /// <summary>Дожидается, пока все поставленные кадры уйдут в терминал.</summary>
    public void Flush()
    {
        lock (_gate)
        {
            while ((_hasPending || _busy) && !_stopping)
                Monitor.Wait(_gate);
        }
    }

    /// <summary>
    ///     Выполняет действие с презентером, когда поток вывода простаивает: смена
    ///     размера, сброс снимка и прямой вывод в терминал не должны пересекаться
    ///     с отправкой кадра. Недоотправленный кадр выбрасывается — он устарел.
    /// </summary>
    public void Exclusive(Action<ConsolePresenter> action)
    {
        lock (_gate)
        {
            _hasPending = false;

            while (_busy && !_stopping)
                Monitor.Wait(_gate);

            action(_presenter);
        }
    }

    public void Dispose()
    {
        Flush();

        lock (_gate)
        {
            _stopping = true;
            Monitor.PulseAll(_gate);
        }

        _thread.Join(500);
    }

    private void Run()
    {
        while (true)
        {
            lock (_gate)
            {
                while (!_hasPending && !_stopping)
                    Monitor.Wait(_gate);

                if (_stopping)
                    return;

                // Забираем кадр обменом буферов: без копирования под замком.
                (_working, _pending) = (_pending, _working);
                _hasPending = false;
                _busy = true;
            }

            try
            {
                _presenter.Present(_working);
                Interlocked.Increment(ref _presentedFrames);
            }
            catch
            {
                // Вывод закрыт или терминал отвалился — игра должна суметь выйти штатно.
            }
            finally
            {
                lock (_gate)
                {
                    _busy = false;
                    Monitor.PulseAll(_gate);
                }
            }
        }
    }
}
