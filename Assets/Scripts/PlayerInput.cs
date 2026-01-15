using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    public TMP_InputField inputField; // Text field to show input
    public EventManager eventManager;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

    }

    // Called when number buttons are pressed
    public void AddNumber(string number)
    {
        inputField.text += number;
    }

    // Called when backspace is pressed
    public void Backspace()
    {
        if (inputField.text.Length > 0)
        {
            inputField.text = inputField.text.Substring(0, inputField.text.Length - 1);
        }
    }

    // Called when submit is pressed
    public void Submit()
    {
        if (int.TryParse(inputField.text, out int participantNumber))
        {
            eventManager.participant = participantNumber;
            Debug.Log("Participant Number Set: " + participantNumber);
            gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("Invalid participant number entered!");
        }
    }
}
