using UnityEngine;

namespace Steppe.Player
{
    /// <summary>
    /// Shared contract for the temporary rolling-ball prototype and the caravan demo.
    /// World streaming and diagnostics follow this contract instead of knowing which
    /// physical traveller currently owns the camera.
    /// </summary>
    public interface ISteppeTravelFocus
    {
        Transform FocusTransform { get; }
        bool IsGrounded { get; }
        float Speed { get; }
        float TrackRadius { get; }
        SteppeTraversalState CurrentSurface { get; }
        void Teleport(Vector3 localPosition);
    }
}
