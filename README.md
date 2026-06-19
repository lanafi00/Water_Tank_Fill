# Water Tank Fill/Drain Controller
A PLC control system simulating an industrial tank-filling process and its real-time digital twin. This project is designed to mirror virtual commissioning, wherein PLC control logic is validated via 3D simulation. 

This repository has two parts:
- **`/devices`, `/pous`, `project.json`** — the PLC controller, built in **OpenPLC Editor** using **IEC 61131-3 Structured Text**
- **`/unity`** — a Unity scene and C# script that emulates the same tank/pump behavior, intended to eventually connect live to the PLC controller over Modbus TCP

## Variables
 
| Variable | Class | Type | Description |
|---|---|---|---|
| `FLOAT_LOW` | Input | BOOL | TRUE when the tank has dropped below the low threshold |
| `FLOAT_HIGH` | Input | BOOL | TRUE when the tank is full |
| `ESTOP` | Input | BOOL | TRUE forces the pump off immediately |
| `PUMP_LATCH` | Local | BOOL | Internal latch that holds the "pump on" state between scans |
| `PUMP` | Output | BOOL | Drives the pump relay |
| `FAULT` | Output | BOOL | TRUE if FLOAT_LOW and FLOAT_HIGH are both TRUE at the same time (should be impossible given set-up) |

### Logic
 
```
PUMP_LATCH := (FLOAT_LOW OR PUMP_LATCH) AND NOT FLOAT_HIGH AND NOT ESTOP;
PUMP := PUMP_LATCH;
FAULT := FLOAT_HIGH AND FLOAT_LOW;
```

Built and tested using OpenPLC Editor v4's built-in simulator and live debugger.

## Next Steps: Live Modbus TCP Integration
The next step is connecting the Unity client to the OpenPLC controller in real time over **Modbus TCP**.  


## Running It
**PLC controller:**
1. Install OpenPLC Editor v4 and open this project
2. Use the built-in Simulator to build and run
3. Use the Debugger tab to force input variables and observe pump behavior live
**Unity digital twin:**
1. Open the `/unity` folder as a project in Unity Hub
2. Press Play
3. Use keys `1`, `2`, `3` to simulate `FLOAT_LOW`, `FLOAT_HIGH`, and `ESTOP`

