using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Range(0, 1)] public float masterVolume = 1f;
    [Range(0, 1)] public float musicVolume = 1f;
    [Range(0, 1)] public float sfxVolume = 1f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = value;
        AudioListener.volume = masterVolume;
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = value;
    }

    public void SetSFXVolume(float value)
    {
        sfxVolume = value;
    }

    // ²¥·Å½Ó¿Ú
    public void PlaySFX(AudioSource source)
    {
        source.volume = sfxVolume * masterVolume;
        source.Play();
    }

    public void PlayMusic(AudioSource source)
    {
        source.volume = musicVolume * masterVolume;
        if (!source.isPlaying)
            source.Play();
    }
}
