using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// Records microphone audio, encodes it as a 16-bit mono WAV and POSTs it
/// to a speech-to-text server. Fires onTranscript with the returned text.
///
/// Usage:
///  - Add to any GameObject.
///  - Set serverUrl to your coworker's endpoint.
///  - Call StartRecording() / StopRecordingAndSend() from a VR controller
///    button event (or use the keyboard push-to-talk key for testing).
/// </summary>
public class Micrófono : MonoBehaviour
{
    [Header("Server")]
    [Tooltip("Endpoint that accepts a WAV upload and returns JSON like {\"text\": \"...\"}")]
    [SerializeField] private string serverUrl = "http://192.168.1.50:3000/transcribe";
    [Tooltip("Multipart field name the server expects for the audio file")]
    [SerializeField] private string formFieldName = "file";
    [SerializeField] private int timeoutSeconds = 30;

    [Header("Recording")]
    [Tooltip("16000 Hz is what Whisper uses internally, and keeps uploads small")]
    [SerializeField] private int sampleRate = 16000;
    [SerializeField] private int maxRecordSeconds = 30;
    [Tooltip("Leave empty to use the default microphone")]
    [SerializeField] private string micrófonoExistente = "";

    [Header("Testing (keyboard push-to-talk)")]
    [SerializeField] private KeyCode pulsarParaHablar = KeyCode.Space;

    [Header("Eventos")]
    public UnityEvent<string> EnTranscribir;
    public UnityEvent<string> EnError;

    public bool grabando { get; private set; }
    private AudioClip grabación;
    private string micrófono;

    [Serializable]
    private class TranscriptResponse
    {
        public string texto;
    }

    private IEnumerator Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Quest / Android needs a runtime microphone permission
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
            yield return new WaitForSeconds(1f);
        }
#endif
        foreach (var micrófonos in Microphone.devices)
        {
            Debug.Log("Name: " + micrófonos);
        }
        micrófono = string.IsNullOrEmpty(micrófonoExistente) ? null : micrófonoExistente;

        if (Microphone.devices.Length == 0)
            Fail("No microphone detected.");
        else
        {
            Debug.Log("Hay micrófonos disponibles");
        } 
        

            yield break;
    }

    private void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(pulsarParaHablar)) StartRecording();
        if (Input.GetKeyUp(pulsarParaHablar)) StopRecordingAndSend();
#endif
    }

    // ---- Public API: hook these to your VR controller button events ----

    public void StartRecording()
    {
        if (grabando) return;
        if (Microphone.devices.Length == 0)
        {
            Fail("No microphone detected.");
            return;
        }

        grabación = Microphone.Start(micrófono, false, maxRecordSeconds, sampleRate);
        grabando = true;
        Debug.Log("[MicrophoneRecorder] Recording started");
    }

    public void StopRecordingAndSend()
    {
        if (!grabando) return;
        grabando = false;

        int samplesRecorded = Microphone.GetPosition(micrófono);
        Microphone.End(micrófono);
        Debug.Log($"[MicrophoneRecorder] Recording stopped ({samplesRecorded} samples)");

        // Ignore accidental taps (< 0.3 seconds)
        if (grabación == null || samplesRecorded < sampleRate * 0.3f)
        {
            Debug.Log("[MicrophoneRecorder] Recording too short, discarded.");
            return;
        }

        // Only grab the portion that was actually recorded, not the whole buffer
        float[] samples = new float[samplesRecorded * grabación.channels];
        grabación.GetData(samples, 0);

        //byte[] wav = EncodeWav(samples, _clip.channels, _clip.frequency);
        //StartCoroutine(SendAudio(wav));
        Reproducir();
    }

    private void Reproducir()
    {
        AudioSource.PlayClipAtPoint(grabación, gameObject.transform.position);
    }
    // ---- Networking ----

    private IEnumerator SendAudio(byte[] wavBytes)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData(formFieldName, wavBytes, "recording.wav", "audio/wav");

        using (UnityWebRequest request = UnityWebRequest.Post(serverUrl, form))
        {
            request.timeout = timeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Fail($"Request failed: {request.error} (HTTP {request.responseCode})");
                yield break;
            }

            string json = request.downloadHandler.text;
            try
            {
                TranscriptResponse response = JsonUtility.FromJson<TranscriptResponse>(json);
                string text = response?.texto?.Trim();
                Debug.Log($"[MicrophoneRecorder] Transcript: {text}");
                EnTranscribir?.Invoke(text);
            }
            catch (Exception e)
            {
                Fail($"Could not parse server response: {e.Message}\n{json}");
            }
        }
    }

    // ---- WAV encoding (16-bit PCM) ----

    private static byte[] EncodeWav(float[] samples, int channels, int sampleRate)
    {
        const int headerSize = 44;
        int dataSize = samples.Length * 2;

        using (MemoryStream stream = new MemoryStream(headerSize + dataSize))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataSize);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);                          // fmt chunk size
            writer.Write((short)1);                    // PCM
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * 2);   // byte rate
            writer.Write((short)(channels * 2));       // block align
            writer.Write((short)16);                   // bits per sample
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataSize);

            foreach (float sample in samples)
            {
                short value = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue);
                writer.Write(value);
            }

            return stream.ToArray();
        }
    }

    private void Fail(string message)
    {
        Debug.LogError($"[MicrophoneRecorder] {message}");
        EnError?.Invoke(message);
    }
}
