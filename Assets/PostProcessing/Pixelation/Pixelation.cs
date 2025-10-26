using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PSX
{
    public class Pixelation : VolumeComponent, IPostProcessComponent
    {
        public BoolParameter enabled = new BoolParameter(false);
        
        public FloatParameter widthPixelation = new FloatParameter(512);
        public FloatParameter heightPixelation = new FloatParameter(512);
        public FloatParameter colorPrecision = new FloatParameter(32.0f);
        
        public bool IsActive() => enabled.value;
        public bool IsTileCompatible() => false;
    }
}