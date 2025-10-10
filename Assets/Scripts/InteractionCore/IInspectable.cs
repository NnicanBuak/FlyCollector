using UnityEngine;

public interface IInspectable
{
    void OnHoverEnter();
    void OnHoverExit();
    void OnInspect(Camera playerCamera);
    void OnInspectBegin();
    void OnInspectEnd(); 
    

    Quaternion GetInspectRotation();
    

    bool UsesCustomOrientation();
}