using UnityEngine;

public interface IInspectable
{
    void OnHoverEnter();
    void OnHoverExit();

    /// <summary>
    /// Called when player attempts to inspect this object.
    /// </summary>
    /// <returns>True if inspection is allowed, false if denied (e.g., bug not accessible)</returns>
    bool OnInspect(Camera playerCamera);

    void OnInspectBegin();
    void OnInspectEnd();


    Quaternion GetInspectRotation();


    bool UsesCustomOrientation();
}