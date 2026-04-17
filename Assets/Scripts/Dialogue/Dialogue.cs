using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Collections;

public class Dialogue : MonoBehaviour
{
  public GameObject window;
  public GameObject indicator;
  public TMP_Text dialogueText;
  public List<string> dialogues;
  public float writingSpeed;
  [SerializeField] private string sceneToLoadOnEnd;
    [SerializeField] private string sceneSpawnPointName = "TargetSpawnPointName";
  private int index;
  private int charIndex;
  private bool started;
  private bool waitForNext;

    private void Awake()
    {
        ToggleIndicator(false);
        ToggleWindow(false);
    }

    private void ToggleWindow(bool show)
    {
        window.SetActive(show);
    }

  public void ToggleIndicator(bool show)
    {
        indicator.SetActive(show);
    }

  public void StartDialogue()
    {
        if(started)
           return;
        started = true;
        ToggleWindow(true);
        ToggleIndicator(false);

        index = 0;

        GetDialogue(0);
    }

  private void GetDialogue(int i)
    {
        index = i;
        charIndex =0;
        dialogueText.text = string.Empty;
        StartCoroutine(Writing());
    }

    public void EndDialogue()
    {
        ToggleWindow(false);
        waitForNext = false;

        if (!string.IsNullOrWhiteSpace(sceneToLoadOnEnd))
        {
            SceneTransitionManager.Instance.LoadScene(sceneToLoadOnEnd, sceneSpawnPointName);
        }
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
        if(waitForNext && Input.GetKeyDown(KeyCode.E))
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
}
