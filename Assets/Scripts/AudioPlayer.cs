using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;

/// <summary>
/// Downloads and plays WAV audio from the avatar server.
/// Provides amplitude data for lip sync.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioPlayer : MonoBehaviour
{
    private AudioSource _audioSource;
    private Action _onFinished;
    private bool _isPlaying;

    // Amplitude data for lip sync
    private float[] _sampleBuffer = new float[256];
    public float CurrentAmplitude { get; private set; }

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f; // 2D audio
    }

    void Update()
    {
        if (_isPlaying && _audioSource.isPlaying)
        {
            // Sample the audio for amplitude
            _audioSource.GetOutputData(_sampleBuffer, 0);

            float sum = 0f;
            for (int i = 0; i < _sampleBuffer.Length; i++)
            {
                sum += Mathf.Abs(_sampleBuffer[i]);
            }
            CurrentAmplitude = sum / _sampleBuffer.Length;
        }
        else if (_isPlaying && !_audioSource.isPlaying)
        {
            // Audio finished
            _isPlaying = false;
            CurrentAmplitude = 0f;
            _onFinished?.Invoke();
            _onFinished = null;
        }
        else
        {
            CurrentAmplitude = 0f;
        }
    }

    /// <summary>
    /// Download audio from URL and play it.
    /// </summary>
    public void PlayFromUrl(string url, Action onFinished = null)
    {
        _onFinished = onFinished;
        StartCoroutine(DownloadAndPlay(url));
    }

    IEnumerator DownloadAndPlay(string url)
    {
        Debug.Log($"[AudioPlayer] Downloading audio: {url}");

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            www.timeout = (int)Config.AudioDownloadTimeout;
            yield return www.SendWebRequest();

            Debug.Log($"[AudioPlayer] Download result: {www.result}, responseCode: {www.responseCode}, error: {www.error}, downloadedBytes: {www.downloadedBytes}");

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[AudioPlayer] Download failed: {www.error} (code: {www.responseCode})");
                _onFinished?.Invoke();
                _onFinished = null;
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            if (clip == null)
            {
                Debug.LogWarning("[AudioPlayer] Failed to decode audio clip");
                _onFinished?.Invoke();
                _onFinished = null;
                yield break;
            }

            Debug.Log($"[AudioPlayer] Clip loaded: length={clip.length:F2}s, channels={clip.channels}, frequency={clip.frequency}, samples={clip.samples}");
            Debug.Log($"[AudioPlayer] AudioSource: volume={_audioSource.volume}, mute={_audioSource.mute}, enabled={_audioSource.enabled}");

            _audioSource.clip = clip;
            _audioSource.volume = 1f;
            _audioSource.mute = false;
            _audioSource.Play();
            _isPlaying = true;

            Debug.Log($"[AudioPlayer] Playing audio ({clip.length:F1}s), isPlaying={_audioSource.isPlaying}");

            // Debug: check if actual audio data exists in the clip
            float[] debugSamples = new float[1024];
            clip.GetData(debugSamples, 0);
            float maxSample = 0f;
            for (int i = 0; i < debugSamples.Length; i++)
                if (Mathf.Abs(debugSamples[i]) > maxSample) maxSample = Mathf.Abs(debugSamples[i]);
            Debug.Log($"[AudioPlayer] DEBUG: max sample amplitude in first 1024 samples = {maxSample:F6} (0 = silence)");
        }
    }

    /// <summary>
    /// Stop current playback.
    /// </summary>
    public void Stop()
    {
        if (_audioSource.isPlaying)
            _audioSource.Stop();

        _isPlaying = false;
        CurrentAmplitude = 0f;
        _onFinished = null;
    }
}
