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
    public float sensorimotorAlignment; // 90 or 315 degrees
    public float condition; // 0 for solid, 1 for transparent

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

    [Header("Feedback UI")]
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private float feedbackDuration = 1.5f;
    private float lastHorizontalError;
    private float lastVerticalError;

    [Header("XR Input")]
    private InputAction rightPrimaryButton;
    [SerializeField] private bool teleportPanelSpawned;

    [Header("Debug UI")]
    [SerializeField] private TMP_Text stageText;

    private int stage = 0;
    private int currentFloor; // 0 = bottom, 1 = top



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

        currentFloor = (int)startingPoint;
    }

    // Update is called once per frame
    void Update()
    {
        // ---- OUTLINES ---- //
        UpdateOutlines();

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
                panelTestTrialInstructor.SetActive(true);
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
                            ShowFeedback(correct);

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

        //==== EXPERIMENTAL PHASE ====\\
        if (stage == 7)
        {
            panelTestTrialInstructor.SetActive(false);
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

    // ---- PRACTICE TRIALS ----

    private List<PracticeTrial> GeneratePracticeTrials(List<GameObject> allObjects)
    {
        List<GameObject> bottomFloorObjects = new List<GameObject>();
        List<GameObject> topFloorObjects = new List<GameObject>();

        // Separate objects by tag
        foreach (GameObject obj in allObjects)
        {
            if (obj.CompareTag("Bottom")) bottomFloorObjects.Add(obj);
            else if (obj.CompareTag("Top")) topFloorObjects.Add(obj);
            else Debug.LogWarning("Object has no floor tag: " + obj.name);
        }

        List<PracticeTrial> trials = new List<PracticeTrial>();

        // Decide order of floors based on startingPoint
        List<GameObject> firstFloor = startingPoint == 0 ? bottomFloorObjects : topFloorObjects;
        List<GameObject> secondFloor = startingPoint == 0 ? topFloorObjects : bottomFloorObjects;

        // ---- First 8 trials: facing objects on first floor ----
        foreach (GameObject facing in firstFloor)
        {
            // Same-floor pointing
            List<GameObject> sameFloorTargets = new List<GameObject>(firstFloor);
            sameFloorTargets.Remove(facing);
            ShuffleGameObjects(sameFloorTargets);
            trials.Add(new PracticeTrial(facing, sameFloorTargets[0]));

            // Cross-floor pointing
            List<GameObject> crossFloorTargets = new List<GameObject>(secondFloor);
            ShuffleGameObjects(crossFloorTargets);
            trials.Add(new PracticeTrial(facing, crossFloorTargets[0]));
        }

        // ---- Second 8 trials: facing objects on second floor ----
        foreach (GameObject facing in secondFloor)
        {
            // Same-floor pointing
            List<GameObject> sameFloorTargets = new List<GameObject>(secondFloor);
            sameFloorTargets.Remove(facing);
            ShuffleGameObjects(sameFloorTargets);
            trials.Add(new PracticeTrial(facing, sameFloorTargets[0]));

            // Cross-floor pointing
            List<GameObject> crossFloorTargets = new List<GameObject>(firstFloor);
            ShuffleGameObjects(crossFloorTargets);
            trials.Add(new PracticeTrial(facing, crossFloorTargets[0]));
        }

        // ---- Randomize trials within each floor block ----
        List<PracticeTrial> firstFloorTrials = trials.GetRange(0, 8);
        List<PracticeTrial> secondFloorTrials = trials.GetRange(8, 8);
        ShuffleTrials(firstFloorTrials);
        ShuffleTrials(secondFloorTrials);

        // Merge back into final trial list
        List<PracticeTrial> finalTrials = new List<PracticeTrial>();
        finalTrials.AddRange(firstFloorTrials);
        finalTrials.AddRange(secondFloorTrials);

        return finalTrials;
    }

    private void StartPracticeSession()
    {
        practiceTrials = GeneratePracticeTrials(practiceObjects);
        currentPracticeTrialIndex = 0;
        holdTimer = 0f;

        // Move instruction panel in front of player
        MovePanelInFrontCamera(panelTestTrialInstructor);

        // Teleport player to starting floor
        TeleportToStartingFloor();

        ShowNextPracticeTrial();
    }

    private void ShowNextPracticeTrial()
    {
        if (currentPracticeTrialIndex >= practiceTrials.Count)
        {
            EndPracticeSession();
            return;
        }

        // After the first 8 trials, teleport to the other floor
        if (currentPracticeTrialIndex == 8)
        {
            // Subscribe to teleport completion with a local lambda
            teleportationProvider.endLocomotion += (locomotion) =>
            {
                MovePanelInFrontCamera(panelTestTrialInstructor);
                // Unsubscribe after teleport completes
                teleportationProvider.endLocomotion -= (locomotion) => { };
            };

            // Trigger teleport
            TeleportToOtherFloor();

            // Do not move the panel yet, it will move after teleport
        }
        else
        {
            // Normal trials: move panel immediately
            MovePanelInFrontCamera(panelTestTrialInstructor);
        }

        PracticeTrial trial = practiceTrials[currentPracticeTrialIndex];
        testTrialText.text = $"Face {trial.facingObject.name}, point to {trial.pointingObject.name}";
    }

    private void EndPracticeSession()
    {
        practiceRunning = false;
        Debug.Log("Practice session complete.");
        AddStage(); // advance to the next stage of the experiment
    }

    
    /// <summary>
    /// Checks whether the participant is pointing correctly from the facing object (Object A)
    /// toward the pointing target (Object B) using the VR hand.
    /// Returns true if both horizontal and vertical errors are within tolerance.
    /// </summary>
    private bool IsPointingCorrect(PracticeTrial trial)
    {
        // --- Validate input ---
        if (trial == null || trial.pointingObject == null || trial.facingObject == null || pointingHand == null)
            return false;

        // --- Find hand pivots ---
        Transform handStart = pointingHand.Find("handStartPivot");
        Transform handEnd = pointingHand.Find("handEndPivot");
        if (handStart == null || handEnd == null) return false;

        // --- Participant position ---
        Vector3 participantPos = xrCamera.position;

        // --- Compute vectors ---
        Vector3 vecPA = trial.facingObject.transform.position - participantPos;   // Participant → Object A (facing)
        Vector3 vecPB = trial.pointingObject.transform.position - participantPos; // Participant → Object B (target)
        Vector3 vecPD = handEnd.position - handStart.position;                    // Hand pointing direction

        // --- Horizontal error ---
        // Project vectors onto horizontal plane (XZ), ignoring vertical component
        Vector3 vecPA_H = new Vector3(vecPA.x, 0f, vecPA.z).normalized;
        Vector3 vecPB_H = new Vector3(vecPB.x, 0f, vecPB.z).normalized;
        Vector3 vecPD_H = new Vector3(vecPD.x, 0f, vecPD.z).normalized;

        // Compute angles relative to facing object
        float angleCorrectHoriz = Vector3.SignedAngle(vecPA_H, vecPB_H, Vector3.up);
        float anglePointingHoriz = Vector3.SignedAngle(vecPA_H, vecPD_H, Vector3.up);

        // Horizontal error = pointing - correct
        float horizontalError = anglePointingHoriz - angleCorrectHoriz;

        // Wrap horizontal error into -180 to 180 range to avoid crazy values 
        horizontalError = Mathf.DeltaAngle(0f, horizontalError);

        // --- Vertical error ---
        // Define a plane perpendicular to facing object for vertical measurement
        Vector3 right = Vector3.Cross(Vector3.up, vecPA).normalized;  // Right relative to facing
        Vector3 up = Vector3.Cross(vecPA, right).normalized;          // Up perpendicular to facing

        // How high or low the target is relative to facing object
        float angleCorrectVert = Mathf.Asin(Vector3.Dot(vecPB.normalized, up)) * Mathf.Rad2Deg;

        // How high or low the hand is pointing relative to facing object
        float anglePointingVert = Mathf.Asin(Vector3.Dot(vecPD.normalized, up)) * Mathf.Rad2Deg;

        // Difference between hand height and correct height
        float verticalError = anglePointingVert - angleCorrectVert;

        // Store for feedback/UI 
        lastHorizontalError = horizontalError;
        lastVerticalError = verticalError;

        // Debug output
        Debug.Log($"Horiz error: {horizontalError:F2}°, Vert error: {verticalError:F2}°");

        // Return true if within tolerance
        return Mathf.Abs(horizontalError) <= pointingToleranceDegrees
            && Mathf.Abs(verticalError) <= pointingToleranceDegrees;
    }

    private void ShowFeedback(bool correct)
    {
        feedbackText.gameObject.SetActive(true);

        string result = correct ? "Correct" : "Incorrect";
        feedbackText.color = correct ? Color.green : Color.red;

        feedbackText.text =
            $"{result}\n" +
            $"Horizontal error: {lastHorizontalError:F1}�\n" +
            $"Vertical error: {lastVerticalError:F1}�";

        StartCoroutine(HideFeedbackAfterDelay());
    }


    private IEnumerator HideFeedbackAfterDelay()
    {
        yield return new WaitForSeconds(feedbackDuration);
        feedbackText.gameObject.SetActive(false);
    }

    private void TeleportToStartingFloor()
    {
        TeleportationAnchor startAnchor = startingPoint == 0 ? Bottom_LearningDirection : Top_LearningDirection;
        TeleportToAnchor(startAnchor);
    }

    private void TeleportToOtherFloor()
    {
        TeleportationAnchor otherAnchor = startingPoint == 0 ? Top_LearningDirection : Bottom_LearningDirection;
        TeleportToAnchor(otherAnchor);

        // Update current floor after teleport
        currentFloor = currentFloor == 0 ? 1 : 0;
    }

    //==== EXPERIMENTAL PHASE ====\\


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

    private void UpdateOutlines()
    {
        // Get all top and bottom objects
        GameObject[] topObjects = GameObject.FindGameObjectsWithTag("Top");
        GameObject[] bottomObjects = GameObject.FindGameObjectsWithTag("Bottom");

        // Disable all outlines first
        foreach (GameObject obj in topObjects)
        {
            Outline outline = obj.GetComponent<Outline>();
            if (outline != null)
                outline.enabled = false;
        }
        foreach (GameObject obj in bottomObjects)
        {
            Outline outline = obj.GetComponent<Outline>();
            if (outline != null)
                outline.enabled = false;
        }

        // Enable the outlines depending on startingPoint and stage
        // ---- Learning phase outlines ----
        if (stage == 1 || stage == 2)
        {
            if (startingPoint == 0)
            {
                foreach (GameObject obj in topObjects)
                {
                    Outline outline = obj.GetComponent<Outline>();
                    if (outline != null)
                        outline.enabled = true;
                }
            }
            else
            {
                foreach (GameObject obj in bottomObjects)
                {
                    Outline outline = obj.GetComponent<Outline>();
                    if (outline != null)
                        outline.enabled = true;
                }
            }
        }

        if (stage == 3 || stage == 4)
        {
            if (startingPoint == 1)
            {
                foreach (GameObject obj in topObjects)
                {
                    Outline outline = obj.GetComponent<Outline>();
                    if (outline != null)
                        outline.enabled = true;
                }
            }
            else
            {
                foreach (GameObject obj in bottomObjects)
                {
                    Outline outline = obj.GetComponent<Outline>();
                    if (outline != null)
                        outline.enabled = true;
                }
            }
        }

        // ---- Practice trials outlines ----
        if (stage == 6)
        {
            bool participantOnBottom = currentFloor == 0;

            foreach (GameObject obj in practiceObjects)
            {
                Outline outline = obj.GetComponent<Outline>();
                if (outline == null) continue;

                if (participantOnBottom && obj.CompareTag("Top"))
                    outline.enabled = true;
                else if (!participantOnBottom && obj.CompareTag("Bottom"))
                    outline.enabled = true;
                else
                    outline.enabled = false;
            }
        }

        // ---- Disable outlines ----
        if (stage == 5 || stage == 7)
        {
            
            foreach (GameObject obj in topObjects)
            {
                Outline outline = obj.GetComponent<Outline>();
                if (outline != null)
                    outline.enabled = false;
            }
                   
            foreach (GameObject obj in bottomObjects)
            {
                Outline outline = obj.GetComponent<Outline>();
                if (outline != null)
                    outline.enabled = false;
            }
            
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
