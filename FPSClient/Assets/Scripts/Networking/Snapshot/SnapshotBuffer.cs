using System.Collections.Generic;

public class SnapshotBuffer {
    private readonly List<WorldSnapshot> buffer = new();

    public int Count => buffer.Count;
    public WorldSnapshot this[int i] => buffer[i];
    public WorldSnapshot Newest => buffer[buffer.Count - 1];

    public void Clear() => buffer.Clear();

    public void Add(WorldSnapshot snapshot) {
        if (buffer.Count == 0) {
            buffer.Add(snapshot);
            return;
        }

        if (snapshot.serverTick <= buffer[0].serverTick) return;

        if (snapshot.serverTick > Newest.serverTick) {
            buffer.Add(snapshot);
            return;
        }

        for (int i = 0; i < buffer.Count; i++) {
            if (snapshot.serverTick == buffer[i].serverTick) return;
            if (snapshot.serverTick < buffer[i].serverTick) {
                buffer.Insert(i, snapshot);
                return;
            }
        }
    }

    public void TrimBefore(float renderTick) {
        while (buffer.Count > 2 && buffer[1].serverTick < renderTick)
            buffer.RemoveAt(0);
    }

    public bool TryGetBounds(float renderTick, out WorldSnapshot from, out WorldSnapshot to) {
        for (int i = 0; i < buffer.Count - 1; i++) {
            if (renderTick >= buffer[i].serverTick && renderTick <= buffer[i + 1].serverTick) {
                from = buffer[i];
                to = buffer[i + 1];
                return true;
            }
        }
        
        if (renderTick < buffer[0].serverTick) {
            from = to = buffer[0];
        }
        else {
            from = buffer[buffer.Count - 2];
            to = buffer[buffer.Count - 1];
        }

        return false;
    }
}