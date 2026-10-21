using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

/// <summary>
/// Connects the Unity tank plant to the OpenPLC controller over Modbus TCP.
///
/// Every poll cycle (on a background thread, so network stalls never freeze rendering):
///   1. WRITE sensor state + heartbeat -> holding registers %MW0..%MW3
///   2. READ  PUMP, FAULT, COMMS_FAULT  <- coils %QX0.0..%QX0.2
///
/// If no exchange has succeeded for staleAfterSeconds, the link is treated as lost and
/// the plant is told the pump is OFF (fail safe) until communication recovers.
/// </summary>
[RequireComponent(typeof(TankPlant))]
public class ModbusPlcLink : MonoBehaviour, IPumpController
{
    [Header("Connection")]
    public string host = "127.0.0.1";
    public int port = 502;
    [Tooltip("Milliseconds between poll cycles")]
    public int pollIntervalMs = 50;
    [Tooltip("Seconds without a successful exchange before the link counts as lost")]
    public float staleAfterSeconds = 0.5f;

    [Header("Address map (confirm in OpenPLC's Address Mapping Reference)")]
    [Tooltip("Holding register address of %MW0. Default OpenPLC v4 layout puts 1024 %QW registers first.")]
    public ushort mw0Address = 1024;
    [Tooltip("Coil address of %QX0.0")]
    public ushort qx0Address = 0;

    [Header("Fault injection")]
    [Tooltip("Stop incrementing the heartbeat (but keep talking) to trip the PLC watchdog")]
    public KeyCode freezeHeartbeatKey = KeyCode.F;
    public bool freezeHeartbeat;

    TankPlant plant;
    Thread worker;
    volatile bool running;

    // ---- State shared between the main thread and the worker thread (guarded by `gate`) ----
    readonly object gate = new object();
    readonly Stopwatch clock = Stopwatch.StartNew();
    bool outFloatHigh, outFloatLow, outEstopOk, outFreeze;   // main -> worker
    bool inPump, inSensorFault, inCommsFault;                // worker -> main
    double lastGoodExchange = double.NegativeInfinity;
    double lastRoundTripMs;
    string lastError = "not connected yet";


    // ---- IPumpController ----
    public bool PumpOn { get { lock (gate) return LinkHealthyUnlocked() && inPump; } }
    public bool SensorFault { get { lock (gate) return LinkHealthyUnlocked() && inSensorFault; } }
    public bool CommsFault { get { lock (gate) return !LinkHealthyUnlocked() || inCommsFault; } }

    public string Status
    {
        get
        {
            lock (gate)
            {
                if (!LinkHealthyUnlocked()) return $"PLC LINK LOST ({host}:{port}): {lastError}";
                string hb = outFreeze ? "  [heartbeat FROZEN]" : "";
                return $"PLC link OK  {host}:{port}  round trip {lastRoundTripMs:F1} ms{hb}";
            }
        }
    }

    bool LinkHealthyUnlocked() => clock.Elapsed.TotalSeconds - lastGoodExchange < staleAfterSeconds;

    void Awake() => plant = GetComponent<TankPlant>();

    void OnEnable()
    {
        running = true;
        worker = new Thread(PollLoop) { IsBackground = true, Name = "ModbusPoll" };
        worker.Start();
    }

    void OnDisable()
    {
        running = false;
        worker?.Join(1000);
        worker = null;
    }

    void Update()
    {
        if (Input.GetKeyDown(freezeHeartbeatKey)) freezeHeartbeat = !freezeHeartbeat;

        // Hand the latest sensor readings to the worker thread.
        lock (gate)
        {
            outFloatHigh = plant.FloatHigh;
            outFloatLow = plant.FloatLow;
            outEstopOk = plant.EstopOk;
            outFreeze = freezeHeartbeat;
        }
    }

    void PollLoop()
    {
        short heartbeat = 0;
        using (var modbus = new ModbusTcpClient(host, port))
        {
            while (running)
            {
                try
                {
                    if (!modbus.IsConnected) modbus.Connect();

                    bool high, low, estopOk, freeze;
                    lock (gate) { high = outFloatHigh; low = outFloatLow; estopOk = outEstopOk; freeze = outFreeze; }
                    if (!freeze) heartbeat = (short)((heartbeat + 1) % 30000);

                    double t0 = clock.Elapsed.TotalMilliseconds;
                    modbus.WriteRegisters(mw0Address, new short[]
                    {
                        (short)(high ? 1 : 0),     // %MW0  FLOAT_HIGH_IN
                        (short)(low ? 1 : 0),      // %MW1  FLOAT_LOW_IN
                        (short)(estopOk ? 1 : 0),  // %MW2  ESTOP_OK_IN
                        heartbeat                  // %MW3  HEARTBEAT_IN
                    });
                    bool[] coils = modbus.ReadCoils(qx0Address, 3); // PUMP, FAULT, COMMS_FAULT
                    double t1 = clock.Elapsed.TotalMilliseconds;

                    lock (gate)
                    {
                        inPump = coils[0];
                        inSensorFault = coils[1];
                        inCommsFault = coils[2];
                        lastRoundTripMs = t1 - t0;
                        lastGoodExchange = clock.Elapsed.TotalSeconds;
                        lastError = "";
                    }
                }
                catch (Exception e)
                {
                    lock (gate) lastError = e.Message;
                    modbus.Close();
                    Thread.Sleep(500); // back off before reconnecting
                }
                Thread.Sleep(pollIntervalMs);
            }
        }
    }
}
