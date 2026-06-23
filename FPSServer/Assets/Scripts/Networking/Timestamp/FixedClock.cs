using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class FixedClock : MonoBehaviour {
    public static float timeScale = 1f;
    public static uint tick;

    private static List<FixedBehaviour> behaviours = new();

    private static Stopwatch stopwatch = Stopwatch.StartNew();
    private static float accumulator;
    private static float currentTime;

    public static void Register(FixedBehaviour behaviour) {
        if (!behaviours.Contains(behaviour))
            behaviours.Add(behaviour);
    }

    public static void Unregister(FixedBehaviour behaviour) {
        behaviours.Remove(behaviour);
    }


    private void Update() {
        float newTime = GetTime();
        float frameTime = (newTime - currentTime) * timeScale;
        currentTime = newTime;

        float tickInterval = NetworkSettings.tickTime;
        accumulator += frameTime;

        while (accumulator >= tickInterval) {
            accumulator -= tickInterval;
            Advance();

            tick++;
        }
    }

    private static void Advance() {
        foreach (FixedBehaviour behaviour in behaviours)
            behaviour.UpdateFixed();
    }

    public static float GetTime() {
        return (float)stopwatch.Elapsed.TotalSeconds;
    }

    public static float GetAccumulatedTime() {
        return accumulator;
    }

    // Seconds from *now* until `targetTick` is simulated, in this clock's own time base.
    // `tick` is the NEXT tick to run (Advance() runs before tick++), and `accumulator`
    // is how far we already are into the current interval, so the next boundary is
    // (tickTime - accumulator) away and targetTick is (targetTick - tick) boundaries past it.
    //
    // Assumes timeScale == 1 here (sim time == real time) -> don't run TimeScaler on the
    // server. If you ever scale the server clock, divide the result by timeScale.
    //
    //   > 0  => targetTick still in the future: this much slack before consumption
    //   < 0  => already consumed: this many seconds late
    //
    // Reads `tick` and `accumulator` without locking. If AddInputsToQueue runs on a
    // different thread than the sim, these can be read mid-step; the error is sub-tick.
    // Wrap both reads in your sim lock if you want it airtight.
    public static float GetTimeUntilTick(uint targetTick) {
        float interval = NetworkSettings.tickTime;
        long ticksAhead = (long)targetTick - tick;
        return (ticksAhead + 1) * interval - accumulator;
    }

    public static void Reset() {
        accumulator = 0f;
        tick = 0;
        timeScale = 1f;
    }
}