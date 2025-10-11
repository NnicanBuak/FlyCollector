﻿using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PSX
{
    [ExecuteInEditMode]
    public class PixelationController : MonoBehaviour
    {
        [SerializeField] protected VolumeProfile volumeProfile;
        [SerializeField] protected bool isEnabled = true;

        protected Pixelation pixelation;
        [SerializeField] protected float widthPixelation = 512;
        [SerializeField] protected float heightPixelation = 256;
        [SerializeField] protected float colorPrecision = 16;
        
        protected void Update()
        {
            this.SetParams();
        }

        protected void SetParams()
        {
            if (this.volumeProfile == null) return;
            if (this.pixelation == null) volumeProfile.TryGet<Pixelation>(out this.pixelation);
            if (this.pixelation == null) return;
            
            this.pixelation.enabled.value = this.isEnabled;
            this.pixelation.widthPixelation.value = this.widthPixelation;
            this.pixelation.heightPixelation.value = this.heightPixelation;
            this.pixelation.colorPrecision.value = this.colorPrecision;
        }
    }
}