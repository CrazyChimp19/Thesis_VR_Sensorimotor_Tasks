using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    public TMP_InputField inputField; // Text field to show input
    public EventManager eventManager;

    [SerializeField] private GameObject panelLearningDirection;
    [SerializeField] private GameObject panelStartingPoint;
    [SerializeField] private GameObject panelSensorimotorAlignment;
    [SerializeField] private GameObject panelCondition;
    [SerializeField] private GameObject panelTrialOrder;

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
            panelLearningDirection.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Invalid participant number entered!");
        }
    }

    public void SetLearningDirection(int degrees)
    {
        eventManager.learningDirection = degrees;
        panelLearningDirection.SetActive(false);
        Debug.Log("Learning direction Set: " + degrees);
        panelStartingPoint.SetActive(true);
    }

    public void SetStartingPoint(int value)
    {
        eventManager.startingPoint = value;
        panelStartingPoint.SetActive(false);
        Debug.Log("Starting point Set: " + value);
        panelSensorimotorAlignment.SetActive(true);
    }

    public void SetSensorimotorAlignment(int value)
    {
        eventManager.sensorimotorAlignment = value;
        panelSensorimotorAlignment.SetActive(false);
        Debug.Log("Alignment set: " + value);
        panelCondition.SetActive(true);
    }

    public void SetCondition(int value)
    {
        eventManager.condition = value;
        panelCondition.SetActive(false);
        Debug.Log("Condition Set: " + value);
        panelTrialOrder.SetActive(true);
    }

    public void SetTrialOrder(int value)
    {
        eventManager.trialOrder = value;
        Debug.Log("Trial order Set: " + value);
        gameObject.SetActive(false);
    }
}
