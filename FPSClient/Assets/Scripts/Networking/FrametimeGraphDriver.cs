using UnityEngine;

/// <summary>
/// Attach this to any active GameObject to feed real frametime data into a FrametimeGraph.
/// Assign the graph reference in the inspector.
/// </summary>
public class FrametimeGraphDriver : MonoBehaviour
{
    [Header("Graph Reference")]
    public FrametimeGraph graph;

    [Header("Optional: stress-test spike simulation")]
    [Tooltip("Hold Space in Play Mode to inject a spike for testing.")]
    public bool enableSpikeTest = true;

    private void Update()
    {
        if (graph == null) return;

        float frametime = Time.unscaledDeltaTime * 1000f; // convert seconds → ms

        // Optional artificial spike for testing thresholds
        if (enableSpikeTest && Input.GetKey(KeyCode.Space))
            frametime += Random.Range(20f, 60f);

        graph.AddSample(frametime);
    }
}