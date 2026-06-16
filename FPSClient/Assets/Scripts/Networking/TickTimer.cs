using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class TickTimer : MonoBehaviour {
    public static TickTimer Instance;

    public static float timeScale = 1f;
    public static uint tick;

    private Stopwatch stopwatch = Stopwatch.StartNew();
    public float accumulator;
    private float currentTime;

    private float timer;

    public static bool doTick = true;

    private void Awake() {
        Instance = this;
    }

    private void Update() {
        float newTime = GetTime();
        float frameTime = (newTime - currentTime) * timeScale;
        currentTime = newTime;

        float tickInterval = NetworkSettings.tickTime;
        accumulator += frameTime;

        while (accumulator >= tickInterval) {
            accumulator -= tickInterval;

            ThreadManager.UpdateMain();
            TickSync.Update();

            if (!doTick) return;
            ProcessTick();
            tick++;
        }

        if (LocalPlayer.Instance != null && tick - 1 > InputManager.lastSentTick)
            InputManager.SendPlayerInputs();
    }

    public float GetTime() {
        return (float)stopwatch.Elapsed.TotalSeconds;
    }

    private void ProcessTick() {
        if (LocalPlayer.Instance != null) {
            PlayerInput input = LocalPlayer.Instance.input.GatherInput(tick);

            LocalPlayer.Instance.invoker.Step();
            LocalPlayer.Instance.input.ProcessInput(input);
        }
    }

    public void AddTicks(uint ticksToAdd) {
        tick += ticksToAdd;
        accumulator += ticksToAdd * NetworkSettings.tickTime;
    }
}