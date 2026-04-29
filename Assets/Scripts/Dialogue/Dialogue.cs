using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.InputSystem;
using System;

public class Dialogue : MonoBehaviour
{
  public event Action DialogueEnded;
  public GameObject window;
  public GameObject indicator;
  public TMP_Text dialogueText;
  public List<string> dialogues;
  public float writingSpeed;
  [SerializeField] private GameObject transitionObjectOnEnd;
  [SerializeField] private GameObject triggerObjectOnEnd;
  [SerializeField] private string sceneToLoadOnEnd;
  [SerializeField] private string sceneSpawnPointName = "TargetSpawnPointName";
  [SerializeField] private bool allowRestartAfterEnd;
  private int index;
  private int charIndex;
  private bool started;
  private bool dialogueActive;
  private bool waitForNext;

  public bool IsRunning => dialogueActive;

    private void Awake()
    {
        ToggleIndicator(false);
        ToggleWindow(false);

        if (transitionObjectOnEnd != null)
        {
            transitionObjectOnEnd.SetActive(false);
        }

        if (triggerObjectOnEnd != null)
        {
            triggerObjectOnEnd.SetActive(false);
        }
    }

    private void ToggleWindow(bool show)
    {
      if (window != null)
      {
        window.SetActive(show);
      }
    }

  public void ToggleIndicator(bool show)
    {
      if (indicator != null)
      {
        indicator.SetActive(show);
      }
    }

  public void StartDialogue()
    {
        if(started)
           return;
        started = true;
        dialogueActive = true;
        ToggleWindow(true);
        ToggleIndicator(false);

        index = 0;

        GetDialogue(0);
    }

  private void GetDialogue(int i)
    {
        index = i;
        charIndex =0;
      if (dialogueText == null)
      {
        EndDialogue();
        return;
      }

      dialogueText.text = string.Empty;
        StartCoroutine(Writing());
    }

    public void EndDialogue()
    {
      dialogueActive = false;

      if (allowRestartAfterEnd)
      {
        started = false;
      }

        ToggleWindow(false);
        waitForNext = false;

        if (transitionObjectOnEnd != null)
        {
            transitionObjectOnEnd.SetActive(true);
        }

        if (triggerObjectOnEnd != null)
        {
            triggerObjectOnEnd.SetActive(true);
        }

        if (!string.IsNullOrWhiteSpace(sceneToLoadOnEnd))
        {
            SceneTransitionManager.Instance.LoadScene(sceneToLoadOnEnd, sceneSpawnPointName);
        }

        DialogueEnded?.Invoke();
    }

  IEnumerator Writing()
    {
        string currentDialogue = dialogues[index];
        dialogueText.text += currentDialogue[charIndex];
        charIndex++;

        if(charIndex < currentDialogue.Length)
        {
            yield return new WaitForSeconds(writingSpeed);

            StartCoroutine(Writing()); 
        }
        else
        {
            waitForNext = true;
        }
       
    }
  private void Update()
    {
        if(!started)
            return;
    if(waitForNext && WasInteractPressedThisFrame())
        {
           waitForNext = false;
           index++;
           if(index < dialogues.Count)
            {
               GetDialogue(index); 
            }
            else
            {
                EndDialogue();
            }
           
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
