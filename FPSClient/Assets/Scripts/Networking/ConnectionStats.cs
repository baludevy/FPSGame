using System.Collections.Generic;
using UnityEngine;

public static class ConnectionStats {
    private static Queue<float> rttSamples = new Queue<float>();
    private static Queue<float> jitterSamples = new Queue<float>();
    private static List<bool> lossHistory = new List<bool>();
    private static readonly object lockObj = new object();

    private static int rttSampleCount = 20;
    private static int jitterSampleCount = 20;
    private static int lossHistorySize = 100;

    private static float lastRtt = -1f;
    private static uint expectedSequence;
    private static bool initialized;

    public static float ping;
    public static float jitter;
    public static float packetLoss;

    private static List<float> jitterSortBuffer = new List<float>(20);

    private static float currentSlackAccumulator = 1f;
    private const float slackGrowSpeed = 1.0f;
    private const float slackDecaySpeed = 0.05f;

    public static void CalculateStats(WorldSnapshot snapshot) {
        lock (lockObj) {
            double clientReceiveTime = TickTimer.Instance.GetTime();

            //ping
            double serverProcessingTime = snapshot.serverSendTime - snapshot.serverReceiveTime;
            float rtt = Mathf.Max(0,
                (float)((clientReceiveTime - snapshot.clientSendTime - serverProcessingTime -
                         NetworkSettings.tickTime) * 1000f));

            rttSamples.Enqueue(rtt);
            if (rttSamples.Count > rttSampleCount) rttSamples.Dequeue();
            ping = Average(rttSamples);

            //jitter
            if (lastRtt >= 0f) {
                jitterSamples.Enqueue(Mathf.Abs(rtt - lastRtt));
                if (jitterSamples.Count > jitterSampleCount) jitterSamples.Dequeue();
                jitter = Percentile95(jitterSamples);
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
            packetLoss = lossHistory.Count > 0 ? (lost / (float)lossHistory.Count) * 100f : 0f;

            float tickTimeMs = NetworkSettings.tickTime * 1000f;
            float halfTickTimeMs = tickTimeMs / 2f;

            float rawTargetSlack = Mathf.Max(1f, Mathf.Ceil(jitter / halfTickTimeMs));

            if (rawTargetSlack > currentSlackAccumulator) {
                currentSlackAccumulator = rawTargetSlack;
            }
            else {
                currentSlackAccumulator = Mathf.MoveTowards(currentSlackAccumulator, rawTargetSlack, slackDecaySpeed);
            }

            NetworkSettings.targetBufferSlack = Mathf.CeilToInt(currentSlackAccumulator);

            if (packetLoss > 5f) {
                NetworkSettings.inputRedundancy = 3;
            }
            else if (packetLoss > 1f) {
                NetworkSettings.inputRedundancy = 2;
            }
            else {
                NetworkSettings.inputRedundancy = 1;
            }
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
            currentSlackAccumulator = 1f;
        }
    }

    private static float Average(Queue<float> queue) {
        float sum = 0f;
        foreach (float v in queue) sum += v;
        return sum / queue.Count;
    }

    private static float Percentile95(Queue<float> queue) {
        if (queue.Count == 0) return 0f;

        jitterSortBuffer.Clear();
        foreach (float v in queue) {
            jitterSortBuffer.Add(v);
        }

        jitterSortBuffer.Sort();

        int index = Mathf.CeilToInt(jitterSortBuffer.Count * 0.95f) - 1;
        index = Mathf.Clamp(index, 0, jitterSortBuffer.Count - 1);

        return jitterSortBuffer[index];
    }
}