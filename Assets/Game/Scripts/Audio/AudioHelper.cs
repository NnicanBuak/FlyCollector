using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Helper class for playing audio with automatic volume control
    /// All sounds automatically respect VolumeSlider settings
    /// </summary>
    public static class AudioHelper
    {
        /// <summary>
        /// Play one-shot sound at position (respects global volume via AudioListener)
        /// </summary>
        public static void PlayClipAtPoint(AudioClip clip, Vector3 position, float volumeScale = 1f)
        {
            if (clip == null)
            {
                Debug.LogWarning("[AudioHelper] AudioClip is null!");
                return;
            }

            // AudioSource.PlayClipAtPoint automatically respects AudioListener.volume
            AudioSource.PlayClipAtPoint(clip, position, volumeScale);
        }

        /// <summary>
        /// Play one-shot sound on existing AudioSource (respects global volume via AudioListener)
        /// </summary>
        public static void PlayOneShot(AudioSource source, AudioClip clip, float volumeScale = 1f)
        {
            if (source == null)
            {
                Debug.LogWarning("[AudioHelper] AudioSource is null!");
                return;
            }

            if (clip == null)
            {
                Debug.LogWarning("[AudioHelper] AudioClip is null!");
                return;
            }

            // AudioSource.PlayOneShot automatically respects AudioListener.volume
            source.PlayOneShot(clip, volumeScale);
        }

        /// <summary>
        /// Play sound on AudioSource (respects global volume via AudioListener)
        /// </summary>
        public static void Play(AudioSource source)
        {
            if (source == null)
            {
                Debug.LogWarning("[AudioHelper] AudioSource is null!");
                return;
            }

            // AudioSource.Play automatically respects AudioListener.volume
            source.Play();
        }

        /// <summary>
        /// Get current global volume (0.0 to 1.0)
        /// </summary>
        public static float GetGlobalVolume()
        {
            return AudioListener.volume;
        }

        /// <summary>
        /// Check if audio is muted globally
        /// </summary>
        public static bool IsMuted()
        {
            return AudioListener.volume <= 0.001f;
        }
    }
}
