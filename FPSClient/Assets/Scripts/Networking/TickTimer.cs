using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class TickTimer : MonoBehaviour {
    public static TickTimer Instance;
    
    public static float timeScale = 1f;
    public static uint tick;

    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    public double accumulator;
    private double currentTime;
    
    private double timer;

    public static bool doTick = false;

    private void Awake() {
        Instance = this;
    }

    private void Update() {
        double newTime = GetTime();
        double frameTime = (newTime - currentTime) * timeScale;
        currentTime = newTime;

        double tickInterval = NetworkSettings.tickTime;
        accumulator += frameTime;

        while (accumulator >= tickInterval) {
            accumulator -= tickInterval;
            ProcessTick();
            tick++;
        }

        if (PlayerMovement.Instance != null && tick - 1 > SendInput.Instance.lastSentTick)
            SendInput.Instance.SendPlayerInputs();
    }

    public double GetTime() {
        return stopwatch.Elapsed.TotalSeconds;
    }

    private void ProcessTick() {
        ThreadManager.UpdateMain();

        if (!doTick) return;

        if (PlayerMovement.Instance != null) {
            PlayerInput input = SendInput.Instance.GatherInput(tick);

            PlayerPrediction.Instance.PredictState(input);
        }
    }

    public void AddTicks(uint ticksToAdd) {
        tick += ticksToAdd;
        accumulator += ticksToAdd * NetworkSettings.tickTime;
    }
}