using System;
using System.Reflection;
using UnityEngine;

public class FocusSession
{
    private readonly Camera cam;
    private readonly Transform focusRoot;
    private readonly float flyTime;
    private readonly float rotationSpeed;
    private readonly Action onFinish;


    private bool entering;
    private bool exiting;
    private bool finished;


    private Vector3 enterStartPos;
    private Quaternion enterStartRot;

    private Vector3 enterEndPos;
    private Quaternion enterEndRot;

    private Vector3 exitStartPos;
    private Quaternion exitStartRot;

    private float t;


    private FocusableObject focusable;
    private Transform resolvedFocusPoint;

    public bool IsAnimating => entering || exiting;

    public FocusSession(Camera cam, Transform focusRoot, float flyTime, float rotationSpeed, Action onFinish)
    {
        this.cam = cam;
        this.focusRoot = focusRoot;
        this.flyTime = Mathf.Max(0.0001f, flyTime);
        this.rotationSpeed = rotationSpeed;
        this.onFinish = onFinish;
    }

    public void Begin()
    {
        if (cam == null || focusRoot == null) return;

        Debug.LogError($"[FocusSession] Starting focus movement to {focusRoot.name}");

        enterStartPos = cam.transform.position;
        enterStartRot = cam.transform.rotation;


        focusable = focusRoot.GetComponentInParent<FocusableObject>();

        if (focusable != null)
        {

            enterEndPos = focusable.GetCameraPosition();
            enterEndRot = focusable.GetCameraRotation();


            focusable.OnFocusStart();
        }
        else
        {

            resolvedFocusPoint = ResolveFocusPoint(focusRoot);

            if (resolvedFocusPoint != null)
            {
                enterEndPos = resolvedFocusPoint.position;
                enterEndRot = resolvedFocusPoint.rotation;
            }
            else
            {

                enterEndPos = focusRoot.position;
                enterEndRot = focusRoot.rotation;
            }
        }

        t = 0f;
        entering = true;
        exiting = false;
        finished = false;
    }

    public void Update()
    {
        if (finished || cam == null) return;

        if (entering)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / flyTime));
            cam.transform.position = Vector3.Lerp(enterStartPos, enterEndPos, k);
            cam.transform.rotation = Quaternion.Slerp(enterStartRot, enterEndRot, k);

            if (t >= flyTime)
            {
                entering = false;
                t = 0f;
            }
            return;
        }

        if (exiting)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / flyTime));
            cam.transform.position = Vector3.Lerp(exitStartPos, enterStartPos, k);
            cam.transform.rotation = Quaternion.Slerp(exitStartRot, enterStartRot, k);

            if (t >= flyTime)
            {
                exiting = false;
                finished = true;


                focusable?.OnFocusEnd();

                onFinish?.Invoke();
            }
            return;
        }




    }


    public void RequestExit()
    {
        if (finished || entering || exiting || cam == null) return;

        exitStartPos = cam.transform.position;
        exitStartRot = cam.transform.rotation;

        t = 0f;
        exiting = true;
    }

    public bool IsFinished() => finished;

    public GameObject GetTarget()
    {
        if (focusable != null) return focusable.gameObject;
        if (resolvedFocusPoint != null) return resolvedFocusPoint.gameObject;
        return focusRoot != null ? focusRoot.gameObject : null;
    }




    private static Transform ResolveFocusPoint(Transform root)
    {
        if (root == null) return null;

        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var comps = root.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (var c in comps)
        {
            if (c == null) continue;
            var type = c.GetType();


            var p1 = type.GetProperty("FocusPoint", flags);
            if (p1 != null && typeof(Transform).IsAssignableFrom(p1.PropertyType))
            {
                var v = p1.GetValue(c, null) as Transform;
                if (v != null) return v;
            }
            var p2 = type.GetProperty("focusPoint", flags);
            if (p2 != null && typeof(Transform).IsAssignableFrom(p2.PropertyType))
            {
                var v = p2.GetValue(c, null) as Transform;
                if (v != null) return v;
            }


            var f1 = type.GetField("FocusPoint", flags);
            if (f1 != null && typeof(Transform).IsAssignableFrom(f1.FieldType))
            {
                var v = f1.GetValue(c) as Transform;
                if (v != null) return v;
            }
            var f2 = type.GetField("focusPoint", flags);
            if (f2 != null && typeof(Transform).IsAssignableFrom(f2.FieldType))
            {
                var v = f2.GetValue(c) as Transform;
                if (v != null) return v;
            }


            var m = type.GetMethod("GetFocusPoint", flags, null, Type.EmptyTypes, null);
            if (m != null && typeof(Transform).IsAssignableFrom(m.ReturnType))
            {
                var v = m.Invoke(c, null) as Transform;
                if (v != null) return v;
            }
        }


        var child = root.Find("FocusPoint");
        if (child != null) return child;

        return null;
    }
    
    public void ForceFinishWithoutReturn()
    {
        if (finished || cam == null) return;
        entering = false;
        exiting = false;
        finished = true;

        focusable?.OnFocusEnd();
        onFinish?.Invoke();
    }

}
