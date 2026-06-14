using System;
using System.Collections.Generic;
using UnityEngine;

public static class ConnectionStats {
    private static Queue<float> rttSamples = new Queue<float>();
    private static Queue<float> jitterSamples = new Queue<float>();
    private static List<bool> lossHistory = new List<bool>();
    private static readonly object lockObj = new object();

    private static int rttSampleCount = 20;
    private static int lossHistorySize = 100;

    private static float lastRtt = -1f;
    private static uint expectedSequence;
    private static bool initialized;

    public static float ping;
    public static float jitter;
    public static float packetLoss;

    private static List<float> jitterSortBuffer = new List<float>(20);

    private const float slackGrowSpeed = 1.0f;
    private const float slackDecaySpeed = 0.05f;

    private static float smoothedPacketLoss;

    public static void CalculateStats(WorldSnapshot snapshot) {
        lock (lockObj) {
            double clientReceiveTime = TickTimer.Instance.GetTime();

            //ping
            double serverProcessingTime = snapshot.serverSendTime - snapshot.serverReceiveTime;
            float rtt = Mathf.Max(0,
                (float)((clientReceiveTime - snapshot.clientSendTime - serverProcessingTime) * 1000f));

            rttSamples.Enqueue(rtt);
            if (rttSamples.Count > rttSampleCount) rttSamples.Dequeue();
            ping = Average(rttSamples);

            //jitter
            if (lastRtt >= 0f) {
                float instantJitter = Mathf.Abs(rtt - lastRtt);

                jitter = Mathf.Lerp(jitter, instantJitter, 0.1f);
            }
            else {
                jitter = 0f;
            }

            lastRtt = rtt;

            //packet loss
            uint sequence = snapshot.serverTick;

            if (!initialized) {
                expectedSequence = sequence;
                initialized = true;
            }

            int delta = (int)(sequence - expectedSequence);

            if (delta > lossHistorySize || delta < -lossHistorySize) {
                lossHistory.Clear();
                expectedSequence = sequence;
                delta = 0;
            }

            if (delta >= 0) {
                while (expectedSequence != sequence) {
                    lossHistory.Add(true);
                    if (lossHistory.Count > lossHistorySize) lossHistory.RemoveAt(0);
                    expectedSequence++;
                }

                lossHistory.Add(false);
                if (lossHistory.Count > lossHistorySize) lossHistory.RemoveAt(0);
                expectedSequence++;
            }
            else {
                int index = lossHistory.Count - 1 - (int)(expectedSequence - 1 - sequence);
                if (index >= 0 && index < lossHistory.Count) {
                    lossHistory[index] = false;
                }
            }

            int lost = 0;
            foreach (bool wasLost in lossHistory)
                if (wasLost)
                    lost++;
            float rawPacketLoss = lossHistory.Count > 0 ? (lost / (float)lossHistory.Count) * 100f : 0f;

            smoothedPacketLoss = Mathf.Lerp(smoothedPacketLoss, rawPacketLoss, 0.05f);
            packetLoss = smoothedPacketLoss;

            //adjustments
            
            float tickTimeMs = NetworkSettings.tickTime * 1000f;

            int targetSlack = Mathf.FloorToInt(jitter / tickTimeMs);
            targetSlack = Math.Clamp(targetSlack, 1, 6);

            if (jitter > 5f) {
                targetSlack += 1;
            } else if (jitter > 10f) {
                targetSlack += 2;
            }

            if (packetLoss > 5f) {
                targetSlack += 3;
                NetworkSettings.inputRedundancy = 5;
            }
            else if (packetLoss > 1f) {
                targetSlack += 2;
                NetworkSettings.inputRedundancy = 3;
            }
            else {
                NetworkSettings.inputRedundancy = 1;
            }

            NetworkSettings.targetBufferSlack = Mathf.Clamp(
                Mathf.CeilToInt(targetSlack),
                1,
                6
            );
        }
    }

    public static void Reset() {
        lock (lockObj) {
            rttSamples.Clear();
            jitterSamples.Clear();
            jitterSortBuffer.Clear();
            lossHistory.Clear();
            lastRtt = -1f;
            expectedSequence = 0;
            initialized = false;
            ping = 0f;
            jitter = 0f;
            packetLoss = 0f;
            smoothedPacketLoss = 0f;
        }
    }

    private static float Average(Queue<float> queue) {
        float sum = 0f;
        foreach (float v in queue) sum += v;
        return sum / queue.Count;
    }
}