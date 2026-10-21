using System;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// The physical process: a tank with a pump filling it and a constant downstream demand
/// draining it. This script contains NO control logic. It only simulates physics and
/// sensors, and obeys whatever pump command the attached IPumpController gives it.
///
/// Physics runs in FixedUpdate (fixed timestep, independent of frame rate). Set
/// Project Settings > Time > Fixed Timestep to 0.02 to match the PLC's 20 ms scan.
///
/// Keys:  E = e-stop   L / H = cycle low / high float fault (normal, stuck on, stuck off)
/// </summary>
public class TankPlant : MonoBehaviour
{
    public enum SensorOverride { Normal, StuckOn, StuckOff }

    [Header("Process")]
    [Range(0f, 1f)] public float level = 0.5f;
    [Tooltip("Fraction of tank volume added per second while the pump runs")]
    public float inflowRate = 0.15f;
    [Tooltip("Fraction of tank volume drained per second (constant downstream demand)")]
    public float outflowRate = 0.05f;

    [Header("Float switch thresholds (fraction of full)")]
    public float lowThreshold = 0.2f;
    public float highThreshold = 0.9f;

    [Header("Operator input / fault injection")]
    public bool estopPressed;
    public SensorOverride lowFloatOverride = SensorOverride.Normal;
    public SensorOverride highFloatOverride = SensorOverride.Normal;

    [Header("Visuals")]
    [Tooltip("Water mesh (cube or cylinder), sized to the FULL tank in the editor")]
    public Transform waterLevel;

    [Header("Logging")]
    public bool logToCsv = true;

    // Sensor values the controller reads.
    public bool FloatLow { get; private set; }
    public bool FloatHigh { get; private set; }
    public bool EstopOk => !estopPressed; // fail-safe sense: TRUE means "safe to run"

    // Process state.
    public bool PumpRunning { get; private set; }
    public bool Overflowing => level >= 1f && PumpRunning;

    IPumpController controller;
    Vector3 fullScale;
    float bottomY;
    float meshHeight = 1f;
    StreamWriter log;
    string logPath;

    void Awake()
    {
        controller = GetComponent<IPumpController>();
        if (controller == null)
            Debug.LogError("TankPlant needs a controller: add LocalPlcStub or ModbusPlcLink to this GameObject.");

        if (waterLevel != null)
        {
            // Unit height of the mesh (cube = 1, Unity cylinder = 2), so this works for any shape.
            var mf = waterLevel.GetComponent<MeshFilter>();
            meshHeight = (mf != null && mf.sharedMesh != null) ? mf.sharedMesh.bounds.size.y : 1f;

            // Remember the full-tank size so the water can grow from the bottom up.
            fullScale = waterLevel.localScale;
            bottomY = waterLevel.localPosition.y - fullScale.y * meshHeight / 2f;
        }
        UpdateSensors();
    }

    void OnEnable()
    {
        if (!logToCsv) return;
        string dir = Path.Combine(Application.dataPath, "..", "TankLogs");
        Directory.CreateDirectory(dir);
        logPath = Path.Combine(dir, $"run_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        log = new StreamWriter(logPath);
        log.WriteLine("t,level,pump,float_low,float_high,estop_ok,sensor_fault,comms_fault,low_override,high_override");
    }

    void OnDisable()
    {
        log?.Close();
        log = null;
        if (logPath != null) Debug.Log($"Tank log written to {Path.GetFullPath(logPath)}");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E)) estopPressed = !estopPressed;
        if (Input.GetKeyDown(KeyCode.L)) lowFloatOverride = Next(lowFloatOverride);
        if (Input.GetKeyDown(KeyCode.H)) highFloatOverride = Next(highFloatOverride);

        if (waterLevel != null)
        {
            Vector3 scale = fullScale;          // keep x and z, only shrink y
            scale.y = fullScale.y * level;
            waterLevel.localScale = scale;

            Vector3 pos = waterLevel.localPosition;
            pos.y = bottomY + scale.y * meshHeight / 2f;  // anchor the water to the tank floor
            waterLevel.localPosition = pos;
        }
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        PumpRunning = controller != null && controller.PumpOn;
        float inflow = PumpRunning ? inflowRate : 0f;
        level = Mathf.Clamp01(level + (inflow - outflowRate) * dt);

        UpdateSensors();

        if (log != null)
        {
            var c = controller;
            log.WriteLine(string.Join(",",
                Time.time.ToString("F3", CultureInfo.InvariantCulture),
                level.ToString("F4", CultureInfo.InvariantCulture),
                B(PumpRunning), B(FloatLow), B(FloatHigh), B(EstopOk),
                B(c != null && c.SensorFault), B(c != null && c.CommsFault),
                lowFloatOverride, highFloatOverride));
        }
    }

    void UpdateSensors()
    {
        FloatLow = Apply(lowFloatOverride, level < lowThreshold);
        FloatHigh = Apply(highFloatOverride, level > highThreshold);
    }

    static bool Apply(SensorOverride o, bool truth) =>
        o == SensorOverride.StuckOn ? true : o == SensorOverride.StuckOff ? false : truth;

    static SensorOverride Next(SensorOverride o) => (SensorOverride)(((int)o + 1) % 3);

    static string B(bool b) => b ? "1" : "0";

    void OnGUI()
    {
        var style = new GUIStyle { fontSize = 22, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.black;
        var warn = new GUIStyle(style);
        warn.normal.textColor = Color.red;

        hudY = 10;
        HudLine(controller != null ? controller.Status : "NO CONTROLLER ATTACHED", warn);
        HudLine($"Level: {level * 100f:F1} %", style);
        HudLine($"PUMP: {(PumpRunning ? "ON" : "off")}", style);
        HudLine($"FLOAT_LOW: {FloatLow}   (L: {lowFloatOverride})", style);
        HudLine($"FLOAT_HIGH: {FloatHigh}   (H: {highFloatOverride})", style);
        HudLine($"E-STOP (E): {(estopPressed ? "PRESSED" : "released")}", style);

        if (controller != null && controller.SensorFault) HudLine("FAULT: both float switches TRUE", warn);
        if (controller != null && controller.CommsFault) HudLine("COMMS FAULT: pump forced off", warn);
        if (Overflowing) HudLine("OVERFLOW: pump running on a full tank", warn);
    }

    float hudY;
    void HudLine(string text, GUIStyle s)
    {
        GUI.Label(new Rect(10, hudY, 900, 30), text, s);
        hudY += 28;
    }
}
