using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.UIElements.Experimental;
using UnityEngine.XR.Interaction.Toolkit;
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.ParticleSystem;
using static UnityEngine.Rendering.DebugUI;

public class EventManager : MonoBehaviour
{
    [Header("Settings")]
    public int participant; //participant number
    public int learningDirection; // LD in degrees
    public int startingPoint; // 0 for bottom floor, 1 for top floor
    public int sensorimotorAlignment; // 90 or 315 degrees
    public int condition; // 0 for solid, 1 for transparent
    public int trialOrder; // 0 for 1,2,3,4 ; 1 for 4,3,2,1

    [Header("Teleportation Provider")]
    public TeleportationProvider teleportationProvider;
    private TeleportationAnchor Bottom_LearningDirection;
    private TeleportationAnchor Top_LearningDirection;

    [Header("Instruction Panels")]
    [SerializeField] private GameObject panelLearningPhase1; // assign your prefab in the inspector
    [SerializeField] private GameObject panelLearningPhase2; // assign your prefab in the inspector
    [SerializeField] private GameObject panelTestTrials; // assign your prefab in the inspector
    [SerializeField] private GameObject panelTestTrialInstructor; // assign your prefab in the inspector
    [SerializeField] private GameObject panelTakeABreak; // assign your prefab in the inspector
    [SerializeField] private GameObject panelRedoLearningPhase; // assign your prefab in the inspector
    [SerializeField] private GameObject teleportButtonPanel1;
    [SerializeField] private GameObject teleportButtonPanel2;
    [SerializeField] private GameObject teleportButtonStartEP;
    [SerializeField] private Transform xrCamera; // XR Origin Camera
    [SerializeField] private float distanceInFront = 1f; // distance in front of the anchor
    [SerializeField] private float heightOffset = 1.5f; // Adjust in Inspector
    [SerializeField] private float distanceInFrontFace = 1f; // distance in front of the anchor
    [SerializeField] private float heightOffsetFace = 1.5f; // Adjust in Inspector

    [Header("Vertical Floor Movement")]
    [SerializeField] private Transform xrOrigin; // XR Origin (Action-based or Device-based)
    [SerializeField] private ActionBasedSnapTurnProvider snapTurnProvider;
    private float bottomFloorY = 0.01000011f;
    private float topFloorY = 5.02f;
    [SerializeField] private float liftDuration = 3.0f; // seconds
    [SerializeField] private float liftRotationDuration = 3.0f;
    [SerializeField] private bool forceLearningDirectionDuringLift = true;
    [SerializeField] private Material transparentFloorMaterial;
    [SerializeField] private float liftDelayAfterRotation = 1f;

    private Dictionary<Renderer, Material[]> originalFloorMaterials = new();
    private bool isMoving = false; // tracks if the lift is running

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
    private GameObject pendingPracticePanel = null;

    [Header("Experimental Trials")]
    private List<ExperimentalTrial> experimentalTrials;
    private bool isTransparent = false;
    private int currentExperimentalTrialIndex = 0;
    private bool experimentRunning = false;
    private bool initialized = false;
    private bool redoLearning = false;

    [SerializeField] private GameObject trialInstructor90;
    [SerializeField] private GameObject trialInstructor315;
    [SerializeField] private Transform sensorimotorObject90;
    [SerializeField] private Transform sensorimotorObject315;
    [SerializeField] private TMP_Text text90Bottom;
    [SerializeField] private TMP_Text text90Top;
    [SerializeField] private TMP_Text text315Bottom;
    [SerializeField] private TMP_Text text315Top;

    private float startTime;
    private float pointingLatency;
    private int trialNumber = 1;


    [Header("Feedback UI")]
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private float feedbackDuration = 1.5f;
    private float lastHorizontalError;
    private float lastVerticalError;

    [Header("XR Input")]
    private InputAction rightPrimaryButton;
    private InputAction rightSecondaryButton;
    [SerializeField] private XRBaseController rightController;
    private bool panelSpawned;
    private bool EPPanelSpawned;

    [Header("Debug UI")]
    [SerializeField] private TMP_Text stageText;
    [SerializeField] private TMP_Text indexText;

    private int stage = 0;
    private int currentFloor; // 0 = bottom, 1 = top
    private int trialType;
    


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

    public class ExperimentalTrial
    {
        public GameObject facingObject;
        public GameObject pointingObject;
        public string trialType;

        public ExperimentalTrial(GameObject facing, GameObject pointing, string type)
        {
            facingObject = facing;
            pointingObject = pointing;
            trialType = type;
        }
    }

    public class PointingDataCombo
    {
        //data outputs
        public string participant;
        public string learningDirection;
        public string startingPoint;
        public string imaginedDirection;
        public string condition;
        public string trialType;
        public string trialOrder;
        public string trialNumber;
        public string facingObject;
        public string facingObjectLocation;
        public string targetObject;
        public string targetObjectLocation;
        public string latency;
        public string horizontalCorrectAngle;
        public string horizontalResponseAngle;
        public string horizontalPointingError;
        public string horizontalAbsPointingError;
        public string verticalCorrectAngle;
        public string verticalResponseAngle;
        public string verticalPointingError;
        public string verticalAbsPointingError;

        public PointingDataCombo(string parti, string learnDir, string startPoi, string imagDir, string con, string trialTyp,
            string trialOrd, string trialNum, string facing, string facingLoc, string target, string targetLoc, string lat,
            string horizCorrectAng, string horizResponseAng, string horizPointingErr, string horizAbsPointingErr,
            string vertCorrectAng, string vertResponseAng, string vertPointingErr, string vertAbsPointingErr)
        {
            participant = parti;
            learningDirection = learnDir;
            startingPoint = startPoi;
            imaginedDirection = imagDir;
            condition = con;
            trialType = trialTyp;
            trialOrder = trialOrd;
            trialNumber = trialNum;
            facingObject = facing;
            facingObjectLocation = facingLoc;
            targetObject = target;
            targetObjectLocation = targetLoc;
            latency = lat;
            horizontalCorrectAngle = horizCorrectAng;
            horizontalResponseAngle = horizResponseAng;
            horizontalPointingError = horizPointingErr;
            horizontalAbsPointingError = horizAbsPointingErr;
            verticalCorrectAngle = vertCorrectAng;
            verticalResponseAngle = vertResponseAng;
            verticalPointingError = vertPointingErr;
            verticalAbsPointingError = vertAbsPointingErr;
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

        rightSecondaryButton = new InputAction(
            type: InputActionType.Button,
            binding: "<XRController>{RightHand}/secondaryButton"
        );
    }

    // Update is called once per frame
    void Update()
    {
        // ---- OUTLINES ---- //
        //UpdateOutlines();

        //if (stage == 1 && !redoLearning)
        //{
        //    stage = +6;
        //    //TestAlignedTrials();
        //}

        //==== LEARNING PHASE ===\\
        // Stage 2 handles the first room in the learning phase
        if (stage == 2)
        {
            snapTurnProvider.enabled = false;
        }

        if (stage == 2 && !panelSpawned)
        { 
            if (rightPrimaryButton.IsPressed())
            {
                holdTimer += Time.deltaTime;

                if (holdTimer >= holdDuration)
                {
                    SpawnPanelInFrontCamera(teleportButtonPanel1);
                    panelSpawned = true;
                }
            }
            else
            {
                holdTimer = 0f;
            }
        }

        if (stage == 3 && !panelSpawned && !redoLearning)
        {
            if (!isMoving)
            {
                SpawnPanelInFrontCamera(panelLearningPhase2);
                panelSpawned = true;
            }
        }

        if (stage == 3 && redoLearning)
        {
            AddStage();
        }

        if (stage == 4 && !panelSpawned)
        {
            if (rightPrimaryButton.IsPressed())
            {
                holdTimer += Time.deltaTime;

                if (holdTimer >= holdDuration)
                {
                    SpawnPanelInFrontCamera(teleportButtonPanel2);
                    panelSpawned = true;
}
            }
            else
            {
                holdTimer = 0f;
            }
        }

        if (stage == 5 && !panelSpawned && !redoLearning)
        {
            if (!isMoving)
            {
                SpawnPanelInFrontCamera(panelTestTrials);
                panelSpawned = true;
            }
        }

        if (stage == 5 && redoLearning && !isMoving)
        {
            stage = 7;
            UpdateStageText();
            redoLearning = false;
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
                // ---- Spawn pending panel after lift ----
                if (pendingPracticePanel != null && !isMoving)
                {
                    MovePanelInFrontCamera(pendingPracticePanel);
                    pendingPracticePanel = null;
                }

                // Only process input if there are remaining trials
                if (currentPracticeTrialIndex < practiceTrials.Count && !isMoving)
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
                            rightController.SendHapticImpulse(0.5f, 0.05f); // amplitude, duration

                            // Reset hold timer
                            holdTimer = 0f;
                        }
                    }
                    else if (rightSecondaryButton.IsPressed())
                    {
                        holdTimer += Time.deltaTime;

                        if (holdTimer >= holdDuration)
                        {
                            //panelTestTrialInstructor.SetActive(false);
                            panelRedoLearningPhase.SetActive(true);
                            MovePanelInFrontCamera(panelRedoLearningPhase);
                        }
                    }
                    else
                    {
                        holdTimer = 0f;
                    }
                }
            }
        }

        


        //==== BREAK TIME ====\\
        // 5 minute break to prevent motion sickness
        if (stage == 7 || stage == 12)
        {
            if (!isMoving && !panelSpawned)
            {
                panelTestTrialInstructor.SetActive(false);
                panelTakeABreak.SetActive(true);
                MovePanelInFrontCamera(panelTakeABreak);
                panelSpawned = true;
            }

            if (rightPrimaryButton.IsPressed() && !isMoving && !EPPanelSpawned)
            {
                holdTimer += Time.deltaTime;

                if (holdTimer >= holdDuration)
                {
                    panelTakeABreak.SetActive(false);
                    SpawnPanelInFrontCamera(teleportButtonStartEP);
                    EPPanelSpawned = true;
                }
            }
            else
            {
                holdTimer = 0f;
            }
        }

        //==== EXPERIMENTAL PHASE ====\\

        //---- Turn off meshrenderer objects ----
        if (stage == 8) 
        { 
            // Disable the target objects (Turn of renderer instead of objects completely so the script can still find them within the scene)
            foreach (GameObject obj in practiceObjects) 
            { 
                MeshRenderer renderer = obj.GetComponent<MeshRenderer>(); 
                if (renderer != null) 
                { 
                    renderer.enabled = false; 
                } 
            }

            // Turn of meshrenderer child component of fan
            GameObject fan = GameObject.Find("fan");
            Transform rFan = fan.transform.Find("R Fan");

            if (rFan != null)
            {
                Renderer r = rFan.GetComponent<Renderer>();
                if (r != null)
                {
                    r.enabled = false;
                }
            }


            GameObject[] standObjects = GameObject.FindGameObjectsWithTag("Stand");

            // Disable all stands
            foreach (GameObject obj in standObjects)
            {
                MeshRenderer rendererStand = obj.GetComponent<MeshRenderer>();
                if (rendererStand != null)
                {
                    rendererStand.enabled = false;
                }
            }
        }

        //---- Initialize the correct experimental trial types for each stage ----
        if (stage == 8 || stage == 10 || stage == 13 || stage == 15)
        {
            if (!initialized)
            {
                // ---- Spawn or defer instruction panel ----
                if (!EPPanelSpawned)
                {
                    if (!isMoving)
                    {
                        MovePanelInFrontCamera(panelTestTrialInstructor);
                    }
                    else
                    {
                        pendingPracticePanel = panelTestTrialInstructor;
                    }

                    EPPanelSpawned = true;
                }

                // Turn floors transparent in transparent condition
                if (condition == 1 && !isTransparent)
                {
                    SetFloorsTransparent(true);
                    isTransparent = true;
                }

                // Generate the correct trials
                switch (stage) 
                {
                    case (8):
                        if (trialOrder == 0)
                        {
                            experimentalTrials = GenerateExperimentalAlignedTrials(practiceObjects);
                            trialType = 1;
                        }
                        else if (trialOrder == 1)
                        {
                            experimentalTrials = GenerateExperimentalMisalignedTrials(practiceObjects);
                            trialType = 4;
                        }
                        break;

                    case (10):
                        if (trialOrder == 0)
                        {
                            experimentalTrials = GenerateExperimentalSemiAlignedTrials(practiceObjects);
                            trialType = 2;
                        }
                        else if (trialOrder == 1)
                        {
                            experimentalTrials = GenerateExperimentalSemiMisalignedTrials(practiceObjects);
                            trialType = 3;
                        }
                        break;

                    case (13):
                        if (trialOrder == 0)
                        {
                            experimentalTrials = GenerateExperimentalSemiMisalignedTrials(practiceObjects);
                            trialType = 3;
                        }
                        else if (trialOrder == 1)
                        {
                            experimentalTrials = GenerateExperimentalSemiAlignedTrials(practiceObjects);
                            trialType = 2;
                        }
                        break;

                    case (15):
                        if (trialOrder == 0)
                        {
                            experimentalTrials = GenerateExperimentalMisalignedTrials(practiceObjects);
                            trialType = 4;
                        }
                        else if (trialOrder == 1)
                        {
                            experimentalTrials = GenerateExperimentalAlignedTrials(practiceObjects);
                            trialType = 1;
                        }
                        break;
                }
               
                currentExperimentalTrialIndex = 0;
                holdTimer = 0f;
                initialized = true;
            }
            else if(initialized)
            {
                AddStage();
                initialized = false;
                EPPanelSpawned = false;
                pendingPracticePanel = null;
            }
            
        }
        
        //---- Conduct the experimental trials ----
        if (stage == 9 || stage == 11 || stage == 14 || stage == 16)
        {
            UpdateIndexText();
            // Start practice session once
            if (!experimentRunning)
            {
                StartExperimentalSession();
                experimentRunning = true;
            }
            else
            {
                // Only process input if there are remaining trials
                if (currentExperimentalTrialIndex < experimentalTrials.Count && !isMoving)
                {
                    ExperimentalTrial currentTrial = experimentalTrials[currentExperimentalTrialIndex];

                    if (rightPrimaryButton.IsPressed())
                    {
                        holdTimer += Time.deltaTime;

                        if (holdTimer >= holdDuration)
                        {
                            // Calculate and log errors
                            pointingLatency = Time.time - startTime - holdDuration;
                            PointFromTo(currentTrial);

                            // Advance to next trial
                            currentExperimentalTrialIndex++;
                            trialNumber++;
                            ShowNextExperimentalTrial();
                            rightController.SendHapticImpulse(0.5f, 0.05f); // amplitude, duration

                            // Reset hold timer
                            holdTimer = 0f;
                        }
                    }
                    else
                    {
                        holdTimer = 0f;
                    }
                }
            }
        }

        if (stage == 17)
        {

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

        currentFloor = startingPoint;

        TeleportToAnchor(targetAnchor);
        SpawnPanelInFrontAnchor(targetAnchor, panel);
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
            int targetFloor = currentFloor == 0 ? 1 : 0;

            // Start vertical movement and pass the panel to spawn after lift
            StartCoroutine(MovePlayerVertically(targetFloor, panelTestTrialInstructor));
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
        TeleportPlayerToNextFloor();
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
            $"{result}";

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

    public void BackToLearningPhase()
    {
        if (stage == 7)
        {
            redoLearning = true;
        }

        if (stage < 7)
        {
            GameObject Anchor0 = GameObject.Find("Bottom_LearningDirection");
            GameObject Anchor1 = GameObject.Find("Top_LearningDirection");
            TeleportationAnchor targetAnchor = null;

            if (startingPoint == 0)
            {
                targetAnchor = Bottom_LearningDirection;
            }
            else if (startingPoint == 1)
            {
                targetAnchor = Top_LearningDirection;
            }
            
            currentFloor = startingPoint;
            TeleportToAnchor(targetAnchor);
            Anchor0.SetActive(false);
            Anchor1.SetActive(false);
            testTrialText.text = "";
            panelTestTrialInstructor.SetActive(false);
            practiceRunning = false;
        }

        stage = 2;
        Debug.Log("Stage set to: " + stage);       
        panelSpawned = false;
        UpdateStageText();
    }

    //==== EXPERIMENTAL PHASE ====\\
    private List<ExperimentalTrial> GenerateExperimentalAlignedTrials(List<GameObject> allObjects)
    {
        List<GameObject> bottomFloorObjects = new List<GameObject>();
        List<GameObject> topFloorObjects = new List<GameObject>();

        List<ExperimentalTrial> trialsFirst = new List<ExperimentalTrial>();
        List<ExperimentalTrial> trialsSecond = new List<ExperimentalTrial>();
        List<ExperimentalTrial> finalTrials = new List<ExperimentalTrial>();

        int repeatsPerFacing = 3;

        // Separate objects by tag
        foreach (GameObject obj in allObjects)
        {
            if (obj.CompareTag("Bottom")) bottomFloorObjects.Add(obj);
            else if (obj.CompareTag("Top")) topFloorObjects.Add(obj);
            else Debug.LogWarning("Object has no floor tag: " + obj.name);
        }

        // Decide order of floors based on startingPoint
        List<GameObject> firstBlock = startingPoint == 0 ? bottomFloorObjects : topFloorObjects;
        List<GameObject> secondBlock = startingPoint == 0 ? topFloorObjects : bottomFloorObjects;

        // --- Create lists of valid facing objects (exclude LD object) ---
        List<GameObject> facingFirstBlock = new List<GameObject>(firstBlock);
        List<GameObject> facingSecondBlock = new List<GameObject>(secondBlock);

        if (learningDirection == 0 && startingPoint == 0)
            facingSecondBlock.RemoveAt(0); // LD object removed only as facing
        else if (learningDirection == 0 && startingPoint == 1)
            facingFirstBlock.RemoveAt(0);
        else if (learningDirection == 225 && startingPoint == 0)
            facingFirstBlock.RemoveAt(0);
        else if (learningDirection == 225 && startingPoint == 1)
            facingSecondBlock.RemoveAt(0);


        // ---- TRIALS FIRST BLOCK ----
        foreach (GameObject facing in facingFirstBlock)
            {
                List<GameObject> possibleTargets = new List<GameObject>(firstBlock);
                possibleTargets.Remove(facing); // cannot point to itself

                int targetIndex = 0;
                for (int r = 0; r < repeatsPerFacing; r++)
                {
                    // Pick next target in list, cycle if needed
                    GameObject target = possibleTargets[targetIndex];
                    trialsFirst.Add(new ExperimentalTrial(facing, target, "Aligned"));

                    targetIndex = (targetIndex + 1) % possibleTargets.Count;
                }
            }

        // ---- TRIALS SECOND BLOCK ----
        foreach (GameObject facing in facingSecondBlock)
        {
            List<GameObject> possibleTargets = new List<GameObject>(secondBlock);
            possibleTargets.Remove(facing); // cannot point to itself

            int targetIndex = 0;
            for (int r = 0; r < repeatsPerFacing; r++)
            {
                GameObject target = possibleTargets[targetIndex];
                trialsSecond.Add(new ExperimentalTrial(facing, target, "Aligned"));

                targetIndex = (targetIndex + 1) % possibleTargets.Count;
            }
        }

        // Shuffle trials
        ShuffleTrials(trialsFirst);
        ShuffleTrials(trialsSecond);

        // Add all trials to final list
        finalTrials.AddRange(trialsFirst);
        finalTrials.AddRange(trialsSecond);

        return finalTrials;
    }

    private List<ExperimentalTrial> GenerateExperimentalMisalignedTrials(List<GameObject> allObjects)
    {
        List<GameObject> bottomFloorObjects = new List<GameObject>();
        List<GameObject> topFloorObjects = new List<GameObject>();

        List<ExperimentalTrial> trialsFirst = new List<ExperimentalTrial>();
        List<ExperimentalTrial> trialsSecond = new List<ExperimentalTrial>();
        List<ExperimentalTrial> finalTrials = new List<ExperimentalTrial>();

        int repeatsPerFacing = 3;

        // Separate objects by tag
        foreach (GameObject obj in allObjects)
        {
            if (obj.CompareTag("Bottom")) bottomFloorObjects.Add(obj);
            else if (obj.CompareTag("Top")) topFloorObjects.Add(obj);
            else Debug.LogWarning("Object has no floor tag: " + obj.name);
        }

        // Decide order of floors based on startingPoint
        List<GameObject> firstBlock = startingPoint == 0 ? topFloorObjects : bottomFloorObjects;
        List<GameObject> secondBlock = startingPoint == 0 ? bottomFloorObjects : topFloorObjects;

        // --- Create lists of valid facing objects (exclude LD object) ---
        List<GameObject> facingFirstBlock = new List<GameObject>(firstBlock);
        List<GameObject> facingSecondBlock = new List<GameObject>(secondBlock);

        if (learningDirection == 0 && startingPoint == 0)
            facingFirstBlock.RemoveAt(0); // LD object removed only as facing
        else if (learningDirection == 0 && startingPoint == 1)
            facingSecondBlock.RemoveAt(0);
        else if (learningDirection == 225 && startingPoint == 0)
            facingSecondBlock.RemoveAt(0);
        else if (learningDirection == 225 && startingPoint == 1)
            facingFirstBlock.RemoveAt(0);


        // ---- TRIALS FIRST BLOCK ----
        foreach (GameObject facing in facingFirstBlock)
        {
            List<GameObject> possibleTargets = new List<GameObject>(firstBlock);
            possibleTargets.Remove(facing); // cannot point to itself

            int targetIndex = 0;
            for (int r = 0; r < repeatsPerFacing; r++)
            {
                // Pick next target in list, cycle if needed
                GameObject target = possibleTargets[targetIndex];
                trialsFirst.Add(new ExperimentalTrial(facing, target, "Misaligned"));

                targetIndex = (targetIndex + 1) % possibleTargets.Count;
            }
        }

        // ---- TRIALS SECOND BLOCK ----
        foreach (GameObject facing in facingSecondBlock)
        {
            List<GameObject> possibleTargets = new List<GameObject>(secondBlock);
            possibleTargets.Remove(facing); // cannot point to itself

            int targetIndex = 0;
            for (int r = 0; r < repeatsPerFacing; r++)
            {
                GameObject target = possibleTargets[targetIndex];
                trialsSecond.Add(new ExperimentalTrial(facing, target, "Misaligned"));

                targetIndex = (targetIndex + 1) % possibleTargets.Count;
            }
        }

        // Shuffle trials
        ShuffleTrials(trialsFirst);
        ShuffleTrials(trialsSecond);

        // Add all trials to final list
        finalTrials.AddRange(trialsFirst);
        finalTrials.AddRange(trialsSecond);

        return finalTrials;
    }

    private List<ExperimentalTrial> GenerateExperimentalSemiAlignedTrials(List<GameObject> allObjects)
    {
        List<GameObject> bottomFloorObjects = new List<GameObject>();
        List<GameObject> topFloorObjects = new List<GameObject>();

        List<ExperimentalTrial> trialsFirst = new List<ExperimentalTrial>();
        List<ExperimentalTrial> trialsSecond = new List<ExperimentalTrial>();
        List<ExperimentalTrial> finalTrials = new List<ExperimentalTrial>();

        int repeatsPerFacing = 4;

        // Separate objects by tag
        foreach (GameObject obj in allObjects)
        {
            if (obj.CompareTag("Bottom")) bottomFloorObjects.Add(obj);
            else if (obj.CompareTag("Top")) topFloorObjects.Add(obj);
            else Debug.LogWarning("Object has no floor tag: " + obj.name);
        }

        // Decide order of floors based on startingPoint (targetobjects)
        List<GameObject> firstBlock = startingPoint == 0 ? topFloorObjects : bottomFloorObjects;
        List<GameObject> secondBlock = startingPoint == 0 ? bottomFloorObjects : topFloorObjects;

        // --- Create lists of valid facing objects (exclude LD object) ---
        List<GameObject> facingFirstBlock = new List<GameObject>(secondBlock);
        List<GameObject> facingSecondBlock = new List<GameObject>(firstBlock);

        if (learningDirection == 0 && startingPoint == 0)
            facingSecondBlock.RemoveAt(0); // LD object removed only as facing
        else if (learningDirection == 0 && startingPoint == 1)
            facingFirstBlock.RemoveAt(0);
        else if (learningDirection == 225 && startingPoint == 0)
            facingFirstBlock.RemoveAt(0);
        else if (learningDirection == 225 && startingPoint == 1)
            facingSecondBlock.RemoveAt(0);


        // ---- TRIALS FIRST BLOCK ----
        foreach (GameObject facing in facingFirstBlock)
        {
            List<GameObject> possibleTargets = new List<GameObject>(firstBlock);
            possibleTargets.Remove(facing); // cannot point to itself

            int targetIndex = 0;
            for (int r = 0; r < repeatsPerFacing; r++)
            {
                // Pick next target in list, cycle if needed
                GameObject target = possibleTargets[targetIndex];
                trialsFirst.Add(new ExperimentalTrial(facing, target, "Semi_Aligned"));

                targetIndex = (targetIndex + 1) % possibleTargets.Count;
            }
        }

        // ---- TRIALS SECOND BLOCK ----
        foreach (GameObject facing in facingSecondBlock)
        {
            List<GameObject> possibleTargets = new List<GameObject>(secondBlock);
            possibleTargets.Remove(facing); // cannot point to itself

            int targetIndex = 0;
            for (int r = 0; r < repeatsPerFacing; r++)
            {
                GameObject target = possibleTargets[targetIndex];
                trialsSecond.Add(new ExperimentalTrial(facing, target, "Semi_Aligned"));

                targetIndex = (targetIndex + 1) % possibleTargets.Count;
            }
        }

        // Shuffle trials
        ShuffleTrials(trialsFirst);
        ShuffleTrials(trialsSecond);

        // Add all trials to final list
        finalTrials.AddRange(trialsFirst);
        finalTrials.AddRange(trialsSecond);

        return finalTrials;
    }

    private List<ExperimentalTrial> GenerateExperimentalSemiMisalignedTrials(List<GameObject> allObjects)
    {
        List<GameObject> bottomFloorObjects = new List<GameObject>();
        List<GameObject> topFloorObjects = new List<GameObject>();

        List<ExperimentalTrial> trialsFirst = new List<ExperimentalTrial>();
        List<ExperimentalTrial> trialsSecond = new List<ExperimentalTrial>();
        List<ExperimentalTrial> finalTrials = new List<ExperimentalTrial>();

        int repeatsPerFacing = 4;

        // Separate objects by tag
        foreach (GameObject obj in allObjects)
        {
            if (obj.CompareTag("Bottom")) bottomFloorObjects.Add(obj);
            else if (obj.CompareTag("Top")) topFloorObjects.Add(obj);
            else Debug.LogWarning("Object has no floor tag: " + obj.name);
        }

        // Decide order of floors based on startingPoint (targetobjects)
        List<GameObject> firstBlock = startingPoint == 0 ? bottomFloorObjects : topFloorObjects;
        List<GameObject> secondBlock = startingPoint == 0 ? topFloorObjects : bottomFloorObjects;

        // --- Create lists of valid facing objects (exclude LD object) ---
        List<GameObject> facingFirstBlock = new List<GameObject>(secondBlock);
        List<GameObject> facingSecondBlock = new List<GameObject>(firstBlock);

        if (learningDirection == 0 && startingPoint == 0)
            facingFirstBlock.RemoveAt(0); // LD object removed only as facing
        else if (learningDirection == 0 && startingPoint == 1)
            facingSecondBlock.RemoveAt(0);
        else if (learningDirection == 225 && startingPoint == 0)
            facingSecondBlock.RemoveAt(0);
        else if (learningDirection == 225 && startingPoint == 1)
            facingFirstBlock.RemoveAt(0);

        // ---- TRIALS FIRST BLOCK ----
        foreach (GameObject facing in facingFirstBlock)
        {
            List<GameObject> possibleTargets = new List<GameObject>(firstBlock);
            possibleTargets.Remove(facing); // cannot point to itself

            int targetIndex = 0;
            for (int r = 0; r < repeatsPerFacing; r++)
            {
                // Pick next target in list, cycle if needed
                GameObject target = possibleTargets[targetIndex];
                trialsFirst.Add(new ExperimentalTrial(facing, target, "Semi_Misaligned"));

                targetIndex = (targetIndex + 1) % possibleTargets.Count;
            }
        }

        // ---- TRIALS SECOND BLOCK ----
        foreach (GameObject facing in facingSecondBlock)
        {
            List<GameObject> possibleTargets = new List<GameObject>(secondBlock);
            possibleTargets.Remove(facing); // cannot point to itself

            int targetIndex = 0;
            for (int r = 0; r < repeatsPerFacing; r++)
            {
                GameObject target = possibleTargets[targetIndex];
                trialsSecond.Add(new ExperimentalTrial(facing, target, "Semi_Misaligned"));

                targetIndex = (targetIndex + 1) % possibleTargets.Count;
            }
        }

        // Shuffle trials
        ShuffleTrials(trialsFirst);
        ShuffleTrials(trialsSecond);

        // Add all trials to final list
        finalTrials.AddRange(trialsFirst);
        finalTrials.AddRange(trialsSecond);

        return finalTrials;
    }

    private void StartExperimentalSession()
    {
        // Move instruction panel in front of player
        if (sensorimotorAlignment == 90)
        {
            trialInstructor90.SetActive(true);
        }
        else if (sensorimotorAlignment == 315)
        {
            trialInstructor315.SetActive(true);
        }

        ShowNextExperimentalTrial();
    }

    private void ShowNextExperimentalTrial()
    {
        if (currentExperimentalTrialIndex >= experimentalTrials.Count)
        {
            EndExperimentalSession();
            return;
        }

        if (!isMoving)
        {
            startTime = Time.time;
        }

        // After the first 8 trials, teleport to the other floor
        // WRITE IF STATEMENTS FOR THE CORRECT MOVING BETWEEN FLOORS!!
        switch (trialType)
        {
            case 1:
                if ((startingPoint == 0 && learningDirection == 0) || (startingPoint == 1 && learningDirection == 225)) 
                {
                    if (currentExperimentalTrialIndex == 12)
                    {
                        int targetFloor = currentFloor == 0 ? 1 : 0;

                        // Start vertical movement and pass the panel to spawn after lift
                        StartCoroutine(MovePlayerVertically(targetFloor, panelTestTrialInstructor));
                    }
                }
                else if ((startingPoint == 0 && learningDirection == 225) || (startingPoint == 1 && learningDirection == 0))
                {
                    if (currentExperimentalTrialIndex == 9)
                    {
                        int targetFloor = currentFloor == 0 ? 1 : 0;

                        // Start vertical movement and pass the panel to spawn after lift
                        StartCoroutine(MovePlayerVertically(targetFloor, panelTestTrialInstructor));
                    }
                }
                    break;

            case 2:
                if ((startingPoint == 0 && learningDirection == 0) || (startingPoint == 1 && learningDirection == 225))
                {
                    if (currentExperimentalTrialIndex == 16)
                    {
                        int targetFloor = currentFloor == 0 ? 1 : 0;

                        // Start vertical movement and pass the panel to spawn after lift
                        StartCoroutine(MovePlayerVertically(targetFloor, panelTestTrialInstructor));
                    }
                }
                else if ((startingPoint == 0 && learningDirection == 225) || (startingPoint == 1 && learningDirection == 0))
                {
                    if (currentExperimentalTrialIndex == 12)
                    {
                        int targetFloor = currentFloor == 0 ? 1 : 0;

                        // Start vertical movement and pass the panel to spawn after lift
                        StartCoroutine(MovePlayerVertically(targetFloor, panelTestTrialInstructor));
                    }
                }
                break;

            case 3:
                if ((startingPoint == 0 && learningDirection == 225) || (startingPoint == 1 && learningDirection == 0))
                {
                    if (currentExperimentalTrialIndex == 16)
                    {
                        int targetFloor = currentFloor == 0 ? 1 : 0;

                        // Start vertical movement and pass the panel to spawn after lift
                        StartCoroutine(MovePlayerVertically(targetFloor, panelTestTrialInstructor));
                    }
                }
                else if ((startingPoint == 0 && learningDirection == 0) || (startingPoint == 1 && learningDirection == 225))
                {
                    if (currentExperimentalTrialIndex == 12)
                    {
                        int targetFloor = currentFloor == 0 ? 1 : 0;

                        // Start vertical movement and pass the panel to spawn after lift
                        StartCoroutine(MovePlayerVertically(targetFloor, panelTestTrialInstructor));
                    }
                }
                break;

            case 4:
                if ((startingPoint == 0 && learningDirection == 225) || (startingPoint == 1 && learningDirection == 0))
                {
                    if (currentExperimentalTrialIndex == 12)
                    {
                        int targetFloor = currentFloor == 0 ? 1 : 0;

                        // Start vertical movement and pass the panel to spawn after lift
                        StartCoroutine(MovePlayerVertically(targetFloor, panelTestTrialInstructor));
                    }
                }
                else if ((startingPoint == 0 && learningDirection == 0) || (startingPoint == 1 && learningDirection == 225))
                {
                    if (currentExperimentalTrialIndex == 9)
                    {
                        int targetFloor = currentFloor == 0 ? 1 : 0;

                        // Start vertical movement and pass the panel to spawn after lift
                        StartCoroutine(MovePlayerVertically(targetFloor, panelTestTrialInstructor));
                    }
                }
                break;
        }     

        ExperimentalTrial trial = experimentalTrials[currentExperimentalTrialIndex];

        if (trialType == 1 || trialType == 2)
        {
            if (sensorimotorAlignment == 90)
            {
                if (trial.facingObject.tag == "Bottom")
                {
                    text90Bottom.text = $"Imagine you are facing the {trial.facingObject.name}, point to the {trial.pointingObject.name}";
                }
                else if (trial.facingObject.tag == "Top")
                {
                    text90Top.text = $"Imagine you are facing the {trial.facingObject.name}, point to the {trial.pointingObject.name}";
                }
            }
            else if (sensorimotorAlignment == 315)
            {
                if (trial.facingObject.tag == "Bottom")
                {
                    text315Bottom.text = $"Imagine you are facing the {trial.facingObject.name}, point to the {trial.pointingObject.name}";
                }
                else if (trial.facingObject.tag == "Top")
                {
                    text315Top.text = $"Imagine you are facing the {trial.facingObject.name}, point to the {trial.pointingObject.name}";
                }
            }
        }
        else if (trialType == 3 || trialType == 4)
        {
            if (sensorimotorAlignment == 90)
            {
                if (trial.facingObject.tag == "Bottom")
                {
                    text90Top.text = $"Imagine you are facing the {trial.facingObject.name}, point to the {trial.pointingObject.name}";
                }
                else if (trial.facingObject.tag == "Top")
                {
                    text90Bottom.text = $"Imagine you are facing the {trial.facingObject.name}, point to the {trial.pointingObject.name}";
                }
            }
            else if (sensorimotorAlignment == 315)
            {
                if (trial.facingObject.tag == "Bottom")
                {
                    text315Top.text = $"Imagine you are facing the {trial.facingObject.name}, point to the {trial.pointingObject.name}";
                }
                else if (trial.facingObject.tag == "Top")
                {
                    text315Bottom.text = $"Imagine you are facing the {trial.facingObject.name}, point to the {trial.pointingObject.name}";
                }
            }
        }
    }


    private void EndExperimentalSession()
    {
        if (stage == 17)
        {
            if (sensorimotorAlignment == 90)
            {
                trialInstructor90.SetActive(false);
            }
            else if (sensorimotorAlignment == 315)
            {
                trialInstructor315.SetActive(false);
            }
        }
        else
        {
            if (sensorimotorAlignment == 90)
            {
                text90Top.text = "";
                text90Bottom.text = "";
            }
            else if (sensorimotorAlignment == 315)
            {
                text315Top.text = "";
                text315Bottom.text = "";
            }
        }


        experimentRunning = false;
        Debug.Log("Experimental session complete.");
        TeleportPlayerToNextFloor();
    }


    //==== DATA LOGGING ====\\
    private void PointFromTo(ExperimentalTrial trial)
    {
        Vector3 participantPos = xrCamera.position; // standingTransform
        Vector3 facingPos = trial.facingObject.transform.position; //facingTransform
        Vector3 targetPos = trial.pointingObject.transform.position; // targetTransform
        Vector3 handStartPos = pointingHand.Find("handStartPivot").position;
        Vector3 handEndPos = pointingHand.Find("handEndPivot").position;


        //horizontal facing angle (starting from X-positive axis, incresing angle with counter clock-wise (0-360))
        Vector3 partToFace = facingPos - participantPos;

        //horizontal target angle
        Vector3 partToTargetFull = targetPos - participantPos;
        Vector3 partToTarget = partToTargetFull;

        // Imagined facing angle
        Vector3 partToImagine = Vector3.zero;

        if (sensorimotorAlignment == 90)
        {
            Vector3 imaginedFacing = sensorimotorObject90.position;
            partToImagine = imaginedFacing - participantPos;
        }
        else if (sensorimotorAlignment == 315)
        {
            Vector3 imaginedFacing = sensorimotorObject315.position;
            partToImagine = imaginedFacing - participantPos;
        }

        //horizontal pointing angle
        Vector3 handPointingFull = handEndPos - handStartPos;
        Vector3 handPointing = handPointingFull;

        // Convert to horizontal plane
        partToFace.y = partToTarget.y = partToImagine.y = handPointing.y = 0f;

        // Calculate signed angles
        float horizontalCorrectAngle = Vector3.SignedAngle(partToFace, partToTarget, Vector3.up);
        float horizontalResponseAngle = Vector3.SignedAngle(partToImagine, handPointing, Vector3.up);

        // Calculate angled errors between actual and response
        float horizErrorSigned = Mathf.DeltaAngle(horizontalCorrectAngle, horizontalResponseAngle);
        float horizErrorAbs = Mathf.Abs(horizErrorSigned);

        // Calculate vertical error
        float verticalCorrectAngle = CalculateVerticalSignedAngle(partToTargetFull);
        float verticalResponseAngle = CalculateVerticalSignedAngle(handPointingFull);
        float verticalErrorSigned = verticalResponseAngle - verticalCorrectAngle;
        float verticalErrorAbs = Mathf.Abs(verticalErrorSigned);

        // ---- DEBUG --- Log the errors for stages 9, 11, 14, 16 ---
        if (stage == 9 || stage == 11 || stage == 14 || stage == 16)
        {
            Debug.Log($"Trial {trialNumber}: horizErrorSigned = {horizErrorSigned}, horizErrorAbs = {horizErrorAbs}, vertErrorSigned = {verticalErrorSigned}, vertErrorAbs = {verticalErrorAbs}, latency = {pointingLatency}");
        }

        PointingDataCombo SavingDataCombo = new PointingDataCombo(
            participant.ToString(),
            learningDirection.ToString(),
            startingPoint.ToString(),
            sensorimotorAlignment.ToString(),
            condition.ToString(),
            trial.trialType.ToString(),
            trialOrder.ToString(),
            trialNumber.ToString(),
            trial.facingObject.name.ToString(),
            trial.facingObject.tag.ToString(),
            trial.pointingObject.name.ToString(),
            trial.pointingObject.tag.ToString(),
            pointingLatency.ToString(CultureInfo.InvariantCulture),
            horizontalCorrectAngle.ToString(CultureInfo.InvariantCulture),
            horizontalResponseAngle.ToString(CultureInfo.InvariantCulture),
            horizErrorSigned.ToString(CultureInfo.InvariantCulture),
            horizErrorAbs.ToString(CultureInfo.InvariantCulture),
            verticalCorrectAngle.ToString(CultureInfo.InvariantCulture),
            verticalResponseAngle.ToString(CultureInfo.InvariantCulture),
            verticalErrorSigned.ToString(CultureInfo.InvariantCulture),
            verticalErrorAbs.ToString(CultureInfo.InvariantCulture));

        //Export data from Unity to CSV file in Asset/Resource folder
#if UNITY_EDITOR
        string filePath = @"Assets/Resources/ExperimentResults/Pointing_saved_data.csv";
#else
        string filePath = Application.persistentDataPath + "/Pointing_saved_data.csv";
#endif

        // Write header ONCE
        if (!File.Exists(filePath))
        {
            File.WriteAllText(filePath,
                "participant,learningDirection,startingPoint,imaginedDirection,condition," +
                "trialType,trialOrder,trialNumber,facingObject,facingObjectLocation," +
                "targetObject,targetObjectLocation,latency," +
                "horizontalCorrectAngle,horizontalResponseAngle,horizontalError,horizontalAbsError," +
                "verticalCorrectAngle,verticalResponseAngle,verticalError,verticalAbsError\n");
        }

        File.AppendAllText(filePath, SavingDataCombo.participant + "," + SavingDataCombo.learningDirection
            + "," + SavingDataCombo.startingPoint + "," + SavingDataCombo.imaginedDirection
            + "," + SavingDataCombo.condition + "," + SavingDataCombo.trialType
            + "," + SavingDataCombo.trialOrder + "," + SavingDataCombo.trialNumber
            + "," + SavingDataCombo.facingObject + "," + SavingDataCombo.facingObjectLocation
            + "," + SavingDataCombo.targetObject + "," + SavingDataCombo.targetObjectLocation
            + "," + SavingDataCombo.latency + "," + SavingDataCombo.horizontalCorrectAngle + "," + SavingDataCombo.horizontalResponseAngle
            + "," + SavingDataCombo.horizontalPointingError + "," + SavingDataCombo.horizontalAbsPointingError
            + "," + SavingDataCombo.verticalCorrectAngle + "," + SavingDataCombo.verticalResponseAngle
            + "," + SavingDataCombo.verticalPointingError + "," + SavingDataCombo.verticalAbsPointingError
             + "\n");
    }

    private float CalculateVerticalSignedAngle(Vector3 Direction)
    {
        float verticalAngle = Vector3.Angle(Direction, Vector3.up);
        if (Direction.y < 0)
        {
            verticalAngle = -verticalAngle;
        }
        if (verticalAngle <= 0)
        {
            verticalAngle = verticalAngle + 90f;
        }
        else
        {
            verticalAngle = 90f - verticalAngle;
        }

        return verticalAngle;
    }

    //==== GENERAL METHODS ====\\
    private IEnumerator TurnParticipantLDGradual(float targetYaw, float turnDuration)
    {
        float startYaw = xrCamera.eulerAngles.y;
        float totalDeltaYaw = Mathf.DeltaAngle(startYaw, targetYaw);

        float elapsed = 0f;

        while (elapsed < turnDuration)
        {
            float t = elapsed / turnDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            float currentDelta = totalDeltaYaw * t;
            float previousDelta = totalDeltaYaw * Mathf.SmoothStep(
                0f, 1f, (elapsed - Time.deltaTime) / turnDuration
            );

            float deltaThisFrame = currentDelta - previousDelta;

            xrOrigin.RotateAround(
                xrCamera.position,
                Vector3.up,
                deltaThisFrame
            );

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Final correction (guarantees exact alignment)
        float finalDelta = Mathf.DeltaAngle(xrCamera.eulerAngles.y, targetYaw);
        xrOrigin.RotateAround(
            xrCamera.position,
            Vector3.up,
            finalDelta
        );
    }

    private IEnumerator MovePlayerVertically(int targetFloor, GameObject panelToSpawnAfterLift = null)
    {
        isMoving = true;

        float targetY = targetFloor == 0 ? bottomFloorY : topFloorY;

        Vector3 startPos = xrOrigin.position;
        Vector3 targetPos = new Vector3(startPos.x, targetY, startPos.z);

        teleportationProvider.enabled = false;
        //snapTurnProvider.enabled = false;

        // ---- Phase 1: rotate ONCE to learning direction ----
        //if (stage < 9) 
        //{
        //    yield return StartCoroutine(TurnParticipantLDGradual(learningDirection, liftRotationDuration));


        //    // ---- Wait so participant registers orientation ----
        //    yield return new WaitForSeconds(liftDelayAfterRotation);
        //}

        // ---- Wait so participant registers orientation ----
        yield return new WaitForSeconds(liftDelayAfterRotation);


        // ---- Phase 2: make floors transparent ----
        if (condition == 1 && stage <= 7)
        {
            SetFloorsTransparent(true);
        }
        else if (condition == 0)
        {
            SetFloorsTransparent(true);
        }
            
        float elapsed = 0f;

        while (elapsed < liftDuration)
        {
            float t = elapsed / liftDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            xrOrigin.position = Vector3.Lerp(startPos, targetPos, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        xrOrigin.position = targetPos;
        currentFloor = targetFloor;

        // ---- Restore floors ----
        // ---- Phase 2: make floors transparent ----
        if (condition == 1 && stage <= 7)
        {
            SetFloorsTransparent(false);
        }
        else if (condition == 0)
        {
            SetFloorsTransparent(false);
        }

        teleportationProvider.enabled = true;
        //snapTurnProvider.enabled = true;
        isMoving = false;

        // ---- Spawn panel after lift ----
        if (panelToSpawnAfterLift != null)
        {
            MovePanelInFrontCamera(panelToSpawnAfterLift);
        }

        startTime = Time.time;
    }

    public void TeleportPlayerToNextFloor()
    {
        isMoving = true;
        int targetFloor = currentFloor == 0 ? 1 : 0;
        Debug.Log($"targetfloor: {targetFloor}, currentfloor: {currentFloor}");
        StartCoroutine(MovePlayerVertically(targetFloor));

        AddStage();
    }

    private void SetFloorsTransparent(bool transparent)
    {
        GameObject[] allFloors = GameObject.FindGameObjectsWithTag("Transparent");
        
        foreach (GameObject floor in allFloors)
        {
            Renderer r = floor.GetComponent<Renderer>();
            if (r == null) continue;

            if (transparent)
            {
                if (!originalFloorMaterials.ContainsKey(r))
                    originalFloorMaterials[r] = r.materials;

                Material[] transparentMats = new Material[r.materials.Length];
                for (int i = 0; i < transparentMats.Length; i++)
                    transparentMats[i] = transparentFloorMaterial;

                r.materials = transparentMats;
            }
            else
            {
                if (originalFloorMaterials.TryGetValue(r, out var mats))
                    r.materials = mats;
            }
        }

        if (!transparent)
            originalFloorMaterials.Clear();
    }


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
        panelSpawned = false;
        holdTimer = 0f;
    }

    private void OnEnable()
    {
        rightPrimaryButton.Enable();
        rightSecondaryButton.Enable();
    }

    private void OnDisable()
    {
        rightPrimaryButton.Disable();
        rightSecondaryButton.Disable();
    }

    private void ShuffleGameObjects(List<GameObject> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rand = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[rand]) = (list[rand], list[i]);
        }
    }

    private void ShuffleTrials<T>(List<T> list)
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

    private void UpdateIndexText()
    {
        if (indexText != null)
        {
            indexText.text = "Index: " + currentExperimentalTrialIndex + " Count: " + experimentalTrials.Count;
        }
    }

    private void TestAlignedTrials()
    {
        List<ExperimentalTrial> alignedTrials = GenerateExperimentalAlignedTrials(practiceObjects);

        for (int i = 0; i < alignedTrials.Count; i++)
        {
            ExperimentalTrial t = alignedTrials[i];
            Debug.Log($"Trial {i + 1}: Facing {t.facingObject.name} -> Pointing {t.pointingObject.name} (Type: {t.trialType})");
        }
    }
    private void TestMisalignedTrials()
    {
        List<ExperimentalTrial> misalignedTrials = GenerateExperimentalMisalignedTrials(practiceObjects);

        for (int i = 0; i < misalignedTrials.Count; i++)
        {
            ExperimentalTrial t = misalignedTrials[i];
            Debug.Log($"Trial {i + 1}: Facing {t.facingObject.name} -> Pointing {t.pointingObject.name} (Type: {t.trialType})");
        }
    }
    private void TestSemiAlignedTrials()
    {
        List<ExperimentalTrial> semialignedTrials = GenerateExperimentalSemiAlignedTrials(practiceObjects);

        for (int i = 0; i < semialignedTrials.Count; i++)
        {
            ExperimentalTrial t = semialignedTrials[i];
            Debug.Log($"Trial {i + 1}: Facing {t.facingObject.name} -> Pointing {t.pointingObject.name} (Type: {t.trialType})");
        }
    }
    private void TestSemiMisalignedTrials()
    {
        List<ExperimentalTrial> semiMisalignedTrials = GenerateExperimentalSemiMisalignedTrials(practiceObjects);

        for (int i = 0; i < semiMisalignedTrials.Count; i++)
        {
            ExperimentalTrial t = semiMisalignedTrials[i];
            Debug.Log($"Trial {i + 1}: Facing {t.facingObject.name} -> Pointing {t.pointingObject.name} (Type: {t.trialType})");
        }
    }
}