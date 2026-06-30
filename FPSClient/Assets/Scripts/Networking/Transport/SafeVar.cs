using System.Threading;

public class SafeFlag {
    private volatile bool value;

    public bool IsSet() {
        return value;
    }

    public void Set() {
        value = true;
    }

    public void Clear() {
        value = false;
    }
}

public class SafeLatch {
    private int value;

    public bool IsSet() {
        return Interlocked.CompareExchange(ref value, 1, 1) == 1;
    }

    public bool TrySet() {
        return Interlocked.CompareExchange(ref value, 1, 0) == 0;
    }

    public void Reset() {
        Interlocked.Exchange(ref value, 0);
    }
}

public class SafeLong {
    private long value;

    public long Get() {
        return Interlocked.Read(ref value);
    }

    public void Set(long newValue) {
        Interlocked.Exchange(ref value, newValue);
    }
}

public class SendGate {
    private int sending;

    public bool TryEnter() {
        return Interlocked.CompareExchange(ref sending, 1, 0) == 0;
    }

    public void Exit() {
        Interlocked.Exchange(ref sending, 0);
    }
}