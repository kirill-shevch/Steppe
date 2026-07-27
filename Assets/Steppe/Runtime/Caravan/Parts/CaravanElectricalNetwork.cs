using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Steppe.Caravan
{
    public enum CaravanElectricalPortKind
    {
        Generator,
        Storage,
        Consumer
    }

    public interface ICaravanElectricalGenerator
    {
        CaravanPart ElectricalPart { get; }
        float AvailableGenerationKilowatts { get; }
    }

    public interface ICaravanElectricalStorage
    {
        CaravanPart ElectricalPart { get; }
        void BeginPowerStep();
        float AcceptCharge(float availableInputKilowatts, float deltaTimeHours);
        float SupplyPower(float requestedOutputKilowatts, float deltaTimeHours);
        void CompletePowerStep();
    }

    public interface ICaravanElectricalConsumer
    {
        CaravanPart ElectricalPart { get; }
        float RequestedPowerKilowatts { get; }
        float DeliveredElectricalKilowatts { get; }
        void ApplyDeliveredPower(float electricalKilowatts);
    }

    [DisallowMultipleComponent]
    public sealed class CaravanElectricalPort : MonoBehaviour
    {
        private readonly List<CaravanElectricalCable> cables =
            new List<CaravanElectricalCable>(2);
        private GameObject buildMarker;
        private Renderer buildMarkerRenderer;
        private Light buildMarkerLight;
        private MaterialPropertyBlock buildMarkerProperties;
        private Vector3 buildMarkerBaseScale;
        private int maximumConnections = 1;

        public CaravanElectricalPortKind Kind { get; private set; }
        public CaravanPart Part { get; private set; }
        public string PortId { get; private set; }
        public ICaravanElectricalGenerator Generator { get; private set; }
        public ICaravanElectricalStorage Storage { get; private set; }
        public ICaravanElectricalConsumer Consumer { get; private set; }
        public int ConnectedCableCount => cables.Count;
        public int MaximumConnections => maximumConnections;
        public bool IsAtCapacity => ConnectedCableCount >= MaximumConnections;
        public bool IsBuildMarkerVisible => buildMarker != null && buildMarker.activeSelf;

        public void Configure(
            CaravanPart part,
            CaravanElectricalPortKind kind,
            GameObject communicationBuildMarker = null,
            int connectionCapacity = 0,
            string portId = null)
        {
            Part = part != null ? part : throw new ArgumentNullException(nameof(part));
            Kind = kind;
            maximumConnections = connectionCapacity > 0
                ? connectionCapacity
                : Kind == CaravanElectricalPortKind.Storage
                    ? 2
                    : 1;
            var module = Part.GetComponent<CaravanModule>();
            PortId = string.IsNullOrWhiteSpace(portId)
                ? $"{module?.InstanceId ?? Part.name}:electrical"
                : portId;
            Generator = null;
            Storage = null;
            Consumer = null;
            var behaviours = Part.GetComponents<MonoBehaviour>();
            for (var index = 0; index < behaviours.Length; index++)
            {
                Generator ??= behaviours[index] as ICaravanElectricalGenerator;
                Storage ??= behaviours[index] as ICaravanElectricalStorage;
                Consumer ??= behaviours[index] as ICaravanElectricalConsumer;
            }
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

        internal void Register(CaravanElectricalCable cable)
        {
            if (cable != null && !cables.Contains(cable))
            {
                cables.Add(cable);
            }
        }

        internal void Unregister(CaravanElectricalCable cable)
        {
            if (cable != null)
            {
                cables.Remove(cable);
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
                        : new Color(0.12f, 0.65f, 1f, 1f);
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
    public sealed class CaravanBatteryModule : MonoBehaviour, ICaravanElectricalStorage
    {
        private CaravanPart part;
        private CaravanModule module;
        private Transform chargeWindow;
        private Vector3 chargeWindowBaseScale;
        private Vector3 chargeWindowBasePosition;

        public float StoredEnergyKilowattHours => part != null ? part.StoredAmount : 0f;
        public CaravanPart ElectricalPart => part;
        public float CapacityKilowattHours => part != null ? part.Capacity : 0f;
        public float StateOfCharge => CapacityKilowattHours > 0f
            ? StoredEnergyKilowattHours / CapacityKilowattHours
            : 0f;
        public float MaximumChargeKilowatts { get; private set; } = 36f;
        public float MaximumDischargeKilowatts { get; private set; } = 65f;
        public float ChargeEfficiency { get; private set; } = 0.94f;
        public float DischargeEfficiency { get; private set; } = 0.93f;
        public float CurrentChargeKilowatts { get; private set; }
        public float CurrentDischargeKilowatts { get; private set; }

        public void Configure(
            Transform window,
            float maximumChargeKilowatts = 36f,
            float maximumDischargeKilowatts = 65f,
            float chargeEfficiency = 0.94f,
            float dischargeEfficiency = 0.93f)
        {
            part = GetComponent<CaravanPart>();
            module = GetComponent<CaravanModule>();
            if (part.Kind != CaravanPartKind.Battery)
            {
                throw new InvalidOperationException(
                    "CaravanBatteryModule requires a Battery CaravanPart.");
            }

            chargeWindow = window;
            if (chargeWindow != null)
            {
                chargeWindowBaseScale = chargeWindow.localScale;
                chargeWindowBasePosition = chargeWindow.localPosition;
            }

            MaximumChargeKilowatts = Mathf.Max(0f, maximumChargeKilowatts);
            MaximumDischargeKilowatts = Mathf.Max(0f, maximumDischargeKilowatts);
            ChargeEfficiency = Mathf.Clamp(chargeEfficiency, 0.01f, 1f);
            DischargeEfficiency = Mathf.Clamp(dischargeEfficiency, 0.01f, 1f);
            RefreshPresentation();
        }

        internal void ApplyFlow(CaravanElectricalFlow flow)
        {
            BeginPowerStep();
            part.SetStoredAmount(flow.StoredKilowattHours);
            CurrentChargeKilowatts = flow.BatteryChargeKilowatts;
            CurrentDischargeKilowatts = flow.BatteryDischargeKilowatts;
            CompletePowerStep();
        }

        public void BeginPowerStep()
        {
            CurrentChargeKilowatts = 0f;
            CurrentDischargeKilowatts = 0f;
        }

        public float AcceptCharge(float availableInputKilowatts, float deltaTimeHours)
        {
            var available = Mathf.Max(0f, availableInputKilowatts);
            var hours = Mathf.Max(0f, deltaTimeHours);
            if (available <= 0f || hours <= 0f || CapacityKilowattHours <= 0f)
            {
                return 0f;
            }

            var availableCapacityInput =
                (CapacityKilowattHours - StoredEnergyKilowattHours)
                / (ChargeEfficiency * hours);
            var accepted = Mathf.Min(
                available,
                MaximumChargeKilowatts,
                Mathf.Max(0f, availableCapacityInput));
            part.SetStoredAmount(
                StoredEnergyKilowattHours + accepted * ChargeEfficiency * hours);
            CurrentChargeKilowatts += accepted;
            return accepted;
        }

        public float SupplyPower(float requestedOutputKilowatts, float deltaTimeHours)
        {
            var request = Mathf.Max(0f, requestedOutputKilowatts);
            var hours = Mathf.Max(0f, deltaTimeHours);
            if (request <= 0f || hours <= 0f || StoredEnergyKilowattHours <= 0f)
            {
                return 0f;
            }

            var storedOutputLimit =
                StoredEnergyKilowattHours * DischargeEfficiency / hours;
            var supplied = Mathf.Min(
                request,
                MaximumDischargeKilowatts,
                storedOutputLimit);
            part.SetStoredAmount(
                StoredEnergyKilowattHours - supplied / DischargeEfficiency * hours);
            CurrentDischargeKilowatts += supplied;
            return supplied;
        }

        public void CompletePowerStep()
        {
            part.SetCurrentOutput(CurrentDischargeKilowatts);
            var load = Mathf.Max(
                MaximumChargeKilowatts > 0f
                    ? CurrentChargeKilowatts / MaximumChargeKilowatts
                    : 0f,
                MaximumDischargeKilowatts > 0f
                    ? CurrentDischargeKilowatts / MaximumDischargeKilowatts
                    : 0f);
            module.SetLoad(load);
            RefreshPresentation();
        }

        private void RefreshPresentation()
        {
            if (chargeWindow == null)
            {
                return;
            }

            var fill = Mathf.Max(0.035f, StateOfCharge);
            var scale = chargeWindowBaseScale;
            scale.x *= fill;
            chargeWindow.localScale = scale;
            var position = chargeWindowBasePosition;
            position.x -= chargeWindowBaseScale.x * (1f - fill) * 0.5f;
            chargeWindow.localPosition = position;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CaravanPart))]
    [RequireComponent(typeof(CaravanModule))]
    public sealed class CaravanElectricMotorModule :
        MonoBehaviour,
        ICaravanElectricalConsumer,
        ICaravanDriveSource
    {
        private CaravanPart part;
        private CaravanModule module;
        private Transform shaft;
        private float shaftAngle;

        public float MaximumPowerKilowatts => part != null ? part.Capacity : 0f;
        public CaravanPart ElectricalPart => part;
        public float RequestedThrottle { get; private set; }
        public float RequestedPowerKilowatts =>
            MaximumPowerKilowatts * RequestedThrottle * (module != null ? module.State.Efficiency : 1f);
        public float RequestedMechanicalKilowatts =>
            RequestedPowerKilowatts * MechanicalEfficiency;
        public float DeliveredElectricalKilowatts { get; private set; }
        public float DeliveredMechanicalKilowatts { get; private set; }
        public float MechanicalEfficiency { get; private set; } = 0.9f;
        public float PowerAvailability { get; private set; }

        public void Configure(Transform motorShaft, float mechanicalEfficiency = 0.9f)
        {
            part = GetComponent<CaravanPart>();
            module = GetComponent<CaravanModule>();
            if (part.Kind != CaravanPartKind.ElectricMotor)
            {
                throw new InvalidOperationException(
                    "CaravanElectricMotorModule requires an ElectricMotor CaravanPart.");
            }

            shaft = motorShaft;
            MechanicalEfficiency = Mathf.Clamp(mechanicalEfficiency, 0.01f, 1f);
            SetRequestedThrottle(0f);
            ApplyDeliveredPower(0f);
        }

        public void SetRequestedThrottle(float normalizedThrottle)
        {
            RequestedThrottle = Mathf.Clamp01(normalizedThrottle);
        }

        public void ApplyDeliveredPower(float electricalKilowatts)
        {
            DeliveredElectricalKilowatts = Mathf.Clamp(
                electricalKilowatts,
                0f,
                RequestedPowerKilowatts);
            DeliveredMechanicalKilowatts =
                DeliveredElectricalKilowatts * MechanicalEfficiency;
            PowerAvailability = RequestedPowerKilowatts > 0.001f
                ? Mathf.Clamp01(DeliveredElectricalKilowatts / RequestedPowerKilowatts)
                : 0f;
            part.SetCurrentOutput(DeliveredMechanicalKilowatts);
            module.SetLoad(MaximumPowerKilowatts > 0f
                ? DeliveredElectricalKilowatts / MaximumPowerKilowatts
                : 0f);
        }

        private void Update()
        {
            if (shaft == null || DeliveredMechanicalKilowatts <= 0.001f)
            {
                return;
            }

            shaftAngle = Mathf.Repeat(
                shaftAngle + DeliveredMechanicalKilowatts * UnityEngine.Time.deltaTime * 28f,
                360f);
            shaft.localRotation = Quaternion.Euler(0f, 0f, 90f)
                                  * Quaternion.Euler(0f, shaftAngle, 0f);
        }
    }

    [DisallowMultipleComponent]
    public sealed class CaravanElectricalCable : MonoBehaviour
    {
        private CaravanElectricalPort start;
        private CaravanElectricalPort end;
        private Transform circuitRoot;
        private LineRenderer positiveConductor;
        private LineRenderer returnConductor;
        private float currentKilowatts;

        public CaravanElectricalPort Start => start;
        public CaravanElectricalPort End => end;
        public float CurrentKilowatts => currentKilowatts;
        public bool IsConductive => isActiveAndEnabled
                                    && start != null
                                    && end != null
                                    && start.isActiveAndEnabled
                                    && end.isActiveAndEnabled;

        public void Configure(
            CaravanElectricalPort startPort,
            CaravanElectricalPort endPort,
            Transform routingRoot,
            Material positiveMaterial,
            Material returnMaterial)
        {
            start = startPort != null
                ? startPort
                : throw new ArgumentNullException(nameof(startPort));
            end = endPort != null ? endPort : throw new ArgumentNullException(nameof(endPort));
            circuitRoot = routingRoot != null
                ? routingRoot
                : throw new ArgumentNullException(nameof(routingRoot));
            positiveConductor = CreateConductor("Positive Conductor", positiveMaterial, 0.065f);
            returnConductor = CreateConductor("Return Conductor", returnMaterial, 0.055f);
            start.Register(this);
            end.Register(this);
            RefreshRoute();
        }

        public void SetCurrent(float kilowatts)
        {
            currentKilowatts = Mathf.Max(0f, kilowatts);
            if (positiveConductor != null)
            {
                positiveConductor.widthMultiplier =
                    Mathf.Lerp(0.055f, 0.085f, Mathf.Clamp01(currentKilowatts / 55f));
            }
        }

        private LineRenderer CreateConductor(string conductorName, Material material, float width)
        {
            var conductor = new GameObject(conductorName);
            conductor.transform.SetParent(transform, false);
            var line = conductor.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 4;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 3;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void LateUpdate()
        {
            RefreshRoute();
        }

        private void RefreshRoute()
        {
            if (positiveConductor == null || returnConductor == null)
            {
                return;
            }

            var visible = IsConductive;
            positiveConductor.enabled = visible;
            returnConductor.enabled = visible;
            if (!visible)
            {
                return;
            }

            var startLocal = circuitRoot.InverseTransformPoint(start.transform.position);
            var endLocal = circuitRoot.InverseTransformPoint(end.transform.position);
            var routeY = 0.18f;
            var direction = endLocal - startLocal;
            var side = Vector3.Cross(Vector3.up, direction);
            side = side.sqrMagnitude > 0.0001f ? side.normalized * 0.045f : Vector3.right * 0.045f;
            SetRoute(positiveConductor, startLocal, endLocal, routeY, side);
            SetRoute(returnConductor, startLocal, endLocal, routeY, -side);
        }

        private void SetRoute(
            LineRenderer line,
            Vector3 startLocal,
            Vector3 endLocal,
            float routeY,
            Vector3 offset)
        {
            line.SetPosition(0, circuitRoot.TransformPoint(startLocal + offset));
            line.SetPosition(
                1,
                circuitRoot.TransformPoint(new Vector3(startLocal.x, routeY, startLocal.z) + offset));
            line.SetPosition(
                2,
                circuitRoot.TransformPoint(new Vector3(endLocal.x, routeY, endLocal.z) + offset));
            line.SetPosition(3, circuitRoot.TransformPoint(endLocal + offset));
        }

        private void OnDestroy()
        {
            start?.Unregister(this);
            end?.Unregister(this);
        }
    }

    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class CaravanElectricalNetwork : MonoBehaviour
    {
        private CaravanChassisController chassis;
        private CaravanPhotovoltaicModule photovoltaic;
        private CaravanBatteryModule battery;
        private CaravanElectricMotorModule motor;
        private CaravanElectricalPort photovoltaicPort;
        private CaravanElectricalPort batteryPort;
        private CaravanElectricalPort motorPort;
        private Material positiveCableMaterial;
        private Material returnCableMaterial;
        private readonly List<CaravanElectricalPort> ports =
            new List<CaravanElectricalPort>(8);
        private readonly List<CaravanElectricalCable> cables =
            new List<CaravanElectricalCable>(8);
        private readonly List<CaravanPhotovoltaicModule> generators =
            new List<CaravanPhotovoltaicModule>(4);
        private readonly List<CaravanBatteryModule> batteries =
            new List<CaravanBatteryModule>(4);
        private readonly List<CaravanElectricMotorModule> motors =
            new List<CaravanElectricMotorModule>(4);
        private readonly List<ICaravanElectricalGenerator> powerGenerators =
            new List<ICaravanElectricalGenerator>(4);
        private readonly List<ICaravanElectricalStorage> powerStorages =
            new List<ICaravanElectricalStorage>(4);
        private readonly List<ICaravanElectricalConsumer> powerConsumers =
            new List<ICaravanElectricalConsumer>(8);
        private readonly Dictionary<CaravanElectricalPort, List<CaravanElectricalPort>>
            adjacency =
                new Dictionary<CaravanElectricalPort, List<CaravanElectricalPort>>();
        private readonly HashSet<CaravanElectricalPort> visited =
            new HashSet<CaravanElectricalPort>();
        private readonly Queue<CaravanElectricalPort> traversal =
            new Queue<CaravanElectricalPort>();
        private readonly List<CaravanElectricalPort> componentPorts =
            new List<CaravanElectricalPort>(8);
        private readonly List<ICaravanElectricalGenerator> componentGenerators =
            new List<ICaravanElectricalGenerator>(4);
        private readonly List<ICaravanElectricalStorage> componentStorages =
            new List<ICaravanElectricalStorage>(4);
        private readonly List<ICaravanElectricalConsumer> componentConsumers =
            new List<ICaravanElectricalConsumer>(8);

        public CaravanPhotovoltaicModule Photovoltaic => photovoltaic;
        public CaravanBatteryModule Battery => battery;
        public CaravanElectricMotorModule Motor => motor;
        public CaravanElectricalPort PhotovoltaicPort => photovoltaicPort;
        public CaravanElectricalPort BatteryPort => batteryPort;
        public CaravanElectricalPort MotorPort => motorPort;
        public IReadOnlyList<CaravanElectricalPort> Ports => ports;
        public IReadOnlyList<CaravanElectricalCable> Cables => cables;
        public IReadOnlyList<CaravanPhotovoltaicModule> Generators => generators;
        public IReadOnlyList<CaravanBatteryModule> Batteries => batteries;
        public IReadOnlyList<CaravanElectricMotorModule> Motors => motors;
        public int CableCount => cables.Count;
        public int ConnectedComponentCount { get; private set; }
        public CaravanElectricalCable PanelToBatteryCable =>
            FindCable(photovoltaicPort, batteryPort);
        public CaravanElectricalCable BatteryToMotorCable =>
            FindCable(batteryPort, motorPort);
        public bool IsClosed => PanelToBatteryCable != null
                                && PanelToBatteryCable.IsConductive
                                && BatteryToMotorCable != null
                                && BatteryToMotorCable.IsConductive;
        public float GeneratedKilowatts { get; private set; }
        public float RequestedKilowatts { get; private set; }
        public float DeliveredKilowatts { get; private set; }
        public float DeficitKilowatts { get; private set; }
        public float SpilledKilowatts { get; private set; }

        public void Configure(
            CaravanChassisController chassis,
            CaravanPhotovoltaicModule photovoltaicModule,
            CaravanBatteryModule batteryModule,
            CaravanElectricMotorModule motorModule,
            CaravanElectricalPort photovoltaicPort,
            CaravanElectricalPort batteryPort,
            CaravanElectricalPort motorPort,
            Material positiveMaterial,
            Material returnMaterial)
        {
            if (photovoltaicModule == null)
            {
                throw new ArgumentNullException(nameof(photovoltaicModule));
            }
            if (batteryModule == null)
            {
                throw new ArgumentNullException(nameof(batteryModule));
            }
            if (motorModule == null)
            {
                throw new ArgumentNullException(nameof(motorModule));
            }

            Configure(
                chassis,
                new[] { photovoltaicPort, batteryPort, motorPort },
                positiveMaterial,
                returnMaterial);
            photovoltaic = photovoltaicModule;
            battery = batteryModule;
            motor = motorModule;
            this.photovoltaicPort = photovoltaicPort;
            this.batteryPort = batteryPort;
            this.motorPort = motorPort;
        }

        public void Configure(
            CaravanChassisController caravan,
            IEnumerable<CaravanElectricalPort> initialPorts,
            Material positiveMaterial,
            Material returnMaterial)
        {
            chassis = caravan != null
                ? caravan
                : throw new ArgumentNullException(nameof(caravan));
            positiveCableMaterial = positiveMaterial != null
                ? positiveMaterial
                : throw new ArgumentNullException(nameof(positiveMaterial));
            returnCableMaterial = returnMaterial != null
                ? returnMaterial
                : throw new ArgumentNullException(nameof(returnMaterial));
            if (initialPorts == null)
            {
                throw new ArgumentNullException(nameof(initialPorts));
            }

            ClearConnections();
            for (var index = motors.Count - 1; index >= 0; index--)
            {
                chassis.DetachElectricMotor(motors[index]);
            }
            ports.Clear();
            generators.Clear();
            batteries.Clear();
            motors.Clear();
            powerGenerators.Clear();
            powerStorages.Clear();
            powerConsumers.Clear();
            adjacency.Clear();
            photovoltaic = null;
            battery = null;
            motor = null;
            photovoltaicPort = null;
            batteryPort = null;
            motorPort = null;

            foreach (var port in initialPorts)
            {
                RegisterPort(port);
            }
        }

        public int RegisterModule(CaravanModule module)
        {
            if (module == null)
            {
                return 0;
            }

            var modulePorts = module.GetComponentsInChildren<CaravanElectricalPort>(true);
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

        public bool RegisterPort(CaravanElectricalPort port)
        {
            if (port == null || port.Part == null || ports.Contains(port))
            {
                return false;
            }

            ports.Add(port);
            adjacency.Add(port, new List<CaravanElectricalPort>(2));
            var electricalGenerator = port.Generator;
            if (electricalGenerator != null
                && !powerGenerators.Contains(electricalGenerator))
            {
                powerGenerators.Add(electricalGenerator);
            }
            var electricalStorage = port.Storage;
            if (electricalStorage != null
                && !powerStorages.Contains(electricalStorage))
            {
                powerStorages.Add(electricalStorage);
            }
            var electricalConsumer = port.Consumer;
            if (electricalConsumer != null
                && !powerConsumers.Contains(electricalConsumer))
            {
                powerConsumers.Add(electricalConsumer);
            }
            if (port.Part.TryGetComponent<CaravanPhotovoltaicModule>(out var generator)
                && !generators.Contains(generator))
            {
                generators.Add(generator);
            }
            if (port.Part.TryGetComponent<CaravanBatteryModule>(out var storage)
                && !batteries.Contains(storage))
            {
                batteries.Add(storage);
            }
            if (port.Part.TryGetComponent<CaravanElectricMotorModule>(out var consumer)
                && !motors.Contains(consumer))
            {
                motors.Add(consumer);
                chassis?.AttachElectricMotor(consumer);
            }

            RefreshPrimaryComponents();
            return true;
        }

        public bool UnregisterPort(
            CaravanElectricalPort port,
            bool removeConnections = true)
        {
            if (port == null || !ports.Contains(port))
            {
                return false;
            }

            if (removeConnections)
            {
                DisconnectPort(port);
            }
            else if (port.ConnectedCableCount > 0)
            {
                return false;
            }

            ports.Remove(port);
            adjacency.Remove(port);
            var part = port.Part;
            if (part != null && !HasRegisteredPort(part))
            {
                var electricalGenerator = port.Generator;
                if (electricalGenerator != null)
                {
                    powerGenerators.Remove(electricalGenerator);
                }
                var electricalStorage = port.Storage;
                if (electricalStorage != null)
                {
                    powerStorages.Remove(electricalStorage);
                }
                var electricalConsumer = port.Consumer;
                if (electricalConsumer != null)
                {
                    powerConsumers.Remove(electricalConsumer);
                    electricalConsumer.ApplyDeliveredPower(0f);
                }
                if (part.TryGetComponent<CaravanPhotovoltaicModule>(out var generator))
                {
                    generators.Remove(generator);
                }
                if (part.TryGetComponent<CaravanBatteryModule>(out var storage))
                {
                    batteries.Remove(storage);
                }
                if (part.TryGetComponent<CaravanElectricMotorModule>(out var consumer))
                {
                    motors.Remove(consumer);
                    chassis?.DetachElectricMotor(consumer);
                }
            }

            RefreshPrimaryComponents();
            return true;
        }

        public bool CanConnect(
            CaravanElectricalPort first,
            CaravanElectricalPort second)
        {
            if (first == null
                || second == null
                || first == second
                || !ports.Contains(first)
                || !ports.Contains(second)
                || first.IsAtCapacity
                || second.IsAtCapacity
                || FindCable(first, second) != null)
            {
                return false;
            }

            return CaravanConnectionRules.AreCompatible(
                first.Kind,
                second.Kind);
        }

        public bool TryConnect(
            CaravanElectricalPort first,
            CaravanElectricalPort second)
        {
            return TryConnect(first, second, out _);
        }

        public bool TryConnect(
            CaravanElectricalPort first,
            CaravanElectricalPort second,
            out CaravanElectricalCable cable)
        {
            cable = null;
            if (!CanConnect(first, second))
            {
                return false;
            }

            cable = CreateCable(first, second);
            cables.Add(cable);
            return true;
        }

        public bool RemoveCable(CaravanElectricalCable cable)
        {
            if (cable == null || !cables.Remove(cable))
            {
                return false;
            }

            cable.Start?.Unregister(cable);
            cable.End?.Unregister(cable);
            cable.SetCurrent(0f);
            Destroy(cable.gameObject);
            return true;
        }

        public int DisconnectPort(CaravanElectricalPort port)
        {
            if (port == null || !ports.Contains(port))
            {
                return 0;
            }

            var removed = 0;
            for (var index = cables.Count - 1; index >= 0; index--)
            {
                var cable = cables[index];
                if (cable != null && (cable.Start == port || cable.End == port))
                {
                    RemoveCable(cable);
                    removed++;
                }
            }

            return removed;
        }

        public void ClearConnections()
        {
            for (var index = cables.Count - 1; index >= 0; index--)
            {
                RemoveCable(cables[index]);
            }
        }

        public void Simulate(float deltaTimeSeconds)
        {
            var hours = Mathf.Max(0f, deltaTimeSeconds) / 3600f;
            GeneratedKilowatts = 0f;
            RequestedKilowatts = 0f;
            DeliveredKilowatts = 0f;
            DeficitKilowatts = 0f;
            SpilledKilowatts = 0f;
            ConnectedComponentCount = 0;

            for (var index = 0; index < powerStorages.Count; index++)
            {
                var storage = powerStorages[index];
                if (IsAlive(storage))
                {
                    storage.BeginPowerStep();
                }
            }
            for (var index = 0; index < powerConsumers.Count; index++)
            {
                var consumer = powerConsumers[index];
                if (!IsAlive(consumer))
                {
                    continue;
                }

                RequestedKilowatts += consumer.RequestedPowerKilowatts;
                consumer.ApplyDeliveredPower(0f);
            }
            for (var index = 0; index < cables.Count; index++)
            {
                cables[index]?.SetCurrent(0f);
            }

            BuildAdjacency();
            visited.Clear();
            for (var index = 0; index < ports.Count; index++)
            {
                var start = ports[index];
                if (start == null
                    || visited.Contains(start)
                    || !adjacency.TryGetValue(start, out var neighbours)
                    || neighbours.Count == 0)
                {
                    continue;
                }

                componentPorts.Clear();
                traversal.Clear();
                traversal.Enqueue(start);
                visited.Add(start);
                while (traversal.Count > 0)
                {
                    var current = traversal.Dequeue();
                    componentPorts.Add(current);
                    if (!adjacency.TryGetValue(current, out var connected))
                    {
                        continue;
                    }

                    for (var neighbourIndex = 0;
                         neighbourIndex < connected.Count;
                         neighbourIndex++)
                    {
                        var neighbour = connected[neighbourIndex];
                        if (visited.Add(neighbour))
                        {
                            traversal.Enqueue(neighbour);
                        }
                    }
                }

                ConnectedComponentCount++;
                SimulateComponent(componentPorts, hours);
            }

            for (var index = 0; index < powerStorages.Count; index++)
            {
                var storage = powerStorages[index];
                if (IsAlive(storage))
                {
                    storage.CompletePowerStep();
                }
            }

            DeficitKilowatts = Mathf.Max(
                0f,
                RequestedKilowatts - DeliveredKilowatts);
        }

        private CaravanElectricalCable FindCable(
            CaravanElectricalPort first,
            CaravanElectricalPort second)
        {
            for (var index = 0; index < cables.Count; index++)
            {
                var cable = cables[index];
                if (cable != null
                    && ((cable.Start == first && cable.End == second)
                        || (cable.Start == second && cable.End == first)))
                {
                    return cable;
                }
            }

            return null;
        }

        private CaravanElectricalCable CreateCable(
            CaravanElectricalPort start,
            CaravanElectricalPort end)
        {
            var cableName = $"{start.Part.Kind} to {end.Part.Kind} Cable";
            var cableObject = new GameObject(cableName);
            cableObject.transform.SetParent(transform, false);
            var cable = cableObject.AddComponent<CaravanElectricalCable>();
            cable.Configure(
                start,
                end,
                transform,
                positiveCableMaterial,
                returnCableMaterial);
            return cable;
        }

        private void RefreshPrimaryComponents()
        {
            photovoltaic = generators.Count > 0 ? generators[0] : null;
            battery = batteries.Count > 0 ? batteries[0] : null;
            motor = motors.Count > 0 ? motors[0] : null;
            photovoltaicPort = FindPort(photovoltaic);
            batteryPort = FindPort(battery);
            motorPort = FindPort(motor);
        }

        private CaravanElectricalPort FindPort(Component component)
        {
            if (component == null)
            {
                return null;
            }

            for (var index = 0; index < ports.Count; index++)
            {
                var port = ports[index];
                if (port != null && port.Part != null
                    && port.Part.gameObject == component.gameObject)
                {
                    return port;
                }
            }

            return null;
        }

        private bool HasRegisteredPort(Component component)
        {
            return FindPort(component) != null;
        }

        private void BuildAdjacency()
        {
            foreach (var neighbours in adjacency.Values)
            {
                neighbours.Clear();
            }

            for (var index = 0; index < cables.Count; index++)
            {
                var cable = cables[index];
                if (cable == null
                    || !cable.IsConductive
                    || !ports.Contains(cable.Start)
                    || !ports.Contains(cable.End))
                {
                    continue;
                }

                AddNeighbour(cable.Start, cable.End);
                AddNeighbour(cable.End, cable.Start);
            }
        }

        private void AddNeighbour(
            CaravanElectricalPort port,
            CaravanElectricalPort neighbour)
        {
            if (!adjacency.TryGetValue(port, out var neighbours))
            {
                neighbours = new List<CaravanElectricalPort>(2);
                adjacency.Add(port, neighbours);
            }

            neighbours.Add(neighbour);
        }

        private void SimulateComponent(
            IReadOnlyList<CaravanElectricalPort> component,
            float hours)
        {
            componentGenerators.Clear();
            componentStorages.Clear();
            componentConsumers.Clear();
            for (var index = 0; index < component.Count; index++)
            {
                var part = component[index].Part;
                if (part == null)
                {
                    continue;
                }

                var generator = component[index].Generator;
                if (generator != null
                    && !componentGenerators.Contains(generator))
                {
                    componentGenerators.Add(generator);
                }
                var storage = component[index].Storage;
                if (storage != null
                    && !componentStorages.Contains(storage))
                {
                    componentStorages.Add(storage);
                }
                var consumer = component[index].Consumer;
                if (consumer != null
                    && !componentConsumers.Contains(consumer))
                {
                    componentConsumers.Add(consumer);
                }
            }

            var generation = 0f;
            for (var index = 0; index < componentGenerators.Count; index++)
            {
                generation += Mathf.Max(
                    0f,
                    componentGenerators[index].AvailableGenerationKilowatts);
            }

            var request = 0f;
            for (var index = 0; index < componentConsumers.Count; index++)
            {
                request += Mathf.Max(
                    0f,
                    componentConsumers[index].RequestedPowerKilowatts);
            }

            GeneratedKilowatts += generation;
            var delivered = Mathf.Min(generation, request);
            var remainingRequest = request - delivered;
            for (var index = 0;
                 index < componentStorages.Count && remainingRequest > 0.0001f;
                 index++)
            {
                var supplied = componentStorages[index].SupplyPower(
                    remainingRequest,
                    hours);
                delivered += supplied;
                remainingRequest -= supplied;
            }

            var surplus = generation - Mathf.Min(generation, request);
            var totalCharge = 0f;
            for (var index = 0;
                 index < componentStorages.Count && surplus > 0.0001f;
                 index++)
            {
                var accepted = componentStorages[index].AcceptCharge(surplus, hours);
                totalCharge += accepted;
                surplus -= accepted;
            }

            DeliveredKilowatts += delivered;
            SpilledKilowatts += Mathf.Max(0f, surplus);
            for (var index = 0; index < componentConsumers.Count; index++)
            {
                var consumer = componentConsumers[index];
                var share = request > 0.0001f
                    ? delivered * consumer.RequestedPowerKilowatts / request
                    : 0f;
                consumer.ApplyDeliveredPower(share);
            }

            var usedGeneration = Mathf.Min(
                generation,
                delivered + totalCharge);
            UpdateComponentCableCurrents(
                component,
                generation,
                usedGeneration);
        }

        private void UpdateComponentCableCurrents(
            IReadOnlyList<CaravanElectricalPort> component,
            float generation,
            float usedGeneration)
        {
            for (var index = 0; index < cables.Count; index++)
            {
                var cable = cables[index];
                if (cable == null
                    || !cable.IsConductive
                    || !ContainsPort(component, cable.Start)
                    || !ContainsPort(component, cable.End))
                {
                    continue;
                }

                var generatorPort = cable.Start.Kind == CaravanElectricalPortKind.Generator
                    ? cable.Start
                    : cable.End.Kind == CaravanElectricalPortKind.Generator
                        ? cable.End
                        : null;
                if (generatorPort != null
                    && generatorPort.Generator is { } generator)
                {
                    var contribution = generation > 0.0001f
                        ? usedGeneration
                          * generator.AvailableGenerationKilowatts
                          / generation
                        : 0f;
                    cable.SetCurrent(contribution);
                    continue;
                }

                var consumerPort = cable.Start.Kind == CaravanElectricalPortKind.Consumer
                    ? cable.Start
                    : cable.End.Kind == CaravanElectricalPortKind.Consumer
                        ? cable.End
                        : null;
                if (consumerPort != null
                    && consumerPort.Consumer is { } consumer)
                {
                    cable.SetCurrent(consumer.DeliveredElectricalKilowatts);
                }
            }
        }

        private static bool IsAlive(object contract)
        {
            return contract is MonoBehaviour behaviour && behaviour != null;
        }

        private static bool ContainsPort(
            IReadOnlyList<CaravanElectricalPort> source,
            CaravanElectricalPort port)
        {
            for (var index = 0; index < source.Count; index++)
            {
                if (source[index] == port)
                {
                    return true;
                }
            }

            return false;
        }

        private void FixedUpdate()
        {
            Simulate(UnityEngine.Time.fixedDeltaTime);
        }
    }
}
