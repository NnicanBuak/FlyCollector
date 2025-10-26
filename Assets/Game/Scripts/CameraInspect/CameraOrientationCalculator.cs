using UnityEngine;

public static class CameraOrientationCalculator
{
    /// <summary>
    /// Calculates the rotation needed for an object to face the camera.
    /// </summary>
    public static Quaternion CalculateCameraFacingRotation(Camera cam, Transform objectTransform, InspectableObject inspectable)
    {
        if (cam == null || objectTransform == null || inspectable == null)
            return objectTransform.rotation;

        if (inspectable.ShouldFaceCamera())
        {
            Vector3 directionToCamera = (cam.transform.position - objectTransform.position).normalized;
            Vector3 forward = -directionToCamera;
            Vector3 objectUp = Vector3.up;

            // Ensure the 'up' vector is not parallel to the 'forward' vector
            if (Vector3.Dot(forward, objectUp) > 0.99f || Vector3.Dot(forward, objectUp) < -0.99f)
            {
                objectUp = Vector3.right;
            }
            
            Quaternion lookRotation = Quaternion.LookRotation(forward, objectUp);
            
            // Adjust rotation based on the user-defined facing axis
            Quaternion axisCorrection = Quaternion.FromToRotation(inspectable.GetFacingAxis(), Vector3.forward);
            
            return lookRotation * Quaternion.Inverse(axisCorrection);
        }

        if (inspectable.UsesCustomOrientation())
        {
            return inspectable.GetInspectRotation();
        }

        return objectTransform.rotation;
    }
}

