using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class FreezeHMDPosition : MonoBehaviour
{
    public ActionBasedContinuousMoveProvider moveProvider; // optional, disable joystick
    public Transform cameraTransform; // usually the Main Camera under XR Origin

    private Vector3 frozenLocalPos;
    private bool isFrozen = false;

    public void Freeze()
    {
        if (cameraTransform == null) return;

        frozenLocalPos = cameraTransform.localPosition; // store position relative to XR Origin
        if (moveProvider != null) moveProvider.enabled = false;
        isFrozen = true;
    }

    public void Unfreeze()
    {
        isFrozen = false;
    }

    void LateUpdate()
    {
        if (isFrozen && cameraTransform != null)
        {
            // Keep the camera fixed relative to XR Origin
            cameraTransform.localPosition = frozenLocalPos;
        }
    }
}
