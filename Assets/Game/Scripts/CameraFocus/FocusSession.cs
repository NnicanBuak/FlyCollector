using System;
using UnityEngine;

public class FocusSession : FocusSessionBase
{
    public FocusSession(Camera cam, Transform focusRoot, float flyTime, float rotationSpeed, Action onFinish)
        : base(cam, focusRoot, flyTime, rotationSpeed, onFinish)
    {
    }
}

