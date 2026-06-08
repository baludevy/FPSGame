using System;
using System.Collections.Generic;

public class TickInvoker {
    private class Timer {
        public int TicksRemaining;
        public Action Callback;
    }

    private readonly List<Timer> timers = new();

    public void Invoke(Action callback, float delaySeconds) {
        int ticks = Math.Max(
            1,
            (int)Math.Ceiling(delaySeconds / NetworkSettings.tickTime)
        );

        timers.Add(new Timer {
            TicksRemaining = ticks,
            Callback = callback
        });
    }

    public void Step() {
        for (int i = timers.Count - 1; i >= 0; i--) {
            Timer timer = timers[i];

            timer.TicksRemaining--;

            if (timer.TicksRemaining > 0) {
                continue;
            }

            timer.Callback?.Invoke();
            timers.RemoveAt(i);
        }
    }

    public void Clear() {
        timers.Clear();
    }
}