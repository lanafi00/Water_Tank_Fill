/// <summary>
/// Anything that can decide whether the pump runs. The tank plant only talks to this
/// interface, so the controller can be swapped (local stub for offline testing, or the
/// real OpenPLC controller over Modbus TCP) without touching the plant model.
/// </summary>
public interface IPumpController
{
    /// <summary>Pump command the plant should obey this physics step.</summary>
    bool PumpOn { get; }

    /// <summary>Sensor-disagreement fault reported by the controller (both floats TRUE).</summary>
    bool SensorFault { get; }

    /// <summary>Controller has lost contact with the plant (watchdog tripped or link down).</summary>
    bool CommsFault { get; }

    /// <summary>One-line, human-readable status for the HUD.</summary>
    string Status { get; }
}
