using System;
using System.Collections.Generic;

public static class TickInvoker {
    private class Timer {
        public int id;
        public int ticksRemaining;
        public Action callback;
    }

    private static readonly List<Timer> timers = new();
    private static int nextId = 1;

    public static int Invoke(Action callback, int ticks) {
        int id = nextId++;

        timers.Add(new Timer {
            id = id,
            ticksRemaining = ticks,
            callback = callback
        });

        return id;
    }

    public static void Cancel(int id) {
        for (int i = timers.Count - 1; i >= 0; i--) {
            if (timers[i].id == id) {
                timers.RemoveAt(i);
                break;
            }
        }
    }

    public static bool IsPending(int id) {
        for (int i = 0; i < timers.Count; i++) {
            if (timers[i].id == id)
                return true;
        }

        return false;
    }

    public static void Step() {
        for (int i = timers.Count - 1; i >= 0; i--) {
            var timer = timers[i];

            timer.ticksRemaining--;

            if (timer.ticksRemaining > 0)
                continue;

            timers.RemoveAt(i);

            try {
                timer.callback?.Invoke();
            }
            catch (Exception ex) {
                UnityEngine.Debug.LogException(ex);
            }
        }
    }

    public static void Clear() {
        timers.Clear();
    }
}