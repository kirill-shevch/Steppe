namespace Steppe.Caravan
{
    public interface ICaravanControlTarget
    {
        float ControlNormalized { get; }
        void SetControlNormalized(float value);
    }

    public interface ICaravanDriveSource
    {
        float RequestedMechanicalKilowatts { get; }
        float DeliveredMechanicalKilowatts { get; }
        void SetRequestedThrottle(float normalizedThrottle);
    }
}
