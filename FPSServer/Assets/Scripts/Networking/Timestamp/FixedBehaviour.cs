using UnityEngine;

public abstract class FixedBehaviour : MonoBehaviour
{
    protected virtual void OnEnable()
    {
        FixedClock.Register(this);
    }

    protected virtual void OnDisable()
    {
        FixedClock.Unregister(this);
    }

    public abstract void UpdateFixed();
}