using System;
using UnityEngine;
using UnityEngine.Events;
using Whisper; // from the whisper.unity package
#if UNITY_ANDROID
using UnityEngine.Android;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Records microphone audio and transcribes it on-device with whisper.unity.
/// No WAV encoding and no server needed.
///
/// Setup:
///  1. Install whisper.unity (see instructions) and add a WhisperManager
///     component to a GameObject. Point it at a model in StreamingAssets.
///  2. Add this script to a GameObject and drag the WhisperManager into the field.
///  3. Call StartRecording() / StopRecordingAndTranscribe() from your VR controller
///     events, or hold the push-to-talk key in the editor.
/// </summary>
public class Micrófono : MonoBehaviour
{
    public Conexión_Bun conexiónBun;
    [Header("References")]
    [SerializeField] private WhisperManager whisper;

    [Header("Recording")]
    [Tooltip("Whisper works at 16000 Hz")]
    [SerializeField] private int sampleRate = 16000;
    [SerializeField] private int maxRecordSeconds = 30;
    [Tooltip("Leave empty to use the default microphone")]
    [SerializeField] private string microphoneDevice = "";

    [Header("Testing (keyboard push-to-talk)")]
    [SerializeField] private KeyCode pushToTalkKey = KeyCode.Space;

    [Header("Events")]
    public UnityEvent<string> onTranscript;
    public UnityEvent<string> onError;

    public bool IsRecording { get; private set; }
    public bool IsTranscribing { get; private set; }

    private AudioClip _clip;
    private string _device;
    public string conversación;

    private void Start()
    {
        foreach (var device in Microphone.devices)
        {
            Debug.Log("Name: " + device);
        }
        _device = string.IsNullOrEmpty(microphoneDevice) ? null : microphoneDevice;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            Permission.RequestUserPermission(Permission.Microphone);
#endif

        if (whisper == null)
            Fail("Assign the WhisperManager in the inspector.");
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.spaceKey.wasPressedThisFrame) StartRecording();
            if (keyboard.spaceKey.wasReleasedThisFrame) StopRecordingAndTranscribe();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(pushToTalkKey)) StartRecording();
        if (Input.GetKeyUp(pushToTalkKey)) StopRecordingAndTranscribe();
#endif
    }

    // ---- Public API: hook these to your VR controller button events ----

    public void StartRecording()
    {
        if (IsRecording || IsTranscribing) return;
        if (Microphone.devices.Length == 0)
        {
            Fail("No microphone detected.");
            return;
        }

        _clip = Microphone.Start(_device, false, maxRecordSeconds, sampleRate);
        IsRecording = true;
        Debug.Log("[MicrophoneTranscriber] Recording started");
    }

    public async void StopRecordingAndTranscribe()
    {
        if (!IsRecording) return;
        IsRecording = false;

        int samplesRecorded = Microphone.GetPosition(_device);
        Microphone.End(_device);

        // Ignore accidental taps (< 0.3 seconds)
        if (_clip == null || samplesRecorded < sampleRate * 0.3f)
        {
            Debug.Log("[MicrophoneTranscriber] Recording too short, discarded.");
            return;
        }

        // Copy only the part that was actually recorded
        float[] samples = new float[samplesRecorded * _clip.channels];
        _clip.GetData(samples, 0);

        IsTranscribing = true;
        try
        {
            var result = await whisper.GetTextAsync(samples, _clip.frequency, _clip.channels);
            string text = result?.Result?.Trim();
            //Debug.Log($"[MicrophoneTranscriber] Transcript: {text}");
            conversación = text;
            onTranscript?.Invoke(text);
        }
        catch (Exception e)
        {
            Fail($"Transcription failed: {e.Message}");
        }
        finally
        {
            IsTranscribing = false;
            SendText(conversación);
        }
    }

    private void Fail(string mensaje)
    {
        Debug.LogError($"[MicrophoneTranscriber] {mensaje}");
        onError?.Invoke(mensaje);
    }

    public void SendText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        StartCoroutine(conexiónBun.Text(text));
    }
}
