using System;
using Steppe.Settings;
using Steppe.Surface;
using Steppe.Terrain;
using Steppe.Weather;
using Steppe.World;
using UnityEngine;

namespace Steppe.Caravan
{
    public enum SteppeExpeditionStage
    {
        Locked,
        AssembleWaterKit,
        ConnectWaterLoop,
        PowerPump,
        SetExtractionMode,
        FindWetGround,
        CollectWater,
        AssembleBiomassKit,
        ConnectBiomassChain,
        PowerHarvester,
        SetHarvesterPower,
        FindGrass,
        HarvestGrass,
        FindDryingWind,
        DryBiomass,
        ReturnToLandmark,
        Complete
    }

    public readonly struct SteppeExpeditionSignals
    {
        public SteppeExpeditionSignals(
            bool introComplete,
            bool hasReservoir,
            bool hasPump,
            bool hasRadiator,
            bool waterLoopClosed,
            bool pumpPowered,
            bool pumpExtracting,
            bool wetGround,
            float storedWaterLitres,
            bool hasHarvester,
            bool hasDryer,
            bool hasBiomassStorage,
            bool biomassChainConnected,
            bool harvesterPowered,
            bool harvesterEnabled,
            bool grassAvailable,
            float harvestedKilograms,
            bool dryingWeather,
            float storedDryBiomassKilograms,
            bool atLandmark)
        {
            IntroComplete = introComplete;
            HasReservoir = hasReservoir;
            HasPump = hasPump;
            HasRadiator = hasRadiator;
            WaterLoopClosed = waterLoopClosed;
            PumpPowered = pumpPowered;
            PumpExtracting = pumpExtracting;
            WetGround = wetGround;
            StoredWaterLitres = storedWaterLitres;
            HasHarvester = hasHarvester;
            HasDryer = hasDryer;
            HasBiomassStorage = hasBiomassStorage;
            BiomassChainConnected = biomassChainConnected;
            HarvesterPowered = harvesterPowered;
            HarvesterEnabled = harvesterEnabled;
            GrassAvailable = grassAvailable;
            HarvestedKilograms = harvestedKilograms;
            DryingWeather = dryingWeather;
            StoredDryBiomassKilograms = storedDryBiomassKilograms;
            AtLandmark = atLandmark;
        }

        public bool IntroComplete { get; }
        public bool HasReservoir { get; }
        public bool HasPump { get; }
        public bool HasRadiator { get; }
        public bool WaterLoopClosed { get; }
        public bool PumpPowered { get; }
        public bool PumpExtracting { get; }
        public bool WetGround { get; }
        public float StoredWaterLitres { get; }
        public bool HasHarvester { get; }
        public bool HasDryer { get; }
        public bool HasBiomassStorage { get; }
        public bool BiomassChainConnected { get; }
        public bool HarvesterPowered { get; }
        public bool HarvesterEnabled { get; }
        public bool GrassAvailable { get; }
        public float HarvestedKilograms { get; }
        public bool DryingWeather { get; }
        public float StoredDryBiomassKilograms { get; }
        public bool AtLandmark { get; }
    }

    public sealed class SteppeFirstExpeditionModel
    {
        private const float RequiredWaterLitres = 30f;
        private const float RequiredHarvestKilograms = 4f;
        private const float RequiredDryBiomassKilograms = 2f;
        private float waterBaseline;
        private float harvestBaseline;
        private float dryBiomassBaseline;

        public SteppeExpeditionStage Stage { get; private set; } =
            SteppeExpeditionStage.Locked;
        public float WaterCollected =>
            Mathf.Max(0f, lastSignals.StoredWaterLitres - waterBaseline);
        public float GrassHarvested =>
            Mathf.Max(0f, lastSignals.HarvestedKilograms - harvestBaseline);
        public float BiomassDried =>
            Mathf.Max(
                0f,
                lastSignals.StoredDryBiomassKilograms - dryBiomassBaseline);

        private SteppeExpeditionSignals lastSignals;

        public void Update(SteppeExpeditionSignals signals)
        {
            lastSignals = signals;
            switch (Stage)
            {
                case SteppeExpeditionStage.Locked:
                    if (signals.IntroComplete)
                    {
                        Stage = SteppeExpeditionStage.AssembleWaterKit;
                    }
                    break;
                case SteppeExpeditionStage.AssembleWaterKit:
                    if (signals.HasReservoir
                        && signals.HasPump
                        && signals.HasRadiator)
                    {
                        Stage = SteppeExpeditionStage.ConnectWaterLoop;
                    }
                    break;
                case SteppeExpeditionStage.ConnectWaterLoop:
                    if (signals.WaterLoopClosed)
                    {
                        Stage = SteppeExpeditionStage.PowerPump;
                    }
                    break;
                case SteppeExpeditionStage.PowerPump:
                    if (signals.PumpPowered)
                    {
                        Stage = SteppeExpeditionStage.SetExtractionMode;
                    }
                    break;
                case SteppeExpeditionStage.SetExtractionMode:
                    if (signals.PumpExtracting)
                    {
                        Stage = SteppeExpeditionStage.FindWetGround;
                    }
                    break;
                case SteppeExpeditionStage.FindWetGround:
                    if (signals.WetGround)
                    {
                        waterBaseline = signals.StoredWaterLitres;
                        Stage = SteppeExpeditionStage.CollectWater;
                    }
                    break;
                case SteppeExpeditionStage.CollectWater:
                    if (signals.StoredWaterLitres - waterBaseline
                        >= RequiredWaterLitres)
                    {
                        Stage = SteppeExpeditionStage.AssembleBiomassKit;
                    }
                    break;
                case SteppeExpeditionStage.AssembleBiomassKit:
                    if (signals.HasHarvester
                        && signals.HasDryer
                        && signals.HasBiomassStorage)
                    {
                        Stage = SteppeExpeditionStage.ConnectBiomassChain;
                    }
                    break;
                case SteppeExpeditionStage.ConnectBiomassChain:
                    if (signals.BiomassChainConnected)
                    {
                        Stage = SteppeExpeditionStage.PowerHarvester;
                    }
                    break;
                case SteppeExpeditionStage.PowerHarvester:
                    if (signals.HarvesterPowered)
                    {
                        Stage = SteppeExpeditionStage.SetHarvesterPower;
                    }
                    break;
                case SteppeExpeditionStage.SetHarvesterPower:
                    if (signals.HarvesterEnabled)
                    {
                        Stage = SteppeExpeditionStage.FindGrass;
                    }
                    break;
                case SteppeExpeditionStage.FindGrass:
                    if (signals.GrassAvailable)
                    {
                        harvestBaseline = signals.HarvestedKilograms;
                        Stage = SteppeExpeditionStage.HarvestGrass;
                    }
                    break;
                case SteppeExpeditionStage.HarvestGrass:
                    if (signals.HarvestedKilograms - harvestBaseline
                        >= RequiredHarvestKilograms)
                    {
                        Stage = SteppeExpeditionStage.FindDryingWind;
                    }
                    break;
                case SteppeExpeditionStage.FindDryingWind:
                    if (signals.DryingWeather)
                    {
                        dryBiomassBaseline =
                            signals.StoredDryBiomassKilograms;
                        Stage = SteppeExpeditionStage.DryBiomass;
                    }
                    break;
                case SteppeExpeditionStage.DryBiomass:
                    if (signals.StoredDryBiomassKilograms
                        - dryBiomassBaseline
                        >= RequiredDryBiomassKilograms)
                    {
                        Stage = SteppeExpeditionStage.ReturnToLandmark;
                    }
                    break;
                case SteppeExpeditionStage.ReturnToLandmark:
                    if (signals.AtLandmark)
                    {
                        Stage = SteppeExpeditionStage.Complete;
                    }
                    break;
            }
        }

        public int RemainingWaterLitres()
        {
            return Mathf.CeilToInt(
                Mathf.Max(0f, RequiredWaterLitres - WaterCollected));
        }

        public int RemainingHarvestKilograms()
        {
            return Mathf.CeilToInt(
                Mathf.Max(0f, RequiredHarvestKilograms - GrassHarvested));
        }

        public int RemainingDryBiomassKilograms()
        {
            return Mathf.CeilToInt(
                Mathf.Max(
                    0f,
                    RequiredDryBiomassKilograms - BiomassDried));
        }
    }

    public enum SteppeOpportunityKind
    {
        WetFront,
        Grass,
        DryingWind
    }

    public readonly struct SteppeOpportunityLead
    {
        public SteppeOpportunityLead(
            SteppeOpportunityKind kind,
            double worldX,
            double worldZ,
            float score,
            float distanceMetres)
        {
            Kind = kind;
            WorldX = worldX;
            WorldZ = worldZ;
            Score = score;
            DistanceMetres = distanceMetres;
        }

        public SteppeOpportunityKind Kind { get; }
        public double WorldX { get; }
        public double WorldZ { get; }
        public float Score { get; }
        public float DistanceMetres { get; }
        public bool IsValid => Score > float.MinValue * 0.5f;
    }

    /// <summary>
    /// Samples broad fields and returns a coarse lead. It never searches persistent
    /// cell storage ahead of the player and therefore cannot reveal an exact resource
    /// deposit that the keeper has not visited.
    /// </summary>
    public sealed class SteppeOpportunityScanner
    {
        private static readonly float[] SearchDistances =
        {
            650f,
            1300f,
            2400f,
            3800f,
            5200f
        };

        private readonly SteppeWeatherSystem weather;
        private readonly TerrainHeightGenerator terrain;
        private readonly SteppeSurfaceGenerator surface;
        private readonly SteppeWorldSettings settings;

        public SteppeOpportunityScanner(
            SteppeWorldSettings worldSettings,
            SteppeWeatherSystem weatherSystem)
        {
            settings = worldSettings != null
                ? worldSettings
                : throw new ArgumentNullException(nameof(worldSettings));
            weather = weatherSystem != null
                ? weatherSystem
                : throw new ArgumentNullException(nameof(weatherSystem));
            terrain = new TerrainHeightGenerator(settings);
            surface = new SteppeSurfaceGenerator(settings);
        }

        public SteppeOpportunityLead Find(
            SteppeOpportunityKind kind,
            WorldPosition origin)
        {
            var best = new SteppeOpportunityLead(
                kind,
                origin.X,
                origin.Z,
                float.MinValue,
                0f);
            for (var directionIndex = 0;
                 directionIndex < 16;
                 directionIndex++)
            {
                var angle = directionIndex / 16f * Mathf.PI * 2f;
                var direction = new Vector2(
                    Mathf.Sin(angle),
                    Mathf.Cos(angle));
                for (var distanceIndex = 0;
                     distanceIndex < SearchDistances.Length;
                     distanceIndex++)
                {
                    var distance = SearchDistances[distanceIndex];
                    var worldX = origin.X + direction.x * distance;
                    var worldZ = origin.Z + direction.y * distance;
                    var score = Score(kind, worldX, worldZ, distance);
                    if (score > best.Score)
                    {
                        best = new SteppeOpportunityLead(
                            kind,
                            worldX,
                            worldZ,
                            score,
                            distance);
                    }
                }
            }
            return best;
        }

        private float Score(
            SteppeOpportunityKind kind,
            double worldX,
            double worldZ,
            float distance)
        {
            var weatherSample = weather.Sample(worldX, worldZ);
            var height = terrain.SampleHeight(worldX, worldZ);
            var normal = terrain.SampleNormal(worldX, worldZ, 2.0);
            var surfaceSample = surface.Sample(
                worldX,
                worldZ,
                height,
                normal.y);
            var distancePenalty = distance / SearchDistances[^1] * 0.16f;
            return kind switch
            {
                SteppeOpportunityKind.WetFront =>
                    (float)weatherSample.RainIntensity * 1.25f
                    + (float)weatherSample.CloudWater * 0.58f
                    + (float)weatherSample.StormGust * 0.12f
                    + (float)surfaceSample.WaterRetention * 0.12f
                    - distancePenalty,
                SteppeOpportunityKind.Grass =>
                    (float)surfaceSample.VegetationPotential * 0.72f
                    + (float)surfaceSample.Biomes.FeatherGrass * 0.26f
                    + (float)surfaceSample.Biomes.Meadow * 0.18f
                    + (float)weatherSample.RainIntensity * 0.08f
                    - distancePenalty,
                _ =>
                    Mathf.Clamp01(weatherSample.SurfaceWind.magnitude / 14f)
                    * 0.64f
                    + (1f - (float)weatherSample.RainIntensity) * 0.24f
                    + (1f - (float)weatherSample.CloudWater) * 0.08f
                    + Mathf.InverseLerp(
                        (float)(settings.BaseHeight
                                - settings.MacroAmplitude * 0.25f),
                        (float)(settings.BaseHeight
                                + settings.MacroAmplitude * 0.65f),
                        (float)height) * 0.2f
                    - distancePenalty
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class SteppeFirstExpeditionDirector : MonoBehaviour
    {
        private SteppeFirstExpeditionModel model;
        private SteppeOpportunityScanner scanner;
        private SteppeWorldSettings settings;
        private FloatingOriginSystem floatingOrigin;
        private SteppeWeatherSystem weatherSystem;
        private CaravanEnvironmentSampler environment;
        private CaravanChassisController chassis;
        private CaravanFluidNetwork fluidNetwork;
        private CaravanMaterialNetwork biomassNetwork;
        private CaravanResourceSystem resourceSystem;
        private Transform landmark;
        private bool introComplete;
        private float nextScanTime;
        private SteppeOpportunityLead currentLead;
        private SteppeExpeditionSignals currentSignals;

        public SteppeExpeditionStage Stage => model?.Stage
            ?? SteppeExpeditionStage.Locked;
        public bool HasNavigationLead =>
            Stage == SteppeExpeditionStage.FindWetGround
            || Stage == SteppeExpeditionStage.FindGrass
            || Stage == SteppeExpeditionStage.FindDryingWind
            || Stage == SteppeExpeditionStage.ReturnToLandmark;
        public Vector3 NavigationDirectionLocal { get; private set; }
        public float NavigationDistanceMetres { get; private set; }
        public string NavigationLabel { get; private set; } = string.Empty;
        public SteppeExpeditionSignals CurrentSignals => currentSignals;

        public void Configure(
            SteppeWorldSettings worldSettings,
            FloatingOriginSystem origin,
            SteppeWeatherSystem weather,
            CaravanEnvironmentSampler environmentSampler,
            CaravanChassisController caravan,
            CaravanFluidNetwork fluids,
            CaravanMaterialNetwork biomass,
            CaravanResourceSystem resources,
            Transform namedLandmark)
        {
            settings = worldSettings != null
                ? worldSettings
                : throw new ArgumentNullException(nameof(worldSettings));
            floatingOrigin = origin != null
                ? origin
                : throw new ArgumentNullException(nameof(origin));
            weatherSystem = weather != null
                ? weather
                : throw new ArgumentNullException(nameof(weather));
            environment = environmentSampler != null
                ? environmentSampler
                : throw new ArgumentNullException(nameof(environmentSampler));
            chassis = caravan != null
                ? caravan
                : throw new ArgumentNullException(nameof(caravan));
            fluidNetwork = fluids != null
                ? fluids
                : throw new ArgumentNullException(nameof(fluids));
            biomassNetwork = biomass != null
                ? biomass
                : throw new ArgumentNullException(nameof(biomass));
            resourceSystem = resources != null
                ? resources
                : throw new ArgumentNullException(nameof(resources));
            landmark = namedLandmark != null
                ? namedLandmark
                : throw new ArgumentNullException(nameof(namedLandmark));
            model = new SteppeFirstExpeditionModel();
            scanner = new SteppeOpportunityScanner(settings, weatherSystem);
        }

        public void SetIntroComplete(bool complete)
        {
            introComplete = complete;
        }

        private void Update()
        {
            if (model == null)
            {
                return;
            }

            currentSignals = CollectSignals();
            var previousStage = model.Stage;
            model.Update(currentSignals);
            if (model.Stage != previousStage)
            {
                nextScanTime = 0f;
            }
            UpdateNavigation();
        }

        public string GetTitle()
        {
            return Stage switch
            {
                SteppeExpeditionStage.AssembleWaterKit =>
                    "Подготовьте сбор воды",
                SteppeExpeditionStage.ConnectWaterLoop =>
                    "Соберите водяной контур",
                SteppeExpeditionStage.PowerPump =>
                    "Перенаправьте энергию",
                SteppeExpeditionStage.SetExtractionMode =>
                    "Переведите насос на добычу",
                SteppeExpeditionStage.FindWetGround =>
                    "Перехватите дождевой след",
                SteppeExpeditionStage.CollectWater =>
                    "Соберите воду",
                SteppeExpeditionStage.AssembleBiomassKit =>
                    "Подготовьте заготовку травы",
                SteppeExpeditionStage.ConnectBiomassChain =>
                    "Проложите поток биомассы",
                SteppeExpeditionStage.PowerHarvester =>
                    "Подайте питание на жатку",
                SteppeExpeditionStage.SetHarvesterPower =>
                    "Запустите жатку",
                SteppeExpeditionStage.FindGrass =>
                    "Найдите густой ковыль",
                SteppeExpeditionStage.HarvestGrass =>
                    "Соберите траву",
                SteppeExpeditionStage.FindDryingWind =>
                    "Найдите сухой ветер",
                SteppeExpeditionStage.DryBiomass =>
                    "Высушите запас",
                SteppeExpeditionStage.ReturnToLandmark =>
                    "Вернитесь к Первому гребню",
                SteppeExpeditionStage.Complete =>
                    "Первая экспедиция завершена",
                _ => "Оживите караван"
            };
        }

        public string GetInstruction()
        {
            return Stage switch
            {
                SteppeExpeditionStage.AssembleWaterKit =>
                    MissingWaterKitInstruction(),
                SteppeExpeditionStage.ConnectWaterLoop =>
                    "В слое жидкостей замкните: резервуар → насос → радиатор → резервуар.",
                SteppeExpeditionStage.PowerPump =>
                    "В электрослое подключите насос к свободному контакту батареи. Мотор можно оставить подключённым.",
                SteppeExpeditionStage.SetExtractionMode =>
                    "Наведитесь на рычаг насоса, нажмите E и переведите его в положение «добыча».",
                SteppeExpeditionStage.FindWetGround =>
                    "Следуйте за тёмным небом и влажным горизонтом. Прогноз показывает только общее направление.",
                SteppeExpeditionStage.CollectWater =>
                    $"Остановитесь на влажной земле и соберите ещё {model.RemainingWaterLitres()} л.",
                SteppeExpeditionStage.AssembleBiomassKit =>
                    MissingBiomassKitInstruction(),
                SteppeExpeditionStage.ConnectBiomassChain =>
                    "В слое биомассы соедините жатку → сушилку → хранилище.",
                SteppeExpeditionStage.PowerHarvester =>
                    "Подключите жатку к ещё одному свободному контакту батареи.",
                SteppeExpeditionStage.SetHarvesterPower =>
                    "Включите физический рычаг жатки.",
                SteppeExpeditionStage.FindGrass =>
                    "Ищите высокую плотную траву. Светлая редкая степь даст мало сырья.",
                SteppeExpeditionStage.HarvestGrass =>
                    $"Медленно двигайтесь через траву и соберите ещё {model.RemainingHarvestKilograms()} кг.",
                SteppeExpeditionStage.FindDryingWind =>
                    "Ищите ясный продуваемый гребень. Сушилка может работать пассивно.",
                SteppeExpeditionStage.DryBiomass =>
                    $"Подождите у сушилки, пока в хранилище не появится ещё {model.RemainingDryBiomassKilograms()} кг.",
                SteppeExpeditionStage.ReturnToLandmark =>
                    "Ориентируйтесь на ветровую башню «Первый гребень».",
                SteppeExpeditionStage.Complete =>
                    "Караван обеспечен водой и ремонтным материалом. Степь стала маршрутом, а не фоном.",
                _ => string.Empty
            };
        }

        public string GetNavigationRange()
        {
            if (!HasNavigationLead)
            {
                return string.Empty;
            }
            if (Stage == SteppeExpeditionStage.ReturnToLandmark)
            {
                return NavigationDistanceMetres < 1000f
                    ? $"около {Mathf.Max(50, Mathf.RoundToInt(NavigationDistanceMetres / 50f) * 50)} м"
                    : $"около {NavigationDistanceMetres / 1000f:F1} км";
            }
            if (NavigationDistanceMetres < 900f)
            {
                return "меньше километра";
            }
            if (NavigationDistanceMetres < 2600f)
            {
                return "примерно 1–3 км";
            }
            return "несколько километров";
        }

        private SteppeExpeditionSignals CollectSignals()
        {
            var reservoir = fluidNetwork.Reservoir;
            var pump = fluidNetwork.Pump;
            var radiator = fluidNetwork.Radiator;
            var pumpPort = pump != null
                ? pump.GetComponentInChildren<CaravanElectricalPort>(true)
                : null;
            var harvester = resourceSystem.Harvesters.Count > 0
                ? resourceSystem.Harvesters[0]
                : null;
            var dryer = resourceSystem.Dryers.Count > 0
                ? resourceSystem.Dryers[0]
                : null;
            var storage = resourceSystem.Storages.Count > 0
                ? resourceSystem.Storages[0]
                : null;
            var harvesterPort = harvester != null
                ? harvester.GetComponentInChildren<CaravanElectricalPort>(true)
                : null;
            var biomassConnected = harvester != null
                                   && dryer != null
                                   && storage != null
                                   && biomassNetwork.AreDirectlyConnected(
                                       harvester.GetComponent<CaravanPart>(),
                                       dryer.GetComponent<CaravanPart>())
                                   && biomassNetwork.AreDirectlyConnected(
                                       dryer.GetComponent<CaravanPart>(),
                                       storage.GetComponent<CaravanPart>());
            var harvested = 0f;
            for (var index = 0;
                 index < resourceSystem.Harvesters.Count;
                 index++)
            {
                harvested += resourceSystem.Harvesters[index]
                    .TotalHarvestedKilograms;
            }
            var dryStored = 0f;
            for (var index = 0;
                 index < resourceSystem.Storages.Count;
                 index++)
            {
                dryStored += resourceSystem.Storages[index]
                    .StoredDryBiomassKilograms;
            }

            var wetGround = false;
            var grassAvailable = false;
            var dryingWeather = false;
            if (environment.TrySample(chassis.transform.position, out var sample))
            {
                var availableWater =
                    sample.Ecology.SurfaceWater
                    + sample.Ecology.SnowWater
                    + sample.Ecology.RootWater * 0.35;
                wetGround = availableWater > 0.11
                            || sample.Weather.RainIntensity > 0.08;
                grassAvailable =
                    sample.Ecology.Biomass > 0.18
                    && sample.Surface.VegetationPotential > 0.32;
                dryingWeather =
                    sample.Weather.SurfaceWind.magnitude > 5.5f
                    && sample.Weather.RainIntensity < 0.08
                    && environment.SampleAirTemperature(
                        chassis.transform.position) > 2.0;
            }

            var atLandmark = landmark != null
                             && Vector3.Distance(
                                 chassis.transform.position,
                                 landmark.position) < 55f;
            return new SteppeExpeditionSignals(
                introComplete,
                reservoir != null,
                pump != null,
                radiator != null,
                fluidNetwork.IsClosed,
                pumpPort != null && pumpPort.ConnectedCableCount > 0,
                pump != null && pump.Mode == CaravanPumpMode.Extraction,
                wetGround,
                reservoir != null ? reservoir.StoredWaterLitres : 0f,
                harvester != null,
                dryer != null,
                storage != null,
                biomassConnected,
                harvesterPort != null
                && harvesterPort.ConnectedCableCount > 0,
                harvester != null && harvester.OperatingLevel > 0.08f,
                grassAvailable,
                harvested,
                dryingWeather,
                dryStored,
                atLandmark);
        }

        private void UpdateNavigation()
        {
            if (!HasNavigationLead)
            {
                NavigationDirectionLocal = Vector3.zero;
                NavigationDistanceMetres = 0f;
                NavigationLabel = string.Empty;
                return;
            }

            if (Stage == SteppeExpeditionStage.ReturnToLandmark)
            {
                var delta = landmark.position - chassis.transform.position;
                NavigationDirectionLocal =
                    Vector3.ProjectOnPlane(delta, Vector3.up).normalized;
                NavigationDistanceMetres =
                    Vector3.ProjectOnPlane(delta, Vector3.up).magnitude;
                NavigationLabel = "башня «Первый гребень»";
                return;
            }

            if (UnityEngine.Time.unscaledTime >= nextScanTime)
            {
                var kind = Stage switch
                {
                    SteppeExpeditionStage.FindWetGround =>
                        SteppeOpportunityKind.WetFront,
                    SteppeExpeditionStage.FindGrass =>
                        SteppeOpportunityKind.Grass,
                    _ => SteppeOpportunityKind.DryingWind
                };
                var world = floatingOrigin.LocalToWorld(
                    chassis.transform.position);
                currentLead = scanner.Find(kind, world);
                nextScanTime = UnityEngine.Time.unscaledTime + 4f;
            }

            var target = floatingOrigin.WorldToLocal(
                currentLead.WorldX,
                chassis.transform.position.y,
                currentLead.WorldZ);
            var direction =
                Vector3.ProjectOnPlane(
                    target - chassis.transform.position,
                    Vector3.up);
            NavigationDistanceMetres = direction.magnitude;
            NavigationDirectionLocal = direction.sqrMagnitude > 0.01f
                ? direction.normalized
                : chassis.transform.forward;
            NavigationLabel = Stage switch
            {
                SteppeExpeditionStage.FindWetGround => "влажный фронт",
                SteppeExpeditionStage.FindGrass => "густая растительность",
                _ => "сильный сухой ветер"
            };
        }

        private string MissingWaterKitInstruction()
        {
            if (!currentSignals.HasReservoir)
            {
                return "В строительстве создайте водяной резервуар.";
            }
            if (!currentSignals.HasPump)
            {
                return "Теперь установите двухрежимный насос.";
            }
            return "Добавьте ветровой радиатор, чтобы замкнуть рабочий контур.";
        }

        private string MissingBiomassKitInstruction()
        {
            if (!currentSignals.HasHarvester)
            {
                return "В строительстве создайте жатку биомассы.";
            }
            if (!currentSignals.HasDryer)
            {
                return "Добавьте сушилку травы.";
            }
            return "Добавьте хранилище сухой биомассы.";
        }
    }
}
