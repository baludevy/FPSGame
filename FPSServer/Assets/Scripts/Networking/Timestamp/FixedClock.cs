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

    public static void Reset() {
        accumulator = 0f;
        tick = 0;
        timeScale = 1f;
    }
}