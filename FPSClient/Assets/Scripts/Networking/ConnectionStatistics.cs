using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public static class ConnectionStatistics {
    public static float ping;
    public static float packetLoss;
    public static float jitter;

    public static float totalRtt;

    //smoothing
    private static float pingSmooth = 0.1f;
    private static float packetLossSmooth = 0.1f;

    //jitter
    private static float lastTransitTime = -1f;
    private static bool hasLastTransit;

    //packet loss
    private const int lossWindow = 128;
    private static bool[] received = new bool[lossWindow];
    private static int receivedCount;
    private static uint latestTick;
    private static bool hasTick;

    public static void CalculateStatistics(uint serverTick, float clientReceive, float clientSend, float serverReceive,
        float serverSend) {
        float serverProcessTime = serverSend - serverReceive;

        float pingSample = clientReceive - clientSend - serverProcessTime;
        float totalRttSample = clientReceive - clientSend + (NetworkSettings.interpTime);

        UpdatePing(pingSample);
        UpdateTotalRtt(totalRttSample);
        UpdateJitter(serverSend, clientReceive);
        UpdatePacketLoss(serverTick);
    }

    private static void UpdatePing(float pingSample) {
        if (ping == 0f) {
            ping = pingSample;
        }

        ping = Mathf.Max(0, (pingSample * pingSmooth) + (ping * (1.0f - pingSmooth)));
    }

    private static void UpdateTotalRtt(float totalRttSample) {
        if (totalRtt == 0f) {
            totalRtt = totalRttSample;
        }

        totalRtt = Mathf.Max(0, (totalRttSample * pingSmooth) + (totalRtt * (1.0f - pingSmooth)));
    }

    private static void UpdateJitter(float serverSend, float clientReceive) {
        float currentTransitTime = clientReceive - serverSend;

        if (!hasLastTransit) {
            hasLastTransit = true;
            lastTransitTime = currentTransitTime;
            return;
        }

        float delta = currentTransitTime - lastTransitTime;
        lastTransitTime = currentTransitTime;

        // rfc 3550
        jitter += (Math.Abs(delta) - jitter) / 16f;
    }

    private static void UpdatePacketLoss(uint serverTick) {
        if (!hasTick) {
            hasTick = true;
            latestTick = serverTick;
        }

        if (serverTick <= latestTick && hasTick) {
            // out of order
            return;
        }

        for (uint t = latestTick + 1; t < serverTick; t++) {
            int slot = (int)(t % lossWindow);
            if (received[slot]) receivedCount--;
            received[slot] = false;
        }

        int arrivedSlot = (int)(serverTick % lossWindow);
        if (!received[arrivedSlot]) receivedCount++;
        received[arrivedSlot] = true;

        latestTick = serverTick;

        float sampleLoss = 1f - (receivedCount / (float)lossWindow);
        packetLoss = (sampleLoss * packetLossSmooth) + (packetLoss * (1f - packetLossSmooth));
    }

    public static void ApplyAdjustments() {
        float jitterInTicks = jitter / NetworkSettings.tickTime;

        int calculatedBuffer = Mathf.CeilToInt(jitterInTicks * 3f);

        float lossPercentage = packetLoss * 100f;

        NetworkSettings.targetInpBufferOffset = calculatedBuffer;

        NetworkSettings.interpTime = Math.Max(1, calculatedBuffer) * NetworkSettings.tickTime;

        int inputRedundancy = Mathf.Clamp(Mathf.RoundToInt(lossPercentage / 3f), 0, 5);
        NetworkSettings.inputRedundancy = inputRedundancy;
    }

    public static void Reset() {
        ping = 0f;
        packetLoss = 0f;
        jitter = 0f;
        totalRtt = 0f;

        lastTransitTime = -1f;
        hasLastTransit = false;

        Array.Clear(received, 0, received.Length);
        receivedCount = 0;
        latestTick = 0;
        hasTick = false;
    }
}