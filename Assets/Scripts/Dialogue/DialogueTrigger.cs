using UnityEngine;
using UnityEngine.InputSystem;

public class DialogueTrigger : MonoBehaviour
{
    public Dialogue dialogueScript;
    private bool playerDetected;


    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.tag == "Player")
        {
            playerDetected = true;
            dialogueScript.ToggleIndicator(playerDetected);
        }
    }


    private void OnTriggerExit2D(Collider2D collision)
    {
         if(collision.tag == "Player")
        {
            playerDetected = false;
            dialogueScript.ToggleIndicator(playerDetected);
        }
    }

    private void Update()
    {
         if(playerDetected && WasInteractPressedThisFrame())
        {
            dialogueScript.StartDialogue();
        
        }
    }

    private static bool WasInteractPressedThisFrame()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            return true;
        }

        return GamepadUiNavigation.TryGetActiveGamepad(out var gamepad) && gamepad.buttonWest.wasPressedThisFrame;
    }
}
