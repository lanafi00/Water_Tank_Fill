# Water Tank Fill/Drain Controller
A PLC control system simulating an industrial tank-filling process and its real-time digital twin. This project is designed to mirror virtual commissioning, wherein PLC control logic is validated via 3D simulation. 
<img width="843" height="319" alt="Screenshot 2026-06-19 at 1 01 08 AM" src="https://github.com/user-attachments/assets/34f85c25-4cad-41ba-b937-2ac590974fed" />

**Demo:** [Watch the demo video](https://drive.google.com/file/d/1syzqZGkP7mkvApGItKnaghewwXMCFJKm/view?usp=sharing)
 
This repository has two parts:
- **`/devices`, `/pous`, `project.json`** — the PLC controller, built in **OpenPLC Editor** using **IEC 61131-3 Structured Text**
- **`/unity`** — a Unity scene that simulates the tank, pump, and float switches, connected live to the PLC controller over **Modbus TCP**. Unity has no control logic of its own; it obeys the PLC's pump command.
## Variables
 
| Variable | Class | Type | Location | Description |
|---|---|---|---|---|
| `FLOAT_LOW_IN` | Local | INT | `%MW1` | Written by Unity; nonzero when the tank has dropped below the low threshold |
| `FLOAT_HIGH_IN` | Local | INT | `%MW0` | Written by Unity; nonzero when the tank is full |
| `ESTOP_OK_IN` | Local | INT | `%MW2` | Written by Unity; 1 = safe to run. Fail-safe: 0 or no signal stops the pump |
| `HEARTBEAT_IN` | Local | INT | `%MW3` | Counter Unity increments every poll |
| `PUMP_LATCH` | Local | BOOL | | Internal latch that holds the "pump on" state between scans |
| `HB_TIMER` | Local | TON | | Watchdog timer on the heartbeat |
| `PUMP` | Output | BOOL | `%QX0.0` | Drives the pump relay |
| `FAULT` | Output | BOOL | `%QX0.1` | TRUE if FLOAT_LOW and FLOAT_HIGH are both TRUE at the same time (should be impossible given set-up) |
| `COMMS_FAULT` | Output | BOOL | `%QX0.2` | TRUE if the heartbeat hasn't changed for 500 ms |
 
### Logic
 
```
HB_TIMER(IN := HEARTBEAT_IN = HB_LAST, PT := T#500ms);
HB_LAST := HEARTBEAT_IN;
COMMS_FAULT := HB_TIMER.Q;
 
FAULT := FLOAT_HIGH AND FLOAT_LOW;
PUMP_LATCH := (FLOAT_LOW OR PUMP_LATCH) AND NOT FLOAT_HIGH AND ESTOP_OK
              AND NOT FAULT AND NOT COMMS_FAULT;
PUMP := PUMP_LATCH;
```
 
Built in OpenPLC Editor v4 and run on OpenPLC Runtime v4 (Docker).
 
## Modbus TCP Integration
Every 50 ms, Unity writes the float switches, e-stop, and heartbeat to holding registers 1024–1027 (`%MW0`–`%MW3`) and reads `PUMP`, `FAULT`, and `COMMS_FAULT` from coils 0–2. If Unity stops responding, the PLC's watchdog trips `COMMS_FAULT` and forces the pump off.
 
## Next Steps
A high float stuck off isn't detected, so the tank overflows. A max-fill-time timeout in the PLC would catch this.
 
## Running It
**PLC controller:**
1. Start OpenPLC Runtime v4 in Docker with port 502 exposed
2. Open this project in OpenPLC Editor v4, set the device to OpenPLC Runtime v4 at `127.0.0.1`, then build and start the PLC
**Unity digital twin:**
1. Open the `/unity` folder as a project in Unity Hub
2. Press Play
3. Use keys `E` (e-stop), `L` / `H` (stuck low / high float), and `F` (freeze heartbeat) to inject faults
