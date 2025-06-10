using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

public class VRMovementController : MonoBehaviour
{
    public InputActionProperty moveAction;          // Left joystick input
    public InputActionProperty rightTriggerAction;  // Right trigger input
    public GameObject mapCanvas; // city map

    public CharacterController characterController;
    public Transform cameraTransform;

    // stuff to swtich scences with
    public float sceneTimeout = 120f; // Time in seconds before auto-switch
    private float timer = 0f;
    private bool sceneChanged = false;


    private void Update()
    {
        
        // returns if the scene was changed or not
        if (sceneChanged) return;

        // checks if the timer has run off and if it does it returns that the scene has to be changed
        timer += Time.deltaTime;
        if (timer >= sceneTimeout)
        {
            sceneChanged = true;
            SceneManager.LoadScene(1);
        }
        

        float triggerValue = rightTriggerAction.action.ReadValue<float>();
        bool isHoldingTrigger = triggerValue >= 0.1f;
        
        if (isHoldingTrigger)
            return; // Don't move while holding trigger

        Vector2 input = moveAction.action.ReadValue<Vector2>();
        Vector3 move = cameraTransform.forward * input.y + cameraTransform.right * input.x;
        move.y = 0;

        characterController.Move(move * Time.deltaTime * 2f);
        

    }
    private void OnTriggerEnter(Collider other)
    {
        if (sceneChanged) return;

        if (other.tag == "LevelExit")
        {
            SceneManager.LoadScene(1);
        }
    }

}
