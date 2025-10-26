using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PSX
{
    public class Dithering : VolumeComponent, IPostProcessComponent
    {
        public BoolParameter enabled = new BoolParameter(false);
        
        public IntParameter patternIndex = new IntParameter(0);
        public FloatParameter ditherThreshold = new FloatParameter(512);
        public FloatParameter ditherStrength = new FloatParameter(1);
        public FloatParameter ditherScale = new FloatParameter(2);
        
        public bool IsActive() => enabled.value && ditherStrength.value > 0f;
        public bool IsTileCompatible() => false;
    }
}