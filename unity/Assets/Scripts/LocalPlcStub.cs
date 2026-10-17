using UnityEngine;

/// <summary>
/// Offline stand-in for the PLC, used only when OpenPLC isn't running.
/// Mirrors the logic in pous/programs/main.st. Enable EITHER this OR ModbusPlcLink,
/// never both; in the real setup this component is disabled and the PLC is the
/// single source of truth for the control logic.
/// </summary>
[RequireComponent(typeof(TankPlant))]
public class LocalPlcStub : MonoBehaviour, IPumpController
{
    TankPlant plant;
    bool pumpLatch;
    bool fault;

    public bool PumpOn => pumpLatch;
    public bool SensorFault => fault;
    public bool CommsFault => false;
    public string Status => "Controller: local C# stub (offline test mode, not the PLC)";

    void Awake() => plant = GetComponent<TankPlant>();

    // Runs on the fixed physics step, like a PLC scan cycle.
    void FixedUpdate()
    {
        fault = plant.FloatHigh && plant.FloatLow;
        pumpLatch = (plant.FloatLow || pumpLatch) && !plant.FloatHigh && plant.EstopOk && !fault;
    }
}
