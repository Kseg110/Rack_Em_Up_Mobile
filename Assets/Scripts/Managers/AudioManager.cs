using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : Singleton<AudioManager>
{
    public AudioMixerGroup masterMixerGroup;
    public AudioMixerGroup musicMixerGroup;
    public AudioMixerGroup sfxMixerGroup;

    private AudioSource audioSource;

    void Start()
    {
        
    }

    void Update()
    {
        EnsureAudioListener();
    }

    private void EnsureAudioListener()
    {
        if (FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 0)
        {
            if (Camera.main != null && Camera.main.GetComponent<AudioListener>() == null)
            {
                Camera.main.gameObject.AddComponent<AudioListener>();
            }
            else if (Camera.main == null)
            {
                GameObject fallbackCam = new("FallbackCamera");
                fallbackCam.AddComponent<Camera>();
                fallbackCam.AddComponent<AudioListener>();
            }
        }
    }
}
