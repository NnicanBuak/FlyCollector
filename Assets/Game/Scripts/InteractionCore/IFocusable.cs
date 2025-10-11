using UnityEngine;

public interface IFocusable
{
    void OnFocusHoverEnter();
    void OnFocusHoverExit();
    void OnFocusStart();
    void OnFocusEnd();


    Vector3 GetCameraPosition();


    Quaternion GetCameraRotation();


    bool IsCameraPositionLocked();


    Vector3 GetFocusCenter();


    int GetRequiredNestLevel();


    int GetTargetNestLevel();


    bool IsAvailableAtNestLevel(int currentLevel);
}