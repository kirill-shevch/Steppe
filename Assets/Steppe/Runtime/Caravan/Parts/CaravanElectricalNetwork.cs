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

    public readonly struct CaravanElectricalFlow
    {
        public CaravanElectricalFlow(
            float generatedKilowatts,
            float requestedKilowatts,
            float deliveredKilowatts,
            float batteryChargeKilowatts,
            float batteryDischargeKilowatts,
            float spilledKilowatts,
            float deficitKilowatts,
            float storedKilowattHours)
        {
            GeneratedKilowatts = generatedKilowatts;
            RequestedKilowatts = requestedKilowatts;
            DeliveredKilowatts = deliveredKilowatts;
            BatteryChargeKilowatts = batteryChargeKilowatts;
            BatteryDischargeKilowatts = batteryDischargeKilowatts;
            SpilledKilowatts = spilledKilowatts;
            DeficitKilowatts = deficitKilowatts;
            StoredKilowattHours = storedKilowattHours;
        }

        public float GeneratedKilowatts { get; }
        public float RequestedKilowatts { get; }
        public float DeliveredKilowatts { get; }
        public float BatteryChargeKilowatts { get; }
        public float BatteryDischargeKilowatts { get; }
        public float SpilledKilowatts { get; }
        public float DeficitKilowatts { get; }
        public float StoredKilowattHours { get; }
    }

    /// <summary>
    /// Stateless power-balance calculation. Power is measured in kW and stored
    /// energy in kWh, so a simulation step can be tested without Unity physics.
    /// </summary>
    public static class CaravanElectricalModel
    {
        public static CaravanElectricalFlow Evaluate(
            float availableGenerationKilowatts,
            float requestedLoadKilowatts,
            float storedKilowattHours,
            float capacityKilowattHours,
            float maximumChargeKilowatts,
            float maximumDischargeKilowatts,
            float chargeEfficiency,
            float dischargeEfficiency,
            float deltaTimeHours,
            bool generatorConnected = true,
            bool consumerConnected = true)
        {
            var generation = generatorConnected
                ? Mathf.Max(0f, availableGenerationKilowatts)
                : 0f;
            var request = Mathf.Max(0f, requestedLoadKilowatts);
            var connectedRequest = consumerConnected ? request : 0f;
            var capacity = Mathf.Max(0f, capacityKilowattHours);
            var stored = Mathf.Clamp(storedKilowattHours, 0f, capacity);
            var hours = Mathf.Max(0f, deltaTimeHours);
            var chargeEfficiencyClamped = Mathf.Clamp(chargeEfficiency, 0.01f, 1f);
            var dischargeEfficiencyClamped = Mathf.Clamp(dischargeEfficiency, 0.01f, 1f);

            var directToLoad = Mathf.Min(generation, connectedRequest);
            var remainingLoad = connectedRequest - directToLoad;
            var availableDischarge = hours > 0f
                ? stored * dischargeEfficiencyClamped / hours
                : 0f;
            var batteryDischarge = Mathf.Min(
                remainingLoad,
                Mathf.Max(0f, maximumDischargeKilowatts),
                availableDischarge);
            var delivered = directToLoad + batteryDischarge;

            var surplus = generation - directToLoad;
            var availableChargeInput = hours > 0f
                ? (capacity - stored) / (chargeEfficiencyClamped * hours)
                : 0f;
            var batteryCharge = Mathf.Min(
                surplus,
                Mathf.Max(0f, maximumChargeKilowatts),
                availableChargeInput);

            if (hours > 0f)
            {
                stored += batteryCharge * chargeEfficiencyClamped * hours;
                stored -= batteryDischarge / dischargeEfficiencyClamped * hours;
                stored = Mathf.Clamp(stored, 0f, capacity);
            }

            return new CaravanElectricalFlow(
                generation,
                request,
                delivered,
                batteryCharge,
                batteryDischarge,
                Mathf.Max(0f, surplus - batteryCharge),
                Mathf.Max(0f, request - delivered),
                stored);
        }
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

        public CaravanElectricalPortKind Kind { get; private set; }
        public CaravanPart Part { get; private set; }
        public int ConnectedCableCount => cables.Count;
        public int MaximumConnections => Kind == CaravanElectricalPortKind.Storage ? 2 : 1;
        public bool IsAtCapacity => ConnectedCableCount >= MaximumConnections;
        public bool IsBuildMarkerVisible => buildMarker != null && buildMarker.activeSelf;

        public void Configure(
            CaravanPart part,
            CaravanElectricalPortKind kind,
            GameObject communicationBuildMarker = null)
        {
            Part = part != null ? part : throw new ArgumentNullException(nameof(part));
            Kind = kind;
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
    public sealed class CaravanBatteryModule : MonoBehaviour
    {
        private CaravanPart part;
        private CaravanModule module;
        private Transform chargeWindow;
        private Vector3 chargeWindowBaseScale;
        private Vector3 chargeWindowBasePosition;

        public float StoredEnergyKilowattHours => part != null ? part.StoredAmount : 0f;
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
            part.SetStoredAmount(flow.StoredKilowattHours);
            CurrentChargeKilowatts = flow.BatteryChargeKilowatts;
            CurrentDischargeKilowatts = flow.BatteryDischargeKilowatts;
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
    public sealed class CaravanElectricMotorModule : MonoBehaviour
    {
        private CaravanPart part;
        private CaravanModule module;
        private Transform shaft;
        private float shaftAngle;

        public float MaximumPowerKilowatts => part != null ? part.Capacity : 0f;
        public float RequestedThrottle { get; private set; }
        public float RequestedPowerKilowatts =>
            MaximumPowerKilowatts * RequestedThrottle * (module != null ? module.State.Efficiency : 1f);
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

        internal void ApplyDeliveredPower(float electricalKilowatts)
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
        private CaravanPhotovoltaicModule photovoltaic;
        private CaravanBatteryModule battery;
        private CaravanElectricMotorModule motor;
        private CaravanElectricalPort photovoltaicPort;
        private CaravanElectricalPort batteryPort;
        private CaravanElectricalPort motorPort;
        private Material positiveCableMaterial;
        private Material returnCableMaterial;
        private readonly List<CaravanElectricalPort> ports =
            new List<CaravanElectricalPort>(3);
        private readonly List<CaravanElectricalCable> cables =
            new List<CaravanElectricalCable>(2);

        public CaravanPhotovoltaicModule Photovoltaic => photovoltaic;
        public CaravanBatteryModule Battery => battery;
        public CaravanElectricMotorModule Motor => motor;
        public CaravanElectricalPort PhotovoltaicPort => photovoltaicPort;
        public CaravanElectricalPort BatteryPort => batteryPort;
        public CaravanElectricalPort MotorPort => motorPort;
        public IReadOnlyList<CaravanElectricalPort> Ports => ports;
        public IReadOnlyList<CaravanElectricalCable> Cables => cables;
        public int CableCount => cables.Count;
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
            photovoltaic = photovoltaicModule != null
                ? photovoltaicModule
                : throw new ArgumentNullException(nameof(photovoltaicModule));
            battery = batteryModule != null
                ? batteryModule
                : throw new ArgumentNullException(nameof(batteryModule));
            motor = motorModule != null
                ? motorModule
                : throw new ArgumentNullException(nameof(motorModule));
            if (chassis == null)
            {
                throw new ArgumentNullException(nameof(chassis));
            }

            this.photovoltaicPort = photovoltaicPort != null
                ? photovoltaicPort
                : throw new ArgumentNullException(nameof(photovoltaicPort));
            this.batteryPort = batteryPort != null
                ? batteryPort
                : throw new ArgumentNullException(nameof(batteryPort));
            this.motorPort = motorPort != null
                ? motorPort
                : throw new ArgumentNullException(nameof(motorPort));
            positiveCableMaterial = positiveMaterial != null
                ? positiveMaterial
                : throw new ArgumentNullException(nameof(positiveMaterial));
            returnCableMaterial = returnMaterial != null
                ? returnMaterial
                : throw new ArgumentNullException(nameof(returnMaterial));
            ports.Clear();
            ports.Add(this.photovoltaicPort);
            ports.Add(this.batteryPort);
            ports.Add(this.motorPort);
            chassis.AttachElectricMotor(motor);
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

            return IsCompatiblePair(first.Kind, second.Kind);
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
            if (photovoltaic == null || battery == null || motor == null)
            {
                return;
            }

            var panelToBattery = PanelToBatteryCable;
            var batteryToMotor = BatteryToMotorCable;
            var sourceConnected = panelToBattery != null && panelToBattery.IsConductive;
            var consumerConnected = batteryToMotor != null && batteryToMotor.IsConductive;
            var flow = CaravanElectricalModel.Evaluate(
                photovoltaic.CurrentGenerationKilowatts,
                motor.RequestedPowerKilowatts,
                battery.StoredEnergyKilowattHours,
                battery.CapacityKilowattHours,
                battery.MaximumChargeKilowatts,
                battery.MaximumDischargeKilowatts,
                battery.ChargeEfficiency,
                battery.DischargeEfficiency,
                Mathf.Max(0f, deltaTimeSeconds) / 3600f,
                sourceConnected,
                consumerConnected);

            GeneratedKilowatts = flow.GeneratedKilowatts;
            RequestedKilowatts = flow.RequestedKilowatts;
            DeliveredKilowatts = flow.DeliveredKilowatts;
            DeficitKilowatts = flow.DeficitKilowatts;
            SpilledKilowatts = flow.SpilledKilowatts;
            battery.ApplyFlow(flow);
            motor.ApplyDeliveredPower(flow.DeliveredKilowatts);
            panelToBattery?.SetCurrent(
                Mathf.Max(
                    0f,
                    flow.GeneratedKilowatts - flow.SpilledKilowatts));
            batteryToMotor?.SetCurrent(flow.DeliveredKilowatts);
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

        private static bool IsCompatiblePair(
            CaravanElectricalPortKind first,
            CaravanElectricalPortKind second)
        {
            return first == CaravanElectricalPortKind.Storage
                ? second != CaravanElectricalPortKind.Storage
                : second == CaravanElectricalPortKind.Storage;
        }

        private void FixedUpdate()
        {
            Simulate(UnityEngine.Time.fixedDeltaTime);
        }
    }
}
