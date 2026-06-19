# Water Tank Fill/Drain Controller
 
A PLC control program simulating an industrial tank-filling process, built in **OpenPLC Editor** using **IEC 61131-3 Structured Text**.
The latch opens when the water level is too low and the Emergency Stop is not enabled. 
The latch closes when the water level is too high or when the Emergency Stop is enabled. 

## Variables
 
| Variable | Class | Type | Description |
|---|---|---|---|
| `FLOAT_LOW` | Input | BOOL | TRUE when the tank has dropped below the low threshold |
| `FLOAT_HIGH` | Input | BOOL | TRUE when the tank is full |
| `ESTOP` | Input | BOOL | TRUE forces the pump off immediately |
| `PUMP_LATCH` | Local | BOOL | Internal latch that holds the "pump on" state between scans |
| `PUMP` | Output | BOOL | Drives the pump relay |
| `FAULT` | Output | BOOL | TRUE if FLOAT_LOW and FLOAT_HIGH are both TRUE at the same time (should be impossible given set-up) |

