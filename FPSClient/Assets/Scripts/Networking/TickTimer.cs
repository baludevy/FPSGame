using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class TickTimer : MonoBehaviour {
    public static TickTimer Instance;

    public static float timeScale = 1f;
    public static uint tick;

    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    public float accumulator;
    private float currentTime;

    private float timer;

    public static bool doTick = false;

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

            if (!doTick) return;
            ProcessTick();
            tick++;
        }

        if (PlayerMovement.Instance != null && tick - 1 > SendInput.Instance.lastSentTick)
            SendInput.Instance.SendPlayerInputs();
    }

    public float GetTime() {
        return (float)stopwatch.Elapsed.TotalSeconds;
    }

    private void ProcessTick() {
        if (PlayerMovement.Instance != null) {
            PlayerInput input = SendInput.Instance.GatherInput(tick);

            SendInput.Instance.ProcessInput(input);
        }
    }

    public void AddTicks(uint ticksToAdd) {
        tick += ticksToAdd;
        accumulator += ticksToAdd * NetworkSettings.tickTime;
    }
}