﻿using UnityEngine; //Importiert Unity-spezifische Klassen
using System.Collections.Generic; //Für Listen und generische Datenstrukturen wie List<T>
using System.IO; // Um mit Dateien zu arbeiten – z. B. File.WriteAllText() 
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.VisualScripting;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System;


/*
In der Klasse Trackinglogger findet die Implementation der Datenspeicherung ab.
Trackinglogger erbt von Monobehaviour (ist eine Unity-Klasse und wichtig für die Arbeit mit Unity).
Das C#-Protokoll wird an das Hauptkamera-Objekts des Spielers in unity hinzugefügt und loggt automatisch.
Die Klasse Trackinglogger enthält die Klassen: Eventdata, Vector3Serializable, ParticipantData
Die Klasse Trackinglogger enthält die Methoden: GetArg(), DetectLookedObject(), DetectLookedPoint(), Start(), FixedUpdate(), OnApplicationQuit()
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
        // public Vector2 gazePoint; // x/y Blick auf den Bildschirm, Eyetracking!!
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
        public List<EventData> events = new List<EventData>();
    }

    // Attribute für EventData
    private ParticipantData currentData = new ParticipantData();
    private string participantId = "Unknown";
    private float sessionStartTime;
    private string dataPath;
    private string currentControllerInput = "";
    private bool mapActive = false;

    // alternativ ohne SceneManager:
    private string currentScene = "";

    // Attrribute, wenn man per Code UI-Elemente erkennen will, auf die die Person gerade schaut
    public GraphicRaycaster uiRaycaster;
    public EventSystem eventSystem;

    // Attribute zum Abspeichern der Controller Inputs
    public InputActionProperty moveAction; // Left joystick input
    public InputActionProperty rightTriggerAction; // Right Trigger Input

    // Attribute genutzt in FixedUpdate, um in regelmäßigen (physisch orientierten) Zeitintervallen die Speicherung durchzunehmen
    public float loggingInterval = 0.25f; // 4 mal die Sekunde wird gespeichert

    // Attribute für die Zeitmessung
    private float nextLogTime;
    private int logCount;


    /*
    Die Methode DetectLookedObject erkennt in der Welt. wo man geradeaus hinguckt (Blickrichtung).
    Dabei wird ein string vom Namen des Objektes gespeichert, z.B. Gebäude 
    */
    private string DetectLookedObject()
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
    sie kreiert) den playerKey,
    sie bestimmt, wo die Datei gespeichert wird (dataPath).
    */
    void Start()
    {
        sessionStartTime = Time.time;
        nextLogTime = Time.time + loggingInterval;

        try
        {
            String suffix = SceneManager.GetActiveScene().name.Contains("Schloss") ? "S" : "M";
            int playerKey = 1;

            while (true)
            {
                string fileS = Path.Combine(Application.persistentDataPath, $"VR_VP_{playerKey}S.csv");
                string fileM = Path.Combine(Application.persistentDataPath, $"VR_VP_{playerKey}M.csv");

                if (suffix == "S")
                {
                    // Wenn weder S noch M existieren → neue ID gefunden
                    if (!File.Exists(fileS) && !File.Exists(fileM)) break;
                }
                else // suffix == "M"
                {
                    // Wenn S existiert, aber M noch nicht → passende ID gefunden
                    if (File.Exists(fileS) && !File.Exists(fileM)) break;
                }

                playerKey++;
            }


            // Speicherpfad
            // Application.persistentDataPath ist ein Unity-Standardpfad, der plattformunabhängig auf einen schreibbaren Speicherort zeigt,
            // z. B.: unter Windows am Expra-PC: C:/Users/USERNAME/AppData/LocalLow/CompanyName/ProductName
            dataPath = Path.Combine(Application.persistentDataPath, $"VR_VP_{playerKey}{suffix}.csv");

            Debug.Log("Tracking gestartet für Teilnehmer: " + playerKey + suffix);


        }
        catch (Exception e)
        {
            Debug.LogError("Fehler in Methode Start(): " + e.Message);
        }

    }


    /* 
    Wenn die Methode SaveData() aufgerufen wird, wird die aktuelle Sitzung in eine JSON-Datei gespeichert.
    Dient als Zusätzliche Absicherung, damit nicht alle Daten verloren gehen, wenn das Programm abstürzt
    */
    private void SaveData()
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
    }


    /*
    Die Methode FixedUpdate() speichert die Daten in einem festen physischen Zeitintervall ab.
    Es kreiert ein neues EventData-Objekt, welches es dann an currentData-Objekt der participantID hängt.
    Vor dem Erstellen des EventData-Objekts werden die gebrauchten Daten initialisiert
    */
    void FixedUpdate()
    {
        if (Time.time >= nextLogTime)
        {
            // Time
            float elapsedTime = logCount * loggingInterval;

            // Szene
            // Todo: Platzhalter
            currentScene = SceneManager.GetActiveScene().name;
            // Todo: Auskommentieren
            /*
            if (currentScene == "Tutorial")
            {
                return;
            }
            */


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
                mapActive = true;
                currentControllerInput = "Trigger + Joystick";
            }
            else
            {
                // weder Joystick noch Trigger
                currentControllerInput = "No Input";
                mapActive = false;
            }


            // Raycast-basierte Blickerkennung
            string gazeTargetName = DetectLookedObject();

            // Berechnet den 3D-Punkt im Raum, auf den geblickt wird
            Vector3Serializable gazeTargetVector = new Vector3Serializable(DetectLookedPoint());


            EventData newEvent = new EventData()
            {
                participantId = participantId,
                timestamp = elapsedTime,
                // postion und rotation werden direkt im Objekt initiiert
                position = new Vector3Serializable(transform.position),
                rotationEuler = new Vector3Serializable(transform.rotation.eulerAngles),
                currentControllerInput = currentControllerInput,
                mapOpened = mapActive,
                gazeTargetName = gazeTargetName,
                gazeTargetVector = gazeTargetVector,
                // gazePoint = gazePos,
                currentScene = currentScene,
            };

            currentData.events.Add(newEvent);

            // Nächstes exaktes Ziel berechnen:
            nextLogTime += loggingInterval;
            logCount++;

            // zusätzliche Speicherung alle 30 Sekunden, damit nicht alle Daten verloren gehen, wenn das Programm abstürzt
            // (Code funktioniert eigentlich so, dass die Speicherdatei bei Programmende ausgeführt wird)
            if (logCount % (int)(30f / loggingInterval) == 0) // alle 30 Sekunden
            {
                SaveData();
            }
        }

    }


    /*
    Wird einmal beim Beenden der Session ausgeführt.
    Hier: Es wird die Liste aller gesammelten Daten als CSV gespeichert.
    Die Datei wird in den Application.persistentDataPath geschrieben.
    */
    void OnApplicationQuit()
    {
        StringBuilder csv = new StringBuilder();

        // Kopfzeile
        csv.AppendLine("participantId,timestamp,posX,posY,posZ,rotX,rotY,rotZ,controllerInput,mapOpened,gazeTargetName,gazeVecX,gazeVecY,currentScene"); // gazeVecZ,gazePointX,gazePointY für EyeTracking hinzufügen

        try
        {
            // Einträge schreiben
            foreach (var e in currentData.events)
            {
                string line = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture, // wichtig für Dezimalpunkt
                    "{0},{1:F2},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16}",
                    e.participantId,
                    e.timestamp,
                    e.position.x, e.position.y, e.position.z,
                    e.rotationEuler.x, e.rotationEuler.y, e.rotationEuler.z,
                    e.currentControllerInput,
                    e.mapOpened,
                    EscapeCSV(e.gazeTargetName),
                    e.gazeTargetVector.x, e.gazeTargetVector.y, e.gazeTargetVector.z,
                    // e.gazePoint.x, e.gazePoint.y, // EyeTracking
                    EscapeCSV(e.currentScene)
                );
                csv.AppendLine(line);
            }

            File.WriteAllText(dataPath, csv.ToString());
        }
        catch (IOException e)
        {
            Debug.LogError("Fehler beim Speichern: " + e.Message);
        }

        Debug.Log("Daten gespeichert unter: " + dataPath);
    }

    /*
    Hilfsmethode für OnApplicationQuit(), um eine saubere Formatierung der CSV-Datei zu haben
    */
    private string EscapeCSV(string input)
    {
        if (input.Contains(",") || input.Contains("\"") || input.Contains("\n"))
        {
            return "\"" + input.Replace("\"", "\"\"") + "\""; // doppelte Anführungszeichen nach CSV-Standard
        }
        return input;
    }


    // -------------------------------------------------------------------
    // Toter Code für Participant-Nummer-Übergabe und für Eyetracking
    // -------------------------------------------------------------------


    /*
    Die Methode GetArg() bekommt vom PythonSkript Daten übergeben und gibt sie als string zurück.
    Hier konkret wichtig, um den playerKey für die participantID abzufangen.
    */
    /*
    private string GetArg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
            if (args[i].StartsWith(name + "="))
                return args[i].Split('=')[1];
        return null;
    }
    */

    // Blickposition als Dummy (kann durch echtes EyeTracking ersetzt werden)
    // Vector2 gazePos = new Vector2(0.5f, 0.5f); // Todo: Platzhalter

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


}



