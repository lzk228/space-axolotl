using System;
using System.Collections.Generic;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Monitor;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Atmos.Monitor.Components
{
    [Serializable, NetSerializable]
    public enum SharedAirAlarmInterfaceKey
    {
        Key
    }

    [Serializable, NetSerializable]
    public enum AirAlarmMode
    {
        Filtering,
        Fill,
        Panic,
        Replace,
        None
    }

    [Serializable, NetSerializable]
    public enum AirAlarmWireStatus
    {
        Power,
        Access,
        Panic,
        DeviceSync
    }

    [Serializable, NetSerializable]
    public readonly struct AirAlarmAirData
    {
        public readonly float? Pressure { get; }
        public readonly float? Temperature { get; }
        public readonly float? TotalMoles { get; }
        public readonly AtmosMonitorAlarmType AlarmState { get; }

        private readonly Dictionary<Gas, float>? _gases;
        public readonly IReadOnlyDictionary<Gas, float>? Gases { get => _gases; }

        public AirAlarmAirData(float? pressure, float? temperature, float? moles, AtmosMonitorAlarmType state, Dictionary<Gas, float>? gases)
        {
            Pressure = pressure;
            Temperature = temperature;
            TotalMoles = moles;
            AlarmState = state;
            _gases = gases;
        }
    }

    public interface IAtmosDeviceData
    {
        public bool Enabled { get; set; }
        public bool IgnoreAlarms { get; set; }
    }

    // would be nice to include the entire state here
    // but it's already handled by messages
    [Serializable, NetSerializable]
    public class AirAlarmUIState : BoundUserInterfaceState
    {}

    [Serializable, NetSerializable]
    public class AirAlarmResyncAllDevicesMessage : BoundUserInterfaceMessage
    {}

    [Serializable, NetSerializable]
    public class AirAlarmSetAddressMessage : BoundUserInterfaceMessage
    {
        public string Address { get; }

        public AirAlarmSetAddressMessage(string address)
        {
            Address = address;
        }
    }

    [Serializable, NetSerializable]
    public class AirAlarmUpdateAirDataMessage : BoundUserInterfaceMessage
    {
        public AirAlarmAirData AirData;

        public AirAlarmUpdateAirDataMessage(AirAlarmAirData airData)
        {
            AirData = airData;
        }
    }

    [Serializable, NetSerializable]
    public class AirAlarmUpdateAlarmModeMessage : BoundUserInterfaceMessage
    {
        public AirAlarmMode Mode { get; }

        public AirAlarmUpdateAlarmModeMessage(AirAlarmMode mode)
        {
            Mode = mode;
        }
    }

    [Serializable, NetSerializable]
    public class AirAlarmUpdateDeviceDataMessage : BoundUserInterfaceMessage
    {
        public string Address { get; }
        public IAtmosDeviceData Data { get; }

        public AirAlarmUpdateDeviceDataMessage(string addr, IAtmosDeviceData data)
        {
            Address = addr;
            Data = data;
        }
    }

    [Serializable, NetSerializable]
    public class AirAlarmUpdateAlarmThresholdMessage : BoundUserInterfaceMessage
    {
        public AtmosAlarmThreshold Threshold { get; }
        public AtmosMonitorThresholdType Type { get; }
        public Gas? Gas { get; }

        public AirAlarmUpdateAlarmThresholdMessage(AtmosMonitorThresholdType type, AtmosAlarmThreshold threshold, Gas? gas = null)
        {
            Threshold = threshold;
            Type = type;
            Gas = gas;
        }
    }

    [Serializable, NetSerializable]
    public class GasVentPumpData : IAtmosDeviceData
    {
        public bool Enabled { get; set; }
        public bool IgnoreAlarms { get; set; } = false;
        public VentPumpDirection PumpDirection { get; set; }
        public VentPressureBound PressureChecks { get; set; }
        public float ExternalPressureBound { get; set; }
        public float InternalPressureBound { get; set; }
    }

    [Serializable, NetSerializable]
    public class GasVentScrubberData : IAtmosDeviceData
    {
        public bool Enabled { get; set; }
        public bool IgnoreAlarms { get; set; }
        public HashSet<Gas> FilterGases { get; set; } = new();
        public ScrubberPumpDirection  PumpDirection { get; set; }
        public float VolumeRate { get; set; }
        public bool WideNet { get; set; }
    }

    [Serializable, NetSerializable]
    public enum ScrubberPumpDirection : sbyte
    {
        Siphoning = 0,
        Scrubbing = 1,
    }

    [Serializable, NetSerializable]
    public enum VentPumpDirection : sbyte
    {
        Siphoning = 0,
        Releasing = 1,
    }

    [Flags]
    [Serializable, NetSerializable]
    public enum VentPressureBound : sbyte
    {
        NoBound       = 0,
        InternalBound = 1,
        ExternalBound = 2,
    }

}
