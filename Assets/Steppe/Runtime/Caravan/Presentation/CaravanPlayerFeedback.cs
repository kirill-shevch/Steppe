using UnityEngine;

namespace Steppe.Caravan
{
    public enum CaravanOperationalState
    {
        Ready,
        Working,
        Starved,
        Blocked,
        Full,
        Dirty,
        Damaged
    }

    public readonly struct CaravanModuleFeedback
    {
        public CaravanModuleFeedback(
            string title,
            CaravanOperationalState state,
            string stateLabel,
            string reason,
            string input,
            string output)
        {
            Title = title;
            State = state;
            StateLabel = stateLabel;
            Reason = reason;
            Input = input;
            Output = output;
        }

        public string Title { get; }
        public CaravanOperationalState State { get; }
        public string StateLabel { get; }
        public string Reason { get; }
        public string Input { get; }
        public string Output { get; }
    }

    /// <summary>
    /// Converts the caravan's authoritative simulation state into short,
    /// player-facing causes. It deliberately reports one dominant explanation
    /// instead of exposing every technical parameter at once.
    /// </summary>
    public static class CaravanModuleFeedbackBuilder
    {
        public static CaravanModuleFeedback Evaluate(CaravanModule module)
        {
            if (module == null)
            {
                return default;
            }

            var part = module.GetComponent<CaravanPart>();
            var title = part != null
                ? CaravanPartCatalog.Get(part.Kind).DisplayName
                : module.ModuleId;
            if (part == null)
            {
                return WithMaintenance(
                    module,
                    new CaravanModuleFeedback(
                        title,
                        CaravanOperationalState.Ready,
                        "Готов",
                        "Несущая часть каравана",
                        string.Empty,
                        $"Нагрузка: {module.State.Load:P0}"));
            }

            var feedback = part.Kind switch
            {
                CaravanPartKind.PhotovoltaicLeaves =>
                    EvaluatePhotovoltaic(module, part, title),
                CaravanPartKind.Battery =>
                    EvaluateBattery(module, title),
                CaravanPartKind.ElectricMotor =>
                    EvaluateMotor(module, title),
                CaravanPartKind.WaterReservoir =>
                    EvaluateReservoir(module, title),
                CaravanPartKind.DualModePump =>
                    EvaluatePump(module, title),
                CaravanPartKind.Radiator =>
                    EvaluateRadiator(module, title),
                CaravanPartKind.Sail =>
                    EvaluateSail(module, title),
                CaravanPartKind.Harvester =>
                    EvaluateHarvester(module, part, title),
                CaravanPartKind.GrassDryer =>
                    EvaluateDryer(module, part, title),
                CaravanPartKind.BiomassStorage =>
                    EvaluateBiomassStorage(module, title),
                _ => EvaluateGeneric(module, part, title)
            };
            return WithMaintenance(module, feedback);
        }

        private static CaravanModuleFeedback EvaluatePhotovoltaic(
            CaravanModule module,
            CaravanPart part,
            string title)
        {
            var photovoltaic = module.GetComponent<CaravanPhotovoltaicModule>();
            var port = module.GetComponentInChildren<CaravanElectricalPort>(true);
            if (photovoltaic == null)
            {
                return EvaluateGeneric(module, part, title);
            }

            var input =
                $"Свет: {photovoltaic.CurrentIncidence:P0}  •  сквозь облака: {photovoltaic.CurrentCloudTransmission:P0}";
            var output = $"Генерация: {photovoltaic.CurrentGenerationKilowatts:F1} кВт";
            if (photovoltaic.CurrentIncidence < 0.04f)
            {
                return Feedback(
                    title,
                    CaravanOperationalState.Starved,
                    "Нет света",
                    "Ночь или листья отвернуты от солнца",
                    input,
                    output);
            }
            if (photovoltaic.CurrentCloudTransmission < 0.18f)
            {
                return Feedback(
                    title,
                    CaravanOperationalState.Starved,
                    "Под облаками",
                    "Плотный фронт почти перекрыл солнце",
                    input,
                    output);
            }
            if (port != null && port.ConnectedCableCount == 0)
            {
                return Feedback(
                    title,
                    CaravanOperationalState.Blocked,
                    "Не подключено",
                    "Энергии некуда уходить",
                    input,
                    output);
            }
            return Feedback(
                title,
                photovoltaic.CurrentGenerationKilowatts > 0.05f
                    ? CaravanOperationalState.Working
                    : CaravanOperationalState.Ready,
                photovoltaic.CurrentGenerationKilowatts > 0.05f
                    ? "Вырабатывает"
                    : "Ожидает света",
                "Поверните листья к солнцу для большей мощности",
                input,
                output);
        }

        private static CaravanModuleFeedback EvaluateBattery(
            CaravanModule module,
            string title)
        {
            var battery = module.GetComponent<CaravanBatteryModule>();
            var port = module.GetComponentInChildren<CaravanElectricalPort>(true);
            if (battery == null)
            {
                return default;
            }

            var input = $"Заряд: {battery.StateOfCharge:P0}";
            var output = battery.CurrentChargeKilowatts > 0.01f
                ? $"Принимает: {battery.CurrentChargeKilowatts:F1} кВт"
                : $"Отдаёт: {battery.CurrentDischargeKilowatts:F1} кВт";
            if (port != null)
            {
                output +=
                    $"  •  линии: {port.ConnectedCableCount}/{port.MaximumConnections}";
            }
            if (port != null && port.ConnectedCableCount == 0)
            {
                return Feedback(
                    title,
                    CaravanOperationalState.Blocked,
                    "Изолирована",
                    "Подключите источник или потребитель",
                    input,
                    output);
            }
            if (battery.CurrentChargeKilowatts > 0.01f)
            {
                return Feedback(
                    title,
                    CaravanOperationalState.Working,
                    "Заряжается",
                    "Излишек энергии сохраняется",
                    input,
                    output);
            }
            if (battery.CurrentDischargeKilowatts > 0.01f)
            {
                return Feedback(
                    title,
                    CaravanOperationalState.Working,
                    "Питает сеть",
                    "Батарея покрывает недостаток генерации",
                    input,
                    output);
            }
            if (battery.StateOfCharge <= 0.005f)
            {
                return Feedback(
                    title,
                    CaravanOperationalState.Starved,
                    "Разряжена",
                    "Нужен подключённый источник энергии",
                    input,
                    output);
            }
            if (battery.StateOfCharge >= 0.995f)
            {
                return Feedback(
                    title,
                    CaravanOperationalState.Full,
                    "Заполнена",
                    "Новая энергия будет потеряна",
                    input,
                    output);
            }
            return Feedback(
                title,
                CaravanOperationalState.Ready,
                "Готова",
                "Нет активного потока энергии",
                input,
                output);
        }

        private static CaravanModuleFeedback EvaluateMotor(
            CaravanModule module,
            string title)
        {
            var motor = module.GetComponent<CaravanElectricMotorModule>();
            var port = module.GetComponentInChildren<CaravanElectricalPort>(true);
            if (motor == null)
            {
                return default;
            }

            var input =
                $"Запрос: {motor.RequestedPowerKilowatts:F1} кВт  •  получено: {motor.DeliveredElectricalKilowatts:F1} кВт";
            var output = $"На валу: {motor.DeliveredMechanicalKilowatts:F1} кВт";
            if (motor.RequestedThrottle <= 0.01f)
            {
                return Feedback(
                    title,
                    CaravanOperationalState.Ready,
                    "Выключен",
                    "Поднимите физический рычаг тяги",
                    input,
                    output);
            }
            if (port != null && port.ConnectedCableCount == 0)
            {
                return Feedback(
                    title,
                    CaravanOperationalState.Starved,
                    "Нет питания",
                    "Электрический вход не подключён",
                    input,
                    output);
            }
            if (motor.PowerAvailability < 0.05f)
            {
                return Feedback(
                    title,
                    CaravanOperationalState.Starved,
                    "Не хватает энергии",
                    "Сеть не покрывает запрос двигателя",
                    input,
                    output);
            }
            return Feedback(
                title,
                CaravanOperationalState.Working,
                "Тянет",
                motor.PowerAvailability < 0.95f
                    ? "Мощность ограничена сетью"
                    : "Питание соответствует запросу",
                input,
                output);
        }

        private static CaravanModuleFeedback EvaluateReservoir(
            CaravanModule module,
            string title)
        {
            var reservoir = module.GetComponent<CaravanWaterReservoirModule>();
            if (reservoir == null)
            {
                return default;
            }

            var fill = reservoir.CapacityLitres > 0f
                ? reservoir.StoredWaterLitres / reservoir.CapacityLitres
                : 0f;
            return Feedback(
                title,
                fill >= 0.995f
                    ? CaravanOperationalState.Full
                    : CaravanOperationalState.Ready,
                fill >= 0.995f ? "Заполнен" : "Хранит воду",
                fill <= 0.005f
                    ? "Резервуар пуст"
                    : "Вода добавляет массу каравану",
                $"Температура: {reservoir.TemperatureCelsius:F0} °C",
                $"{reservoir.StoredWaterLitres:F0} / {reservoir.CapacityLitres:F0} л");
        }

        private static CaravanModuleFeedback EvaluatePump(
            CaravanModule module,
            string title)
        {
            var pump = module.GetComponent<CaravanElectricPumpModule>();
            if (pump == null)
            {
                return default;
            }

            var input =
                $"Питание: {pump.DeliveredElectricalKilowatts:F1} / {pump.RequestedPowerKilowatts:F1} кВт";
            var output = $"Поток: {pump.CurrentFlowLitresPerSecond:F1} л/с";
            if (pump.Mode == CaravanPumpMode.Off)
            {
                return Feedback(
                    title,
                    CaravanOperationalState.Ready,
                    "Выключен",
                    "Переведите рычаг в добычу или циркуляцию",
                    input,
                    output);
            }
            if (pump.RequestedPowerKilowatts > 0.01f
                && pump.PowerAvailability < 0.05f)
            {
                return Feedback(
                    title,
                    CaravanOperationalState.Starved,
                    "Нет привода",
                    "Подключите электрическую или механическую мощность",
                    input,
                    output);
            }
            return Feedback(
                title,
                pump.CurrentFlowLitresPerSecond > 0.01f
                    ? CaravanOperationalState.Working
                    : CaravanOperationalState.Blocked,
                pump.CurrentFlowLitresPerSecond > 0.01f
                    ? "Качает"
                    : "Нет потока",
                pump.Mode == CaravanPumpMode.Extraction
                    ? "Добывает доступную местную воду"
                    : "Обслуживает замкнутый контур",
                input,
                output);
        }

        private static CaravanModuleFeedback EvaluateRadiator(
            CaravanModule module,
            string title)
        {
            var radiator = module.GetComponent<CaravanRadiatorModule>();
            if (radiator == null)
            {
                return default;
            }

            return Feedback(
                title,
                radiator.CurrentCoolingKilowatts > 0.01f
                    ? CaravanOperationalState.Working
                    : CaravanOperationalState.Ready,
                radiator.CurrentCoolingKilowatts > 0.01f
                    ? "Охлаждает"
                    : "Ожидает поток",
                radiator.Opening < 0.05f
                    ? "Заслонка закрыта"
                    : "Сильный ветер улучшает охлаждение",
                $"Открытие: {radiator.Opening:P0}",
                $"Охлаждение: {radiator.CurrentCoolingKilowatts:F1} кВт");
        }

        private static CaravanModuleFeedback EvaluateSail(
            CaravanModule module,
            string title)
        {
            var sail = module.GetComponent<CaravanSailModule>();
            if (sail == null)
            {
                return default;
            }

            var force = sail.CurrentForce.Force.magnitude;
            var vane = module.GetComponent<CaravanWindVane>();
            var windSpeed = vane != null ? vane.WindSpeed : 0f;
            return Feedback(
                title,
                force > 5f
                    ? CaravanOperationalState.Working
                    : CaravanOperationalState.Ready,
                force > 5f ? "Ловит ветер" : "Слабая тяга",
                "Курс и угол паруса определяют полезную силу",
                $"Ветер: {windSpeed:F1} м/с  •  угол: {sail.TrimDegrees:F0}°",
                $"Сила: {force:F0} Н");
        }

        private static CaravanModuleFeedback EvaluateHarvester(
            CaravanModule module,
            CaravanPart part,
            string title)
        {
            var harvester = module.GetComponent<CaravanHarvesterModule>();
            if (harvester == null)
            {
                return default;
            }

            var fill = part.Capacity > 0f
                ? harvester.StoredWetBiomassKilograms / part.Capacity
                : 0f;
            var state = harvester.CurrentHarvestKilogramsPerSecond > 0.0001f
                ? CaravanOperationalState.Working
                : fill >= 0.995f
                    ? CaravanOperationalState.Full
                    : harvester.OperatingLevel <= 0.01f
                        ? CaravanOperationalState.Ready
                        : harvester.PowerAvailability < 0.05f
                            ? CaravanOperationalState.Starved
                            : CaravanOperationalState.Blocked;
            var label = state switch
            {
                CaravanOperationalState.Working => "Собирает",
                CaravanOperationalState.Full => "Заполнен",
                CaravanOperationalState.Starved => "Нет привода",
                CaravanOperationalState.Blocked => "Нет доступной травы",
                _ => "Выключен"
            };
            return Feedback(
                title,
                state,
                label,
                state == CaravanOperationalState.Blocked
                    ? "Ищите более густую живую растительность"
                    : "Для сбора караван должен медленно двигаться",
                $"Питание: {harvester.PowerAvailability:P0}",
                $"{harvester.StoredWetBiomassKilograms:F1} / {part.Capacity:F0} кг");
        }

        private static CaravanModuleFeedback EvaluateDryer(
            CaravanModule module,
            CaravanPart part,
            string title)
        {
            var dryer = module.GetComponent<CaravanGrassDryerModule>();
            if (dryer == null)
            {
                return default;
            }

            var working = dryer.CurrentDryingKilogramsPerSecond > 0.0001f;
            var hasInput = dryer.StoredWetBiomassKilograms > 0.001f;
            var state = working
                ? CaravanOperationalState.Working
                : !hasInput
                    ? CaravanOperationalState.Starved
                    : CaravanOperationalState.Ready;
            return Feedback(
                title,
                state,
                working ? "Сушит" : hasInput ? "Ожидает условий" : "Нет сырья",
                hasInput
                    ? "Тёплый ветер ускоряет пассивную сушку"
                    : "Подключите выход жатки",
                $"Влажная масса: {dryer.StoredWetBiomassKilograms:F1} кг",
                $"Сухая масса: {dryer.DryOutputKilograms:F1} кг");
        }

        private static CaravanModuleFeedback EvaluateBiomassStorage(
            CaravanModule module,
            string title)
        {
            var storage = module.GetComponent<CaravanBiomassStorageModule>();
            if (storage == null)
            {
                return default;
            }

            var fill = storage.CapacityKilograms > 0f
                ? storage.StoredDryBiomassKilograms / storage.CapacityKilograms
                : 0f;
            return Feedback(
                title,
                fill >= 0.995f
                    ? CaravanOperationalState.Full
                    : CaravanOperationalState.Ready,
                fill >= 0.995f ? "Заполнено" : "Хранит топливо",
                fill <= 0.005f
                    ? "Нет сухой биомассы для ремонта и сжигания"
                    : "Сухая биомасса доступна потребителям",
                string.Empty,
                $"{storage.StoredDryBiomassKilograms:F1} / {storage.CapacityKilograms:F0} кг");
        }

        private static CaravanModuleFeedback EvaluateGeneric(
            CaravanModule module,
            CaravanPart part,
            string title)
        {
            var working = part.CurrentOutput > 0.001f;
            return Feedback(
                title,
                working
                    ? CaravanOperationalState.Working
                    : CaravanOperationalState.Ready,
                working ? "Работает" : "Готов",
                working
                    ? "Модуль выдаёт полезный результат"
                    : "Нет активного процесса",
                part.Capacity > 0f
                    ? $"Запас: {part.StoredAmount:F1} / {part.Capacity:F1}"
                    : string.Empty,
                $"Выход: {part.CurrentOutput:F1}");
        }

        private static CaravanModuleFeedback WithMaintenance(
            CaravanModule module,
            CaravanModuleFeedback feedback)
        {
            if (module.State.Integrity < 0.35f)
            {
                return Feedback(
                    feedback.Title,
                    CaravanOperationalState.Damaged,
                    "Сильно повреждено",
                    "Удерживайте R и используйте сухую биомассу",
                    feedback.Input,
                    feedback.Output);
            }
            if (module.State.Dust > 0.72f)
            {
                return Feedback(
                    feedback.Title,
                    CaravanOperationalState.Dirty,
                    "Забито пылью",
                    "Удерживайте C для очистки",
                    feedback.Input,
                    feedback.Output);
            }
            return feedback;
        }

        private static CaravanModuleFeedback Feedback(
            string title,
            CaravanOperationalState state,
            string label,
            string reason,
            string input,
            string output)
        {
            return new CaravanModuleFeedback(
                title,
                state,
                label,
                reason,
                input,
                output);
        }
    }

    public enum CaravanOnboardingStage
    {
        ConnectSolarToBattery,
        ConnectBatteryToMotor,
        SetThrottle,
        StartMoving,
        Complete
    }

    public sealed class CaravanOnboardingModel
    {
        private const float RequiredTravelMetres = 120f;
        private float travelledMetres;

        public CaravanOnboardingStage Stage { get; private set; } =
            CaravanOnboardingStage.ConnectSolarToBattery;
        public float TravelledMetres => travelledMetres;

        public void Update(
            bool solarConnected,
            bool motorConnected,
            float throttle,
            float travelledDeltaMetres)
        {
            if (Stage == CaravanOnboardingStage.StartMoving)
            {
                travelledMetres += Mathf.Max(0f, travelledDeltaMetres);
            }
            switch (Stage)
            {
                case CaravanOnboardingStage.ConnectSolarToBattery:
                    if (solarConnected)
                    {
                        Stage = motorConnected
                            ? CaravanOnboardingStage.SetThrottle
                            : CaravanOnboardingStage.ConnectBatteryToMotor;
                    }
                    break;
                case CaravanOnboardingStage.ConnectBatteryToMotor:
                    if (solarConnected && motorConnected)
                    {
                        Stage = CaravanOnboardingStage.SetThrottle;
                    }
                    break;
                case CaravanOnboardingStage.SetThrottle:
                    if (solarConnected && motorConnected && throttle > 0.08f)
                    {
                        Stage = CaravanOnboardingStage.StartMoving;
                    }
                    break;
                case CaravanOnboardingStage.StartMoving:
                    if (travelledMetres >= RequiredTravelMetres)
                    {
                        Stage = CaravanOnboardingStage.Complete;
                    }
                    break;
            }
        }

        public string GetTitle()
        {
            return Stage switch
            {
                CaravanOnboardingStage.ConnectSolarToBattery =>
                    "Оживите караван",
                CaravanOnboardingStage.ConnectBatteryToMotor =>
                    "Замкните силовую цепь",
                CaravanOnboardingStage.SetThrottle =>
                    "Подайте тягу",
                CaravanOnboardingStage.StartMoving =>
                    "Почувствуйте караван",
                _ => "Путь открыт"
            };
        }

        public string GetInstruction()
        {
            return Stage switch
            {
                CaravanOnboardingStage.ConnectSolarToBattery =>
                    "B — строительство, Tab — электричество. Соедините солнечные листья с батареей.",
                CaravanOnboardingStage.ConnectBatteryToMotor =>
                    "Соедините второй порт батареи с электромотором.",
                CaravanOnboardingStage.SetThrottle =>
                    "Выйдите из строительства, наведитесь на рычаг мотора и нажмите E. A/D меняют тягу.",
                CaravanOnboardingStage.StartMoving =>
                    $"Займите руль и проедьте ещё {Mathf.CeilToInt(Mathf.Max(0f, RequiredTravelMetres - travelledMetres))} м.",
                _ =>
                    "Следующая цель — научиться читать погоду и искать воду в степи."
            };
        }
    }
}
