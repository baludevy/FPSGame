using System;
using System.Collections.Generic;

public sealed class TickInvoker
{
    private sealed class Timer
    {
        public int id;
        public int ticksRemaining;
        public Action callback;
    }

    private readonly List<Timer> timers = new();
    private int nextId = 1;

    public int Invoke(Action callback, int ticks)
    {
        int id = nextId++;

        timers.Add(new Timer
        {
            id = id,
            ticksRemaining = ticks,
            callback = callback
        });

        return id;
    }

    public void Cancel(int id)
    {
        for (int i = timers.Count - 1; i >= 0; i--)
        {
            if (timers[i].id == id)
            {
                timers.RemoveAt(i);
                return;
            }
        }
    }

    public void Cancel(Action callback)
    {
        for (int i = timers.Count - 1; i >= 0; i--)
        {
            if (timers[i].callback == callback)
            {
                timers.RemoveAt(i);
            }
        }
    }

    public bool IsPending(int id)
    {
        for (int i = 0; i < timers.Count; i++)
        {
            if (timers[i].id == id)
                return true;
        }

        return false;
    }

    public void Step()
    {
        for (int i = timers.Count - 1; i >= 0; i--)
        {
            Timer timer = timers[i];

            timer.ticksRemaining--;

            if (timer.ticksRemaining > 0)
                continue;

            timers.RemoveAt(i);

            try
            {
                timer.callback?.Invoke();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }
        }
    }

    public void Clear()
    {
        timers.Clear();
    }
}