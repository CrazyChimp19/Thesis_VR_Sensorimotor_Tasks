using UnityEngine;

public class ButtonUI : MonoBehaviour
{
    private EventManager eventManager;

    private void Awake()
    {
        eventManager = FindObjectOfType<EventManager>();
    }

    public void AddStageOnButtonClicked()
    {
        if (eventManager != null)
        {
            eventManager.AddStage();
        }
        else
        {
            Debug.LogError("EventManager not found!");
        }
    }

    public void TeleportNextFloorOnButtonClicked()
    {
        eventManager.TeleportPlayerToNextFloor();
        Destroy(gameObject); 
    }

    public void TeleportStartOnButtonClicked()
    {
        GameObject panelToSpawn = eventManager.GetCurrentLearningPanel();

        if (panelToSpawn != null)
        {
            eventManager.TeleportPlayerToStart(panelToSpawn);
            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning("No panel assigned for this stage!");
        }
    }

    public void DestroyObject()
    {
        Destroy(gameObject);
    }

}
