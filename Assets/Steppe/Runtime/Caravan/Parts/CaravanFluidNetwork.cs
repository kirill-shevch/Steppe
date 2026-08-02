using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Steppe.Caravan
{
    public enum CaravanFluidPortRole
    {
        ReservoirSupply,
        PumpInlet,
        PumpOutlet,
        RadiatorInlet,
        RadiatorOutlet,
        ReservoirReturn,
        ThermalTap
    }

    public enum CaravanPumpMode
    {
        Off,
        Extraction,
        Circulation
    }

    [DisallowMultipleComponent]
    public sealed class CaravanFluidPort : MonoBehaviour
    {
        private readonly List<CaravanFluidPipe> pipes =
            new List<CaravanFluidPipe>(1);
        private GameObject buildMarker;
        private Renderer buildMarkerRenderer;
        private Light buildMarkerLight;
        private MaterialPropertyBlock buildMarkerProperties;
        private Vector3 buildMarkerBaseScale;
        private int maximumConnections = 1;

        public CaravanPart Part { get; private set; }
        public CaravanFluidPortRole Role { get; private set; }
        public string PortId { get; private set; }
        public int ConnectedPipeCount => pipes.Count;
        public int MaximumConnections => maximumConnections;
        public bool IsAtCapacity => ConnectedPipeCount >= maximumConnections;
        public bool IsBuildMarkerVisible =>
            buildMarker != null && buildMarker.activeSelf;

        public void Configure(
            CaravanPart part,
            CaravanFluidPortRole role,
            GameObject communicationBuildMarker = null,
            string portId = null,
            int connectionCapacity = 0)
        {
            Part = part != null
                ? part
                : throw new ArgumentNullException(nameof(part));
            Role = role;
            maximumConnections = connectionCapacity > 0
                ? connectionCapacity
                : role == CaravanFluidPortRole.ReservoirReturn
                    ? 5
                    : 1;
            var module = Part.GetComponent<CaravanModule>();
            PortId = string.IsNullOrWhiteSpace(portId)
                ? $"{module?.InstanceId ?? Part.name}:fluid:{role}"
                : portId;
            buildMarker = communicationBuildMarker;
            if (buildMarker != null)
            {
                buildMarkerRenderer = buildMarker.GetComponent<Renderer>();
                buildMarkerLight = buildMarker.GetComponent<Light>();
                buildMarkerProperties = new MaterialPropertyBlock();
                buildMarkerBaseScale = buildMarker.transform.localScale;
                buildMarker.SetActive(false);
            }
        }

        internal void Register(CaravanFluidPipe pipe)
        {
            if (pipe != null && !pipes.Contains(pipe))
            {
                pipes.Add(pipe);
            }
        }

        internal void Unregister(CaravanFluidPipe pipe)
        {
            if (pipe != null)
            {
                pipes.Remove(pipe);
            }
        }

        public void SetBuildPresentation(
            bool visible,
            bool selected,
            bool compatible,
            bool focused)
        {
            if (buildMarker == null)
            {
                return;
            }

            buildMarker.SetActive(visible);
            if (!visible)
            {
                return;
            }

            var color = selected
                ? new Color(1f, 0.68f, 0.08f, 1f)
                : !compatible
                    ? new Color(0.95f, 0.12f, 0.08f, 1f)
                    : focused
                        ? new Color(0.18f, 1f, 0.48f, 1f)
                        : new Color(0.1f, 0.82f, 0.92f, 1f);
            if (buildMarkerRenderer != null)
            {
                buildMarkerRenderer.GetPropertyBlock(buildMarkerProperties);
                buildMarkerProperties.SetColor("_BaseColor", color);
                buildMarkerProperties.SetColor("_Color", color);
                buildMarkerRenderer.SetPropertyBlock(buildMarkerProperties);
            }
            if (buildMarkerLight != null)
            {
                buildMarkerLight.color = color;
                buildMarkerLight.intensity = selected || focused ? 0.9f : 0.45f;
            }

            buildMarker.transform.localScale = buildMarkerBaseScale
                                               * (selected || focused ? 1.35f : 1f);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CaravanPart))]
    [RequireComponent(typeof(CaravanModule))]
    public sealed class CaravanWaterReservoirModule : MonoBehaviour
    {
        private CaravanPart part;
        private CaravanModule module;

        public float StoredWaterLitres => part != null ? part.StoredAmount : 0f;
        public float CapacityLitres => part != null ? part.Capacity : 0f;
        public float TemperatureCelsius =>
            part != null ? part.TemperatureCelsius : 18f;
        public float CurrentFlowLitresPerSecond { get; private set; }

        public void Configure()
        {
            part = GetComponent<CaravanPart>();
            module = GetComponent<CaravanModule>();
            if (part.Kind != CaravanPartKind.WaterReservoir)
            {
                throw new InvalidOperationException(
                    "CaravanWaterReservoirModule requires a WaterReservoir CaravanPart.");
            }

            RefreshState(0f, part.TemperatureCelsius);
        }

        public void SetStoredWater(float litres)
        {
            part.SetStoredAmount(litres);
            RefreshState(CurrentFlowLitresPerSecond, TemperatureCelsius);
        }

        public void SetTemperature(float temperatureCelsius)
        {
            part.SetTemperature(temperatureCelsius);
        }

        internal void ApplyFlow(CaravanWaterFlow flow)
        {
            RefreshState(flow.FlowLitresPerSecond, flow.TemperatureCelsius);
        }

        private void RefreshState(float flowLitresPerSecond, float temperatureCelsius)
        {
            CurrentFlowLitresPerSecond = Mathf.Max(0f, flowLitresPerSecond);
            part.SetTemperature(temperatureCelsius);
            part.SetCurrentOutput(CurrentFlowLitresPerSecond);
            module.SetPayloadMass(StoredWaterLitres);
            module.SetLoad(CapacityLitres > 0f
                ? StoredWaterLitres / CapacityLitres
                : 0f);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CaravanPart))]
    [RequireComponent(typeof(CaravanModule))]
    public sealed class CaravanElectricPumpModule :
        MonoBehaviour,
        ICaravanElectricalConsumer,
        ICaravanControlTarget
    {
        private CaravanPart part;
        private CaravanModule module;
        private bool circuitDemand;
        private bool extractionDemand;
        private float mechanicalPowerAvailability;

        public CaravanPart ElectricalPart => part;
        public float MaximumFlowLitresPerSecond =>
            part != null ? part.Capacity : 0f;
        public float MaximumElectricalPowerKilowatts { get; private set; } = 6f;
        public CaravanPumpMode Mode { get; private set; } =
            CaravanPumpMode.Circulation;
        public float ControlNormalized { get; private set; } = 1f;
        public float RequestedPowerKilowatts =>
            circuitDemand || extractionDemand
                ? MaximumElectricalPowerKilowatts
                  * (module != null ? module.State.Efficiency : 1f)
                  * (1f - mechanicalPowerAvailability)
                : 0f;
        public float DeliveredElectricalKilowatts { get; private set; }
        public float PowerAvailability { get; private set; }
        public float CurrentFlowLitresPerSecond { get; private set; }

        public void Configure(float maximumElectricalPowerKilowatts = 6f)
        {
            part = GetComponent<CaravanPart>();
            module = GetComponent<CaravanModule>();
            if (part.Kind != CaravanPartKind.DualModePump)
            {
                throw new InvalidOperationException(
                    "CaravanElectricPumpModule requires a DualModePump CaravanPart.");
            }

            MaximumElectricalPowerKilowatts = Mathf.Max(
                0.1f,
                maximumElectricalPowerKilowatts);
            SetMode(CaravanPumpMode.Circulation);
            SetCircuitDemand(false);
            SetExtractionDemand(false);
            ApplyDeliveredPower(0f);
            ApplyFlow(0f);
        }

        public void SetControlNormalized(float value)
        {
            SetMode(CaravanControlModel.ToPumpMode(value));
        }

        public void SetMode(CaravanPumpMode mode)
        {
            Mode = mode;
            ControlNormalized = CaravanControlModel.FromPumpMode(mode);
            if (mode != CaravanPumpMode.Circulation)
            {
                circuitDemand = false;
            }
            if (mode != CaravanPumpMode.Extraction)
            {
                extractionDemand = false;
            }
            if (!circuitDemand && !extractionDemand)
            {
                ApplyDeliveredPower(0f);
            }
        }

        public void SetCircuitDemand(bool active)
        {
            circuitDemand = active && Mode == CaravanPumpMode.Circulation;
            if (!circuitDemand && !extractionDemand)
            {
                ApplyDeliveredPower(0f);
            }
        }

        public void SetExtractionDemand(bool active)
        {
            extractionDemand = active && Mode == CaravanPumpMode.Extraction;
            if (!circuitDemand && !extractionDemand)
            {
                ApplyDeliveredPower(0f);
            }
        }

        public void SetMechanicalPowerAvailability(float availability)
        {
            mechanicalPowerAvailability = Mathf.Clamp01(availability);
            RefreshPowerAvailability();
        }

        public void ApplyDeliveredPower(float electricalKilowatts)
        {
            DeliveredElectricalKilowatts = Mathf.Clamp(
                electricalKilowatts,
                0f,
                RequestedPowerKilowatts);
            RefreshPowerAvailability();
            part.SetCurrentOutput(CurrentFlowLitresPerSecond);
            RefreshLoad();
        }

        internal void ApplyFlow(float litresPerSecond)
        {
            CurrentFlowLitresPerSecond = Mathf.Clamp(
                litresPerSecond,
                0f,
                MaximumFlowLitresPerSecond);
            part.SetCurrentOutput(CurrentFlowLitresPerSecond);
            RefreshLoad();
        }

        private void RefreshLoad()
        {
            var electricalLoad = MaximumElectricalPowerKilowatts > 0f
                ? DeliveredElectricalKilowatts / MaximumElectricalPowerKilowatts
                : 0f;
            var hydraulicLoad = MaximumFlowLitresPerSecond > 0f
                ? CurrentFlowLitresPerSecond / MaximumFlowLitresPerSecond
                : 0f;
            module.SetLoad(Mathf.Max(electricalLoad, hydraulicLoad));
        }

        private void RefreshPowerAvailability()
        {
            var electricalAvailability = RequestedPowerKilowatts > 0.001f
                ? DeliveredElectricalKilowatts / RequestedPowerKilowatts
                : 0f;
            PowerAvailability = Mathf.Clamp01(
                Mathf.Max(
                    mechanicalPowerAvailability,
                    electricalAvailability));
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CaravanPart))]
    [RequireComponent(typeof(CaravanModule))]
    public sealed class CaravanRadiatorModule :
        MonoBehaviour,
        ICaravanControlTarget
    {
        private CaravanPart part;
        private CaravanModule module;

        public float MaximumCoolingKilowatts =>
            part != null ? part.Capacity : 0f;
        public float CurrentCoolingKilowatts { get; private set; }
        public float CurrentFlowLitresPerSecond { get; private set; }
        public float ControlNormalized { get; private set; } = 1f;
        public float Opening => (ControlNormalized + 1f) * 0.5f;
        public float Efficiency =>
            module != null ? module.State.Efficiency : 1f;

        public void Configure()
        {
            part = GetComponent<CaravanPart>();
            module = GetComponent<CaravanModule>();
            if (part.Kind != CaravanPartKind.Radiator)
            {
                throw new InvalidOperationException(
                    "CaravanRadiatorModule requires a Radiator CaravanPart.");
            }

            ApplyFlow(default);
        }

        public void SetControlNormalized(float value)
        {
            ControlNormalized = CaravanControlModel.Clamp(value);
        }

        internal void ApplyFlow(CaravanWaterFlow flow)
        {
            CurrentCoolingKilowatts =
                Mathf.Max(0f, flow.CoolingKilowatts);
            CurrentFlowLitresPerSecond = Mathf.Max(
                0f,
                flow.FlowLitresPerSecond);
            part.SetCurrentOutput(CurrentCoolingKilowatts);
            module.SetLoad(MaximumCoolingKilowatts > 0f
                ? CurrentCoolingKilowatts / MaximumCoolingKilowatts
                : 0f);
        }
    }

    [DisallowMultipleComponent]
    public sealed class CaravanFluidPipe : MonoBehaviour
    {
        private CaravanFluidPort start;
        private CaravanFluidPort end;
        private Transform routingRoot;
        private LineRenderer line;
        private float flowLitresPerSecond;

        public CaravanFluidPort Start => start;
        public CaravanFluidPort End => end;
        public float FlowLitresPerSecond => flowLitresPerSecond;
        public bool IsConductive => isActiveAndEnabled
                                    && start != null
                                    && end != null
                                    && start.isActiveAndEnabled
                                    && end.isActiveAndEnabled;

        public void Configure(
            CaravanFluidPort startPort,
            CaravanFluidPort endPort,
            Transform circuitRoot,
            Material material)
        {
            start = startPort != null
                ? startPort
                : throw new ArgumentNullException(nameof(startPort));
            end = endPort != null
                ? endPort
                : throw new ArgumentNullException(nameof(endPort));
            routingRoot = circuitRoot != null
                ? circuitRoot
                : throw new ArgumentNullException(nameof(circuitRoot));
            line = gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 4;
            line.widthMultiplier = 0.085f;
            line.numCapVertices = 5;
            line.numCornerVertices = 4;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            start.Register(this);
            end.Register(this);
            RefreshRoute();
        }

        public void SetFlow(float litresPerSecond)
        {
            flowLitresPerSecond = Mathf.Max(0f, litresPerSecond);
            if (line != null)
            {
                line.widthMultiplier = Mathf.Lerp(
                    0.075f,
                    0.105f,
                    Mathf.Clamp01(flowLitresPerSecond / 24f));
            }
        }

        private void LateUpdate()
        {
            RefreshRoute();
        }

        private void RefreshRoute()
        {
            if (line == null)
            {
                return;
            }

            line.enabled = IsConductive;
            if (!line.enabled)
            {
                return;
            }

            var startLocal = routingRoot.InverseTransformPoint(start.transform.position);
            var endLocal = routingRoot.InverseTransformPoint(end.transform.position);
            const float routeY = 0.12f;
            line.SetPosition(0, start.transform.position);
            line.SetPosition(
                1,
                routingRoot.TransformPoint(
                    new Vector3(startLocal.x, routeY, startLocal.z)));
            line.SetPosition(
                2,
                routingRoot.TransformPoint(
                    new Vector3(endLocal.x, routeY, endLocal.z)));
            line.SetPosition(3, end.transform.position);
        }

        private void OnDestroy()
        {
            start?.Unregister(this);
            end?.Unregister(this);
        }
    }

    [DefaultExecutionOrder(60)]
    [DisallowMultipleComponent]
    public sealed class CaravanFluidNetwork : MonoBehaviour
    {
        private readonly List<CaravanFluidPort> ports =
            new List<CaravanFluidPort>(12);
        private readonly List<CaravanFluidPipe> pipes =
            new List<CaravanFluidPipe>(12);
        private Material pipeMaterial;
        private CaravanEnvironmentSampler environment;
        private CaravanWaterReservoirModule reservoir;
        private CaravanElectricPumpModule pump;
        private CaravanRadiatorModule radiator;
        private CaravanFluidPort reservoirSupply;
        private CaravanFluidPort reservoirReturn;
        private CaravanFluidPort pumpInlet;
        private CaravanFluidPort pumpOutlet;
        private CaravanFluidPort radiatorInlet;
        private CaravanFluidPort radiatorOutlet;
        private float externalHeatingKilowatts;

        public IReadOnlyList<CaravanFluidPort> Ports => ports;
        public IReadOnlyList<CaravanFluidPipe> Pipes => pipes;
        public CaravanWaterReservoirModule Reservoir => reservoir;
        public CaravanElectricPumpModule Pump => pump;
        public CaravanRadiatorModule Radiator => radiator;
        public CaravanFluidPort ReservoirSupplyPort => reservoirSupply;
        public CaravanFluidPort ReservoirReturnPort => reservoirReturn;
        public CaravanFluidPort PumpInletPort => pumpInlet;
        public CaravanFluidPort PumpOutletPort => pumpOutlet;
        public CaravanFluidPort RadiatorInletPort => radiatorInlet;
        public CaravanFluidPort RadiatorOutletPort => radiatorOutlet;
        public int PipeCount => pipes.Count;
        public float CurrentFlowLitresPerSecond { get; private set; }
        public float CurrentCoolingKilowatts { get; private set; }
        public float CurrentHeatingKilowatts { get; private set; }
        public float LiquidFraction { get; private set; } = 1f;
        public bool IsClosed =>
            IsConnected(reservoirSupply, pumpInlet)
            && IsConnected(pumpOutlet, radiatorInlet)
            && IsConnected(radiatorOutlet, reservoirReturn);
        public event Action ConnectionsChanged;

        public void Configure(Material material)
        {
            pipeMaterial = material != null
                ? material
                : throw new ArgumentNullException(nameof(material));
        }

        public void SetEnvironment(CaravanEnvironmentSampler sampler)
        {
            environment = sampler;
        }

        public void SetExternalHeatingKilowatts(float heatingKilowatts)
        {
            externalHeatingKilowatts = Mathf.Max(0f, heatingKilowatts);
        }

        public int RegisterModule(CaravanModule module)
        {
            if (module == null)
            {
                return 0;
            }

            var modulePorts = module.GetComponentsInChildren<CaravanFluidPort>(true);
            var added = 0;
            for (var index = 0; index < modulePorts.Length; index++)
            {
                if (RegisterPort(modulePorts[index]))
                {
                    added++;
                }
            }

            return added;
        }

        public bool RegisterPort(CaravanFluidPort port)
        {
            if (port == null || port.Part == null || ports.Contains(port))
            {
                return false;
            }

            ports.Add(port);
            RefreshPrimaryComponents();
            return true;
        }

        public bool UnregisterPort(CaravanFluidPort port)
        {
            if (port == null || !ports.Contains(port))
            {
                return false;
            }

            DisconnectPort(port);
            ports.Remove(port);
            RefreshPrimaryComponents();
            return true;
        }

        public bool CanConnect(CaravanFluidPort first, CaravanFluidPort second)
        {
            if (first == null
                || second == null
                || first == second
                || !ports.Contains(first)
                || !ports.Contains(second)
                || first.IsAtCapacity
                || second.IsAtCapacity
                || FindPipe(first, second) != null)
            {
                return false;
            }

            return CaravanConnectionRules.AreCompatible(
                first.Role,
                second.Role);
        }

        public bool TryConnect(CaravanFluidPort first, CaravanFluidPort second)
        {
            if (!CanConnect(first, second))
            {
                return false;
            }

            var pipeObject = new GameObject(
                $"{first.Role} to {second.Role} Pipe");
            pipeObject.transform.SetParent(transform, false);
            var pipe = pipeObject.AddComponent<CaravanFluidPipe>();
            pipe.Configure(first, second, transform, pipeMaterial);
            pipes.Add(pipe);
            ConnectionsChanged?.Invoke();
            return true;
        }

        public int DisconnectPort(CaravanFluidPort port)
        {
            if (port == null || !ports.Contains(port))
            {
                return 0;
            }

            var removed = 0;
            for (var index = pipes.Count - 1; index >= 0; index--)
            {
                var pipe = pipes[index];
                if (pipe != null && (pipe.Start == port || pipe.End == port))
                {
                    RemovePipe(pipe);
                    removed++;
                }
            }

            return removed;
        }

        public bool RemovePipe(CaravanFluidPipe pipe)
        {
            if (pipe == null || !pipes.Remove(pipe))
            {
                return false;
            }

            pipe.Start?.Unregister(pipe);
            pipe.End?.Unregister(pipe);
            pipe.SetFlow(0f);
            Destroy(pipe.gameObject);
            ConnectionsChanged?.Invoke();
            return true;
        }

        public void ClearConnections()
        {
            for (var index = pipes.Count - 1; index >= 0; index--)
            {
                RemovePipe(pipes[index]);
            }
        }

        public void Simulate(float deltaTimeSeconds)
        {
            if (reservoir == null || pump == null || radiator == null)
            {
                CurrentFlowLitresPerSecond = 0f;
                CurrentCoolingKilowatts = 0f;
                CurrentHeatingKilowatts = 0f;
                pump?.SetCircuitDemand(false);
                pump?.ApplyFlow(0f);
                radiator?.ApplyFlow(default);
                if (reservoir != null)
                {
                    reservoir.ApplyFlow(
                        new CaravanWaterFlow(
                            0f,
                            0f,
                            reservoir.TemperatureCelsius));
                }
                for (var index = 0; index < pipes.Count; index++)
                {
                    pipes[index]?.SetFlow(0f);
                }
                return;
            }

            LiquidFraction = Mathf.InverseLerp(
                -4f,
                2f,
                reservoir.TemperatureCelsius);
            var closed = IsClosed
                         && reservoir.StoredWaterLitres > 0.001f
                         && LiquidFraction > 0.01f;
            pump.SetCircuitDemand(closed);
            var ambientTemperature = 18f;
            var windSpeed = 0f;
            if (environment != null)
            {
                ambientTemperature = (float)environment.SampleAirTemperature(
                    transform.position);
                windSpeed = environment.SampleWeather(transform.position)
                    .SurfaceWind.magnitude;
            }

            var flow = CaravanWaterCircuitModel.Evaluate(
                reservoir.StoredWaterLitres,
                reservoir.TemperatureCelsius,
                ambientTemperature,
                pump.MaximumFlowLitresPerSecond * LiquidFraction,
                pump.PowerAvailability,
                radiator.MaximumCoolingKilowatts
                * radiator.Opening
                * radiator.Efficiency,
                windSpeed,
                deltaTimeSeconds,
                closed);
            CurrentFlowLitresPerSecond = flow.FlowLitresPerSecond;
            CurrentCoolingKilowatts = flow.CoolingKilowatts;
            CurrentHeatingKilowatts = closed
                ? externalHeatingKilowatts
                : 0f;
            if (CurrentHeatingKilowatts > 0f
                && reservoir.StoredWaterLitres > 0.001f)
            {
                var heatedTemperature =
                    flow.TemperatureCelsius
                    + CurrentHeatingKilowatts
                    * Mathf.Max(0f, deltaTimeSeconds)
                    / (reservoir.StoredWaterLitres * 4.186f);
                flow = new CaravanWaterFlow(
                    flow.FlowLitresPerSecond,
                    flow.CoolingKilowatts,
                    Mathf.Min(140f, heatedTemperature));
            }
            reservoir.ApplyFlow(flow);
            pump.ApplyFlow(flow.FlowLitresPerSecond);
            radiator.ApplyFlow(flow);
            for (var index = 0; index < pipes.Count; index++)
            {
                pipes[index]?.SetFlow(flow.FlowLitresPerSecond);
            }
        }

        private bool IsConnected(CaravanFluidPort first, CaravanFluidPort second)
        {
            var pipe = FindPipe(first, second);
            return pipe != null && pipe.IsConductive;
        }

        public bool IsPartConnected(CaravanPart part)
        {
            if (part == null)
            {
                return false;
            }

            for (var index = 0; index < pipes.Count; index++)
            {
                var pipe = pipes[index];
                if (pipe != null
                    && pipe.IsConductive
                    && (pipe.Start.Part == part || pipe.End.Part == part))
                {
                    return true;
                }
            }

            return false;
        }

        private CaravanFluidPipe FindPipe(
            CaravanFluidPort first,
            CaravanFluidPort second)
        {
            if (first == null || second == null)
            {
                return null;
            }

            for (var index = 0; index < pipes.Count; index++)
            {
                var pipe = pipes[index];
                if (pipe != null
                    && ((pipe.Start == first && pipe.End == second)
                        || (pipe.Start == second && pipe.End == first)))
                {
                    return pipe;
                }
            }

            return null;
        }

        private void RefreshPrimaryComponents()
        {
            reservoir = null;
            pump = null;
            radiator = null;
            reservoirSupply = null;
            reservoirReturn = null;
            pumpInlet = null;
            pumpOutlet = null;
            radiatorInlet = null;
            radiatorOutlet = null;
            for (var index = 0; index < ports.Count; index++)
            {
                var port = ports[index];
                if (port == null || port.Part == null)
                {
                    continue;
                }

                reservoir ??= port.Part.GetComponent<CaravanWaterReservoirModule>();
                pump ??= port.Part.GetComponent<CaravanElectricPumpModule>();
                radiator ??= port.Part.GetComponent<CaravanRadiatorModule>();
                switch (port.Role)
                {
                    case CaravanFluidPortRole.ReservoirSupply:
                        reservoirSupply ??= port;
                        break;
                    case CaravanFluidPortRole.ReservoirReturn:
                        reservoirReturn ??= port;
                        break;
                    case CaravanFluidPortRole.PumpInlet:
                        pumpInlet ??= port;
                        break;
                    case CaravanFluidPortRole.PumpOutlet:
                        pumpOutlet ??= port;
                        break;
                    case CaravanFluidPortRole.RadiatorInlet:
                        radiatorInlet ??= port;
                        break;
                    case CaravanFluidPortRole.RadiatorOutlet:
                        radiatorOutlet ??= port;
                        break;
                }
            }
        }

        private void FixedUpdate()
        {
            Simulate(UnityEngine.Time.fixedDeltaTime);
        }
    }
}
