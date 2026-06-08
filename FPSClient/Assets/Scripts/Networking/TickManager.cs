using System;
using System.Diagnostics;
using UnityEngine;

public class TickManager : MonoBehaviour {
    private static readonly float timeScale = 1f;
    public static int tick;

    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private double accumulator;
    private double currentTime;

    private double timer;

    private void Update() {
        timer += Time.deltaTime;
        
        while (timer >= NetworkSettings.tickTime)
        {
            timer -= NetworkSettings.tickTime;

            // ProcessTick();
            tick++;
        }
    }

    private void FixedUpdate() {
        ThreadManager.UpdateMain();
        
        if (PlayerMovement.Instance != null ) {
            PlayerInput input = SendInput.Instance.GatherInput(tick);
            PlayerMovement.Instance.SetInputs(input.x, input.y , input.jumping, input.crouching);
            SendInput.Instance.SendPlayerInputs();

            PlayerMovement.Instance.Movement();
        }

        Physics.Simulate(NetworkSettings.tickTime);
    }
}