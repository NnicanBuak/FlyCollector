using UnityEngine;

namespace Audio
{
    public class AudioToggle : MonoBehaviour
    {
        public AudioSource src;

        public void Toggle()
        {
            if (src.isPlaying) src.Pause();
            else src.UnPause();
        }
    }
}