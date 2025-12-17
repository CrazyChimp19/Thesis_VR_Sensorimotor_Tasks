using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using static UnityEngine.Rendering.DebugUI;

public class EventManager : MonoBehaviour
{
    [Header("Settings")]
    public float learningDirection; // LD in degrees
    public float startingPoint; // 0 for bottom floor, 1 for top floor

    [Header("Teleportation Provider")]
    public TeleportationProvider teleportationProvider;
    private TeleportationAnchor Bottom_LearningDirection;
    private TeleportationAnchor Top_LearningDirection;

    [Header("Instruction Panels")]
    [SerializeField] private GameObject panelLearningPhase1; // assign your prefab in the inspector
    [SerializeField] private GameObject panelLearningPhase2; // assign your prefab in the inspector
    [SerializeField] private GameObject panelTestTrials; // assign your prefab in the inspector
    [SerializeField] private GameObject panelTestTrialInstructor; // assign your prefab in the inspector
    [SerializeField] private GameObject teleportButtonPanel1;
    [SerializeField] private GameObject teleportButtonPanel2;
    [SerializeField] private Transform xrCamera; // XR Origin Camera
    [SerializeField] private float distanceInFront = 1f; // distance in front of the anchor
    [SerializeField] private float heightOffset = 1.5f; // Adjust in Inspector
    [SerializeField] private float distanceInFrontFace = 1f; // distance in front of the anchor
    [SerializeField] private float heightOffsetFace = 1.5f; // Adjust in Inspector

    [Header("Practice Trials")]
    [SerializeField] private List<GameObject> practiceObjects; // 8 objects in Inspector
    [SerializeField] private float holdDuration = 1f;
    [SerializeField] private float pointingToleranceDegrees = 15f;
    [SerializeField] private Transform pointingHand; // your VR controller
    [SerializeField] private TMP_Text testTrialText;

    private List<PracticeTrial> practiceTrials;
    private int currentPracticeTrialIndex = 0;
    private bool practiceRunning = false;
    private float holdTimer = 0f;

    [Header("XR Input")]
    private InputAction rightPrimaryButton;
    [SerializeField] private bool teleportPanelSpawned;

    [Header("Debug UI")]
    [SerializeField] private TMP_Text stageText;

    private int stage = 0;


    [System.Serializable]
    public class PracticeTrial
    {
        public GameObject facingObject;
        public GameObject pointingObject;

        public PracticeTrial(GameObject facing, GameObject pointing)
        {
            facingObject = facing;
            pointingObject = pointing;
        }
    }

    // Start is called before the first frame update
    void Awake()
    {
        // Assign anchors by name from the scene
        Bottom_LearningDirection = GameObject.Find("Bottom_LearningDirection").GetComponent<TeleportationAnchor>();
        Top_LearningDirection = GameObject.Find("Top_LearningDirection").GetComponent<TeleportationAnchor>();
        UpdateStageText();

        // Create a new InputAction for the right controller "A" button
        rightPrimaryButton = new InputAction(
            type: InputActionType.Button,
            binding: "<XRController>{RightHand}/primaryButton"
        );
    }

    // Update is called once per frame
    void Update()
    {
        //==== LEARNING PHASE ===\\
        // Stage 2 handles the first room in the learning phase
        if (stage == 2 && !teleportPanelSpawned)
        { 
            if (rightPrimaryButton.IsPressed())
            {
                holdTimer += Time.deltaTime;

                if (holdTimer >= holdDuration)
                {
                    SpawnPanelInFrontCamera(teleportButtonPanel1);
                    teleportPanelSpawned = true;
                }
            }
            else
            {
                if (holdTimer > 0f)
                holdTimer = 0f;
            }
        }

        if (stage == 4 && !teleportPanelSpawned)
        {
            if (rightPrimaryButton.IsPressed())
            {
                holdTimer += Time.deltaTime;

                if (holdTimer >= holdDuration)
                {
                    SpawnPanelInFrontCamera(teleportButtonPanel2);
                    teleportPanelSpawned = true;
}
            }
            else
            {
                if (holdTimer > 0f)
                    holdTimer = 0f;
            }
        }

        //==== PRACTICE TRIAL PHASE ===\\
        // Stage 6 handles the test trials
        if (stage == 6)
        {
            // Start practice session once
            if (!practiceRunning)
            {
                StartPracticeSession();
                practiceRunning = true;
            }
            else
            {
                // Only process input if there are remaining trials
                if (currentPracticeTrialIndex < practiceTrials.Count)
                {
                    PracticeTrial currentTrial = practiceTrials[currentPracticeTrialIndex];

                    if (rightPrimaryButton.IsPressed())
                    {
                        holdTimer += Time.deltaTime;

                        if (holdTimer >= holdDuration)
                        {
                            // Check if pointing is correct
                            bool correct = IsPointingCorrect(currentTrial);
                            Debug.Log(correct ? "Correct!" : "Incorrect!");

                            // Advance to next trial
                            currentPracticeTrialIndex++;
                            ShowNextPracticeTrial();

                            // Reset hold timer
                            holdTimer = 0f;
                        }
                    }
                    else
                    {
                        if (holdTimer > 0f)
                            holdTimer = 0f;
                    }
                }
            }
        }
    }

    //==== LEARNING PHASE ====\\
    private void TeleportToAnchor(TeleportationAnchor targetAnchor)
    {
        if (targetAnchor == null)
        {
            Debug.LogError("Target TeleportationAnchor is null!");
            return;
        }

        // Rotate the anchor itself
        targetAnchor.transform.rotation = Quaternion.Euler(0, learningDirection, 0);

        // Now teleport the player to the anchor's position and rotation
        TeleportRequest request = new TeleportRequest
        {
            destinationPosition = targetAnchor.transform.position,
            destinationRotation = targetAnchor.transform.rotation, // use the anchor's rotation
            matchOrientation = MatchOrientation.TargetUpAndForward
        };

        teleportationProvider.QueueTeleportRequest(request);
    }

    public void TeleportPlayerToStart(GameObject panel)
    {
        TeleportationAnchor targetAnchor = null;

        if (startingPoint == 0)
        {
            targetAnchor = Bottom_LearningDirection;
        }
        else if (startingPoint == 1)
        {
            targetAnchor = Top_LearningDirection;
        }
     
        TeleportToAnchor(targetAnchor);
        SpawnPanelInFrontAnchor(targetAnchor, panel);
        AddStage();
    }

    public void TeleportPlayerToNextFloor()
    {
        TeleportationAnchor targetAnchor = null;

        if (startingPoint == 1)
        {
            targetAnchor = Bottom_LearningDirection;
        }
        else if (startingPoint == 0)
        {
            targetAnchor = Top_LearningDirection;
        }

        TeleportToAnchor(targetAnchor);
        SpawnPanelInFrontAnchor(targetAnchor, panelLearningPhase2);
        AddStage();
    }

    // PRACTICE TRIALS \\
    private List<PracticeTrial> GeneratePracticeTrials(List<GameObject> objects)
    {
        List<PracticeTrial> trials = new List<PracticeTrial>();

        foreach (GameObject facingObj in objects)
        {
            List<GameObject> possibleTargets = new List<GameObject>(objects);
            possibleTargets.Remove(facingObj);

            ShuffleGameObjects(possibleTargets);

            for (int i = 0; i < 2; i++)
            {
                trials.Add(new PracticeTrial(facingObj, possibleTargets[i]));
            }
        }

        ShuffleTrials(trials);
        return trials;
    }

    private void StartPracticeSession()
    {
        practiceTrials = GeneratePracticeTrials(practiceObjects);
        currentPracticeTrialIndex = 0;
        holdTimer = 0f;
        MovePanelInFrontCamera(panelTestTrialInstructor);

        ShowNextPracticeTrial();
    }

    private void ShowNextPracticeTrial()
    {
        if (currentPracticeTrialIndex >= practiceTrials.Count)
        {
            EndPracticeSession();
            return;
        }

        // Always move panel in front of player
        MovePanelInFrontCamera(panelTestTrialInstructor);
        PracticeTrial trial = practiceTrials[currentPracticeTrialIndex];
        testTrialText.text = ($"Face {trial.facingObject.name}, point to {trial.pointingObject.name}");

        // Optional: highlight objects, outline cross-floor objects
    }

    private void EndPracticeSession()
    {
        practiceRunning = false;
        Debug.Log("Practice session complete.");
        AddStage(); // advance to the next stage of the experiment
    }

    private bool IsPointingCorrect(PracticeTrial trial)
    {
        Vector3 correctDir = (trial.pointingObject.transform.position - trial.facingObject.transform.position).normalized;
        Vector3 participantDir = pointingHand.forward; // assuming forward of controller is pointing

        float angle = Vector3.Angle(correctDir, participantDir);

        return angle <= pointingToleranceDegrees;
    }

    //==== GENERAL METHODS ====\\
    private void SpawnPanelInFrontAnchor(TeleportationAnchor targetAnchor, GameObject panelPrefab)
    {
        if (targetAnchor == null || panelPrefab == null)
        {
            Debug.LogError("Target anchor or panel prefab is null!");
            return;
        }

        // Calculate the position in front of the anchor
        Vector3 spawnPosition = targetAnchor.transform.position + targetAnchor.transform.forward * distanceInFront;

        // Apply height offset
        spawnPosition.y += heightOffset;

        // Keep the panel facing the same direction as the anchor
        Quaternion spawnRotation = targetAnchor.transform.rotation;

        // Instantiate the panel
        GameObject panelInstance = Instantiate(panelPrefab, spawnPosition, spawnRotation);
    }

    private void SpawnPanelInFrontCamera(GameObject panelPrefab)
    {
        Vector3 forward = xrCamera.forward;
        forward.y = 0;
        forward.Normalize();

        // Calculate spawn position in front of player at set height
        Vector3 spawnPos = xrCamera.position + forward * distanceInFrontFace;
        spawnPos.y = xrCamera.position.y + heightOffsetFace; // fixed height relative to player

        // Optional: make the panel face the player
        Quaternion spawnRot = Quaternion.LookRotation(forward);

        // Spawn the panel
        GameObject panel = Instantiate(panelPrefab, spawnPos, spawnRot);
    }

    private void MovePanelInFrontCamera(GameObject panel)
    {
        if(panel == null) return;

        // Get horizontal forward direction of camera (ignore vertical)
        Vector3 forward = xrCamera.forward;
        forward.y = 0;
        forward.Normalize();

        // Calculate new position in front of player at fixed height
        Vector3 spawnPos = xrCamera.position + forward * distanceInFrontFace;
        spawnPos.y = xrCamera.position.y + heightOffsetFace;
        panel.transform.position = spawnPos;

        // Make the panel face the player horizontally
        Vector3 lookDirection = xrCamera.position - spawnPos;
        lookDirection.y = 0; // ignore vertical to keep panel upright
        panel.transform.rotation = Quaternion.LookRotation(-lookDirection);
    }

    public GameObject GetCurrentLearningPanel()
    {
        switch (stage)
        {
            case 0:
                return panelLearningPhase1;
            case 4:
                return panelTestTrials;
            default:
                return null; // or some default panel
        }
    }

    public void AddStage()
    {
        stage ++;
        UpdateStageText();
        teleportPanelSpawned = false;
        holdTimer = 0f;
    }

    private void OnEnable()
    {
        rightPrimaryButton?.Enable();
    }

    private void OnDisable()
    {
        rightPrimaryButton?.Disable();
    }

    private void ShuffleGameObjects(List<GameObject> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rand = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[rand]) = (list[rand], list[i]);
        }
    }

    private void ShuffleTrials(List<PracticeTrial> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rand = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[rand]) = (list[rand], list[i]);
        }
    }

    //==== DEBUG ====\\
    //When deleting this, remove this method in AddStage() and Start()
    private void UpdateStageText()
    {
        if (stageText != null)
        {
            stageText.text = "Stage: " + stage; 
        }
    }
}
