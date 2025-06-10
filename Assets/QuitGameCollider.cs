using UnityEngine;

public class QuitOnTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Optionally check if it's the player by tag or component
        if (other.tag == "LevelExit")
        {
            Debug.Log("Quit trigger entered. Exiting game...");

            // Quit the application (does nothing in the editor)
            Application.Quit();

            // If you're testing in the Unity Editor
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}

