using UnityEngine; //Importiert Unity-spezifische Klassen
using System.Collections.Generic; //Für Listen und generische Datenstrukturen wie List<T>
using System.IO; // Um mit Dateien zu arbeiten – z. B. File.WriteAllText() 
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.VisualScripting;

/*
In der Klasse Trackinglogger findet die Implementation der Datenspeicherung ab.
Trackinglogger erbt von Monobehaviour (ist eine Unity-Klasse und wichtig für die Arbeit mit Unity).
Das C#-Protokoll wird an das Hauptkamera-Objekts des Spielers in unity hinzugefügt und loggt automatisch.
Die Klasse Trackinglogger enthält die Klassen: Eventdata, Vector3Serializable, ParticipantData
Die Klasse Trackinglogger enthält die Methoden: GetArg(), DetectLookedBuilding(), DetectUIElement(), DetectLookedPoint(), Start(), FixedUpdate(), OnApplicationQuit()
*/
public class TrackingLogger : MonoBehaviour
{
    // System.Serializable ist wichtig, damit die Daten lokal in einem Ordner gespeichert werden können
    [System.Serializable]
    public class EventData
    {
        public string participantId; // wird aktuell nicht bei jeder Iteration angezeigt, sondern die Datei wird so abgespeichert
        public float timestamp; // wie viel Zeit ab 0:00:00 vergangen ist
        public Vector3Serializable position; // Wo ist die Person gerade (3D)
        public Vector3Serializable rotationEuler; // Wie ist der Kopf rotiert (3D)
        public string currentControllerInput; // entweder Trigger oder Joystick oder nichts
        public bool mapOpened; // Ob Karte gerade geöffnet ist
        // gazeTarget ist das, was in Blickrichtung der Kamera liegt – also dorthin, wo du „geradeaus“ schaust (unterteilt in gazeTargetName und -vector)
        public string gazeTargetName; // gazeTargetName spuckt den Blick als Gebäudenamen aus
        public Vector3Serializable gazeTargetVector; // gazeTargetVector spuckt den Blick als Vektor aus
        public Vector2 gazePoint; // x/y Blick auf den Bildschirm, Eyetracking!!
        public string currentScene; // ob Schloss oder Marktplatz und welcher
    }

    /*
    Vector3Serializable ist eine eigenst erstellte Klasse, um mit 3D-Vektoren umzugehen.
    Unitys Vector3 ist nicht automatisch serialisierbar in JSON
    Die Klasse enthält die Attribute x, y und z
    */
    [System.Serializable]
    public class Vector3Serializable
    {
        public float x, y, z;
        public Vector3Serializable(Vector3 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }
    }

    /*
    Diese Klasse ist eine Container-Klasse für die ganze Sitzung.
    Sie bildet den zentralen Punkt, um diese Daten dann später abzuspeicherrn
    Kurz: ParticipantData = eine Datei pro Versuchsperson/Sitzung.
    */
    [System.Serializable]
    public class ParticipantData
    {
        public string participantId;
        public List<EventData> events = new List<EventData>();
    }

    // Attribute für EventData
    private ParticipantData currentData = new ParticipantData();
    private float sessionStartTime;
    private string dataPath;
    private string currentControllerInput = "";
    private bool mapActive = false;

    // alternativ ohne SceneManager:
    private string currentScene = "";
    // Überlege dir, ob du scene dynamisch über SceneManager.GetActiveScene().name holst.


    // Attrribute, wenn man per Code UI-Elemente erkennen will, auf die die Person gerade schaut
    public GraphicRaycaster mapCanvas;
    public EventSystem eventSystem;


    // Attribute zum Abspeichern der Controller Inputs
    public InputActionProperty moveAction; // Left joystick input
    public InputActionProperty rightTriggerAction; // Right Trigger Input


    // Attribute genutzt in FixedUpdate, um in regelmäßigen (physisch orientierten) Zeitintervallen die Speicherung durchzunehmen
    // 1 ist Sekundentakt, 5 mal die Sekunde wird gespeichert

    // TODO: funktionniert das, brauche ich nextLogTime?
    private float nextLogTime;

    /*
    Die Methode GetArg() bekommt vom PythonSkript Daten übergeben und gibt sie als string zurück.
    Hier konkret wichtig, um den playerKey für die participantID abzufangen.
    */
    private string GetArg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
            if (args[i].StartsWith(name + "="))
                return args[i].Split('=')[1];
        return null;
    }

    /*
    Die Methode DetectLookedBuilding erkennt in der Welt. wo man geradeaus hinguckt (Blickrichtung).
    Dabei wird ein string vom Namen des Objektes gespeichert, z.B. Gebäude 
    */
    private string DetectLookedBuilding()
    {
        RaycastHit hit;
        Vector3 gazeDirection = transform.forward; // Blickrichtung der Kamera
        if (Physics.Raycast(transform.position, gazeDirection, out hit, 100f))
        {
            return hit.collider.gameObject.name;
        }
        return "No building";
    }

    /*
    Die Methode DetectUIElement() erkennt in der Karte, wo man geradeaus hinguckt.
    Dies gibt sie als string zurück.
    (Blickerkennung)
    */
    private string DetectUIElement()
    {
        PointerEventData pointerData = new PointerEventData(eventSystem);
        pointerData.position = new Vector2(Screen.width / 2, Screen.height / 2); // Blickzentrum
        List<RaycastResult> results = new List<RaycastResult>();
        mapCanvas.Raycast(pointerData, results);
        if (results.Count > 0)
        {
            return results[0].gameObject.name;
        }
        return "No UI-Element";
    }

    /*
    Die Methode DetectLookedPoint() erkennt in der Karte, wo man geradeaus hinguckt und speichert das als 3D-Vektor ab.
    (Blickerkennung)
    */
    // Todo: Muss noch auf Eyetracking angepasst werden
    Vector3 DetectLookedPoint()
    {
        RaycastHit hit;
        Vector3 gazeDirection = transform.forward; // Blickrichtung der Kamera
        if (Physics.Raycast(transform.position, gazeDirection, out hit, 100f))
        {
            return hit.point; // Weltposition, wo der Blickstrahl ein Objekt getroffen hat
        }

        // Optionaler Default-Wert bei keinem Treffer (z. B. "unendlich weit vorne")
        return transform.position + gazeDirection * 100f;
    }


    /*
    Die Methode Start() Wird ein Mal beim Start des Spieles ausgeführt.
    Sie initialisiert die Zeitmessung,
    sie bekommt den playerKey und die aktuelle Szene übergeben,
    sie bestimmt, wo die Datei gespeichert wird (dataPath).
    */
    void Start()
    {
        sessionStartTime = Time.time;

    

        // Kommandozeilenparameter auslesen
        string playerKey = "4";

        if (string.IsNullOrEmpty(playerKey))
        {
            Debug.LogError("No --playerKey given! Please provide a participant ID.");
            playerKey = "Unknown";
        }

        currentData.participantId = playerKey;

        // Speicherpfad
        // Application.persistentDataPath ist ein Unity-Standardpfad, der plattformunabhängig auf einen schreibbaren Speicherort zeigt,
        // z. B.: unter Windows: C:/Users/USERNAME/AppData/LocalLow/CompanyName/ProductName
        dataPath = Path.Combine(Application.persistentDataPath, $"VR_VP_{playerKey}.json");

        Debug.Log("Tracking gestartet für Teilnehmer: " + playerKey);
    }


    /*
    Die Methode FixedUpdate() speichert die Daten in einem festen physischen Zeitintervall ab.
    Es kreiert ein neues EventData-Objekt, welches es dann an currentData-Objekt der participantID hängt.
    Vor dem Erstellen des EventData-Objekts werden die gebrauchten Daten initialisiert
    */
    void FixedUpdate()
    {
        
        // von if zu while-Schleife gewechselt, weil so mit Frame Drops umgeangen werden kann
        
            // Time
            float elapsedTime = Time.time - sessionStartTime;

            // Szene
            // FALLS SZENE BLACK SCREEN IST, DANN RETURN
            // Todo: Platzhalter
            currentScene = SceneManager.GetActiveScene().name;
            if (currentScene == "Pipipause" || currentScene == "Tutorial")
            {
                return;
            }


            // Controller-Eingabe prüfen
            float triggerValue = rightTriggerAction.action.ReadValue<float>();
            bool isHoldingTrigger = triggerValue >= 0.1f;
            Vector2 joystickInput = moveAction.action.ReadValue<Vector2>();
            bool isHoldingJoystick = joystickInput.magnitude > 0.1f;

            Debug.Log($"Trigger Value: {triggerValue}, Joystick Magnitude: {joystickInput.magnitude}");

            if (isHoldingTrigger && !isHoldingJoystick)
            {
                mapActive = true;
                currentControllerInput = "Trigger";
            }
            else if (isHoldingJoystick && !isHoldingTrigger)
            {
                mapActive = false;
                currentControllerInput = "Joystick";
            }
            else if (isHoldingTrigger && isHoldingJoystick)
            {
                // Joystick und Trigger beide gleichzeitig gedrückt
                mapActive = true;
                currentControllerInput = "Trigger + Joystick";
            }
            else
            { 
                currentControllerInput = "No Input";
                mapActive = false;
            }


            // Raycast-basierte Blickerkennung
            string gazeTargetName = mapActive ? DetectUIElement() : DetectLookedBuilding();

            // Berechnet den 3D-Punkt im Raum, auf den geblickt wird
            Vector3Serializable gazeTargetVector = new Vector3Serializable(DetectLookedPoint());


            // Blickposition als Dummy (kann durch echtes EyeTracking ersetzt werden)
            Vector2 gazePos = new Vector2(0.5f, 0.5f); // Todo: Platzhalter



            // Todo: hier wird Eyetracking implementiert
            /*
            InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (device.TryGetFeatureValue(CommonUsages.eyesData, out Eyes eyes))
            {
              if (eyes.TryGetFixationPoint(out Vector3 gazeFixation))
              {
                 gazePoint = Camera.main.WorldToScreenPoint(gazeFixation);
                }
            }
            */

            EventData newEvent = new EventData()
            {
                participantId = currentData.participantId,
                timestamp = elapsedTime,
                // postion und rotation werden direkt im Objekt initiiert
                position = new Vector3Serializable(transform.position),
                rotationEuler = new Vector3Serializable(transform.rotation.eulerAngles),
                currentControllerInput = currentControllerInput,
                mapOpened = mapActive,
                gazeTargetName = gazeTargetName,
                gazeTargetVector = gazeTargetVector,
                gazePoint = gazePos,
                currentScene = currentScene,
            };

            currentData.events.Add(newEvent);

         

    }

    /*
    Wird einmal beim Beenden der Session ausgeführt.
    Hier: Es wird die Liste ller gesammelten Daten als JSON serialisiert.
    Die Datei wird in den Application.persistentDataPath geschrieben.
    */
    void OnApplicationQuit()
    {
        string json = JsonUtility.ToJson(currentData, true);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dataPath));
            File.WriteAllText(dataPath, json);
        }
        catch (IOException e)
        {
            Debug.LogError("Fehler beim Speichern: " + e.Message);
        }
        Debug.Log("Daten gespeichert unter: " + dataPath);
    }

    // wenn ich das als CSV speichern möchte:
    /*
    void OnApplicationQuit()
    {
        List<string> csvLines = new List<string>();
        // Header
        csvLines.Add("timestamp,posX,posY,posZ,rotX,rotY,rotZ,controllerInput,mapOpened,gazeTargetName,gazeTargetVectorX,gazeTargetVectorY,gazeTargetVectorZ,gazeX,gazeY,scene");
        foreach (var e in currentData.events)
        {
            string line = $"{e.timestamp:F5},{e.position.x:F4},{e.position.y:F4},{e.position.z:F4}," +
              $"{e.rotationEuler.x:F4},{e.rotationEuler.y:F4},{e.rotationEuler.z:F4}," +
              $"{e.currentControllerInput},{e.mapOpened},{e.gazeTargetName},{e.gazeTargetVector.x:F4}," +
              $"{e.gazeTargetVector.y:F4},{e.gazeTargetVector.z:F4},{e.gazePoint.x:F4},{e.gazePoint.y:F4}," +
              $"{e.currentScene}";
            csvLines.Add(line);
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dataPath));
            File.WriteAllLines(dataPath, csvLines);
        }
        catch (IOException e)
        {
            Debug.LogError("Fehler beim Speichern: " + e.Message);
        }


        Debug.Log("Daten gespeichert unter: " + dataPath);
        // wenn ich parallel JSON und CSV speichern möchte:

        //File.WriteAllLines(dataPath.Replace(".json", ".csv"), csvLines);
        //Debug.Log("CSV gespeichert unter: " + dataPath.Replace(".json", ".csv"));
    }
    */
}


// TODO: Idee bei Absturz
/*
void OnDisable()
{
    SaveData();
}

void SaveData()
{
    string json = JsonUtility.ToJson(currentData, true);
    File.WriteAllText(dataPath, json);
}

*/

