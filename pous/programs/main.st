PROGRAM main
  VAR_OUTPUT
    PUMP AT %QX0.0 : BOOL;          (* coil 0: pump relay *)
    FAULT AT %QX0.1 : BOOL;         (* coil 1: both floats TRUE, impossible if sensors are healthy *)
    COMMS_FAULT AT %QX0.2 : BOOL;   (* coil 2: heartbeat from the plant stopped changing *)
  END_VAR
  VAR
    (* Written by the Unity plant over Modbus (holding registers). 0 = FALSE, nonzero = TRUE. *)
    FLOAT_HIGH_IN AT %MW0 : INT;
    FLOAT_LOW_IN AT %MW1 : INT;
    ESTOP_OK_IN AT %MW2 : INT;      (* fail-safe: 1 = e-stop circuit healthy, 0 or no data = stop *)
    HEARTBEAT_IN AT %MW3 : INT;     (* plant increments this every poll *)

    FLOAT_HIGH : BOOL;
    FLOAT_LOW : BOOL;
    ESTOP_OK : BOOL;
    PUMP_LATCH : BOOL;
    HB_LAST : INT;
    HB_TIMER : TON;
  END_VAR

  FLOAT_HIGH := FLOAT_HIGH_IN <> 0;
  FLOAT_LOW := FLOAT_LOW_IN <> 0;
  ESTOP_OK := ESTOP_OK_IN <> 0;

  (* Watchdog: if the heartbeat hasn't changed for 500 ms, assume the plant link is dead. *)
  HB_TIMER(IN := HEARTBEAT_IN = HB_LAST, PT := T#500ms);
  HB_LAST := HEARTBEAT_IN;
  COMMS_FAULT := HB_TIMER.Q;

  FAULT := FLOAT_HIGH AND FLOAT_LOW;

  (* Seal-in latch: start on low float, hold until high float, any fault, or e-stop. *)
  PUMP_LATCH := (FLOAT_LOW OR PUMP_LATCH) AND NOT FLOAT_HIGH AND ESTOP_OK
                AND NOT FAULT AND NOT COMMS_FAULT;
  PUMP := PUMP_LATCH;
END_PROGRAM

CONFIGURATION Config0
  RESOURCE Res0 ON PLC
    TASK task0 (INTERVAL := T#20ms, PRIORITY := 0);
    PROGRAM instance0 WITH task0 : main;
  END_RESOURCE
END_CONFIGURATION
