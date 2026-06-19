using UnityEngine;

public class TankController : MonoBehaviour
{
    [Header("Sensor Inputs")]
    public bool floatLow;
    public bool floatHigh;
    public bool eStop;

    [Header("Outputs")]
    public bool pumpLatch;
    public bool pump;
    public bool fault;

    [Header("Visuals")]
    public Transform waterLevel;
    public float fillSpeed = 0.2f;
    public float currentFill = 0f;

    void Update()
    { 
        //Keyboard toggles for testing
        if (Input.GetKeyDown(KeyCode.Alpha1)) floatLow = !floatLow;
        if (Input.GetKeyDown(KeyCode.Alpha2)) floatHigh = !floatHigh;
        if (Input.GetKeyDown(KeyCode.Alpha3)) eStop = !eStop;

        pumpLatch = (floatLow || pumpLatch) && !floatHigh && !eStop;
        pump = pumpLatch;
        fault = floatHigh && floatLow;

        if (pump && !fault)
            currentFill += fillSpeed * Time.deltaTime;
        currentFill = Mathf.Clamp01(currentFill);

        if (waterLevel != null){
            //Rescale the water level based on current fill
            Vector2 scale = waterLevel.localScale;
            scale.y = currentFill;
            waterLevel.localScale = scale;
        }
        
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = Color.black;
        style.fontStyle = FontStyle.Bold;

        GUI.Label(new Rect(10, 10, 400, 30), $"FLOAT_LOW (press 1): {floatLow}", style);
        GUI.Label(new Rect(10, 40, 400, 30), $"FLOAT_HIGH (press 2): {floatHigh}", style);
        GUI.Label(new Rect(10, 70, 400, 30), $"ESTOP (press 3): {eStop}", style);
        GUI.Label(new Rect(10, 110, 400, 30), $"PUMP: {pump}", style);
        GUI.Label(new Rect(10, 140, 400, 30), $"FAULT: {fault}", style);
    }


}
    

