using System;
using System.Collections.Generic;

public sealed class TickInvoker
{
    private sealed class Timer
    {
        public int Id;
        public int TicksRemaining;
        public Action Callback;
    }

    private readonly List<Timer> timers = new();
    private int nextId = 1;

    public int Invoke(Action callback, int ticks)
    {
        int id = nextId++;

        timers.Add(new Timer
        {
            Id = id,
            TicksRemaining = ticks,
            Callback = callback
        });

        return id;
    }

    public void Cancel(int id)
    {
        for (int i = timers.Count - 1; i >= 0; i--)
        {
            if (timers[i].Id == id)
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
            if (timers[i].Callback == callback)
            {
                timers.RemoveAt(i);
            }
        }
    }

    public bool IsPending(int id)
    {
        for (int i = 0; i < timers.Count; i++)
        {
            if (timers[i].Id == id)
                return true;
        }

        return false;
    }

    public void Step()
    {
        for (int i = timers.Count - 1; i >= 0; i--)
        {
            Timer timer = timers[i];

            timer.TicksRemaining--;

            if (timer.TicksRemaining > 0)
                continue;

            timers.RemoveAt(i);

            try
            {
                timer.Callback?.Invoke();
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