using System.Diagnostics;
using UnityEngine;

public class TickTimer : MonoBehaviour {
    private static readonly float timeScale = 1f;
    public static int tick;

    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private double accumulator;
    private double currentTime;

    private double timer;
    

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

        // tickInvoker.Step();

        if (PlayerMovement.Instance != null ) {
            PlayerInput input = SendInput.Instance.GatherInput(tick);

            PlayerMovement.Instance.SetInputs(
                input.x,
                input.y,
                input.jumping,
                input.crouching
            );

            PlayerMovement.Instance.Tick();
        }

        Physics.Simulate(NetworkSettings.tickTime);
    }
}