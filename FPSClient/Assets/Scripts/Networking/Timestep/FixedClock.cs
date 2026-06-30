using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

public class FixedClock : MonoBehaviour {
    public static float timeScale = 1f;
    public static uint tick;

    private static List<FixedBehaviour> behaviours = new();

    private static Stopwatch stopwatch = Stopwatch.StartNew();
    private static float accumulator;
    private static float currentTime;
    
    // Track the render alpha explicitly
    private static float renderInterpolationAlpha;

    public static void Register(FixedBehaviour behaviour) {
        if (!behaviours.Contains(behaviour))
            behaviours.Add(behaviour);
    }

    public static void Unregister(FixedBehaviour behaviour) {
        behaviours.Remove(behaviour);
    }

    private void Update() {
        InputSystem.Update();
        
        foreach (FixedBehaviour behaviour in behaviours)
            behaviour.UpdateBeforeTick();
        
        float newTime = GetTime();
        float frameTime = Mathf.Max(0f, (newTime - currentTime) * timeScale);
        currentTime = newTime;

        float tickInterval = NetworkSettings.tickTime;
        accumulator += frameTime;

        while (accumulator >= tickInterval) {
            accumulator -= tickInterval;

            Advance();
            tick++;
        }
        
        renderInterpolationAlpha = accumulator / tickInterval;

        foreach (FixedBehaviour behaviour in behaviours)
            behaviour.UpdateAfterTick();
    }

    private static void Advance() {
        TickInvoker.Step();

        foreach (FixedBehaviour behaviour in behaviours)
            behaviour.UpdateFixed();
    }

    public static float GetTime() {
        return (float)stopwatch.Elapsed.TotalSeconds;
    }

    public static float GetInterpolationAlpha() {
        return renderInterpolationAlpha;
    }

    public static void Reset() {
        accumulator = 0f;
        tick = 0;
        timeScale = 1f;
        currentTime = GetTime();
        renderInterpolationAlpha = 0f;
    }
}