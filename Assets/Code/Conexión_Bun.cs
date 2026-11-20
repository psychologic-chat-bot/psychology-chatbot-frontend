using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using UnityEngine.Events;

public class Conexión_Bun : MonoBehaviour
{
    [Header("Backend")]
    [SerializeField] private string baseUrl = "http://localhost:3000";
    [SerializeField] private string patientId = "shared_conversation_Valentina";
    [SerializeField] private int timeoutSeconds = 120;
    [SerializeField] private AudioSource audioSource;

    [Header("Eventos")]
    [SerializeField] private UnityEvent<string> onReply = new UnityEvent<string>();
    [SerializeField] private UnityEvent<string> onError = new UnityEvent<string>();

    private const string UserIdKey = "psychology-chatbot-user-id";
    private string userId;

    [Serializable]
    private class PatientListResponse
    {
        public PatientSummary[] patients;
    }

    [Serializable]
    private class PatientSummary
    {
        public string id;
    }

    [Serializable]
    private class ChatRequest
    {
        public string userId;
        public string patientId;
        public string message;
        public bool includeAudio;
    }

    [Serializable]
    private class ChatAudio
    {
        public string mimeType;
        public string data;
    }

    [Serializable]
    private class ChatResponse
    {
        public string reply;
        public string error;
        public string audioError;
        public ChatAudio audio;
    }

    private AudioClip currentAudioClip;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        userId = PlayerPrefs.GetString(UserIdKey, "");
        if (string.IsNullOrEmpty(userId))
        {
            userId = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(UserIdKey, userId);
            PlayerPrefs.Save();
        }
    }

    private IEnumerator Start()
    {
        string patientsUrl = GetBaseUrl() + "/patients";
        using (UnityWebRequest request = UnityWebRequest.Get(patientsUrl))
        {
            request.timeout = timeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                ReportError($"No se pudo conectar con el backend ({request.error}). Verifica la URL y que el servidor esté activo.");
                yield break;
            }

            PatientListResponse response;
            try
            {
                response = JsonUtility.FromJson<PatientListResponse>(request.downloadHandler.text);
            }
            catch (Exception exception)
            {
                ReportError($"El backend devolvió una lista de pacientes inválida: {exception.Message}");
                yield break;
            }

            if (response?.patients == null || response.patients.Length == 0)
            {
                ReportError("El backend no tiene pacientes disponibles.");
                yield break;
            }

            bool patientExists = false;
            foreach (PatientSummary patient in response.patients)
            {
                if (patient.id == patientId)
                {
                    patientExists = true;
                    break;
                }
            }

            if (!patientExists)
            {
                patientId = response.patients[0].id;
                Debug.LogWarning($"El paciente configurado no existe. Se seleccionó '{patientId}'.");
            }

            Debug.Log($"Backend conectado. Paciente seleccionado: {patientId}");
        }
    }

    public void SendMessageToPatient(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            ReportError("Escribe un mensaje antes de enviarlo.");
            return;
        }

        if (string.IsNullOrEmpty(patientId))
        {
            ReportError("No hay un paciente seleccionado. Espera a que el backend cargue los pacientes.");
            return;
        }

        StartCoroutine(RequestChatReply(message.Trim()));
    }

    private IEnumerator RequestChatReply(string message)
    {
        ChatRequest payload = new ChatRequest
        {
            userId = userId,
            patientId = patientId,
            message = message,
            includeAudio = true
        };
        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

        using (UnityWebRequest request = new UnityWebRequest(GetBaseUrl() + "/chat", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = timeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            string responseText = request.downloadHandler.text;
            ChatResponse response = null;
            if (!string.IsNullOrEmpty(responseText))
            {
                try
                {
                    response = JsonUtility.FromJson<ChatResponse>(responseText);
                }
                catch (Exception exception)
                {
                    ReportError($"No se pudo interpretar la respuesta del backend: {exception.Message}");
                    yield break;
                }
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                string messageText = response?.error;
                if (string.IsNullOrEmpty(messageText))
                {
                    messageText = request.error;
                }
                ReportError($"Error del backend (HTTP {request.responseCode}): {messageText}");
                yield break;
            }

            if (string.IsNullOrEmpty(response?.reply))
            {
                ReportError("El backend respondió correctamente, pero no devolvió el campo 'reply'.");
                yield break;
            }

            onReply.Invoke(response.reply);

            if (response.audio != null && !string.IsNullOrEmpty(response.audio.data))
            {
                try
                {
                    PlayWav(Convert.FromBase64String(response.audio.data));
                }
                catch (Exception exception)
                {
                    ReportError($"No se pudo reproducir el audio de la paciente: {exception.Message}");
                }
            }
            else if (!string.IsNullOrEmpty(response.audioError))
            {
                ReportError($"La respuesta de texto llegó, pero no se pudo generar el audio: {response.audioError}");
            }
        }
    }

    private void PlayWav(byte[] wav)
    {
        int format = 0;
        int channels = 0;
        int sampleRate = 0;
        int bitsPerSample = 0;
        int dataOffset = -1;
        int dataLength = 0;

        if (wav.Length < 12 ||
            ReadFourCc(wav, 0) != "RIFF" ||
            ReadFourCc(wav, 8) != "WAVE")
        {
            throw new InvalidDataException("El archivo recibido no es WAV.");
        }

        int offset = 12;
        while (offset + 8 <= wav.Length)
        {
            string chunkId = ReadFourCc(wav, offset);
            int chunkSize = BitConverter.ToInt32(wav, offset + 4);
            int chunkData = offset + 8;
            if (chunkSize < 0 || chunkSize > wav.Length - chunkData)
            {
                throw new InvalidDataException("El WAV contiene un bloque inválido.");
            }

            if (chunkId == "fmt " && chunkSize >= 16)
            {
                format = BitConverter.ToInt16(wav, chunkData);
                channels = BitConverter.ToInt16(wav, chunkData + 2);
                sampleRate = BitConverter.ToInt32(wav, chunkData + 4);
                bitsPerSample = BitConverter.ToInt16(wav, chunkData + 14);
            }
            else if (chunkId == "data")
            {
                dataOffset = chunkData;
                dataLength = chunkSize;
            }

            offset = chunkData + chunkSize + (chunkSize & 1);
        }

        if (format != 1 || bitsPerSample != 16 || channels < 1 || channels > 2 ||
            sampleRate <= 0 || dataOffset < 0 || dataLength == 0 ||
            dataLength % (2 * channels) != 0)
        {
            throw new InvalidDataException("El WAV debe ser PCM de 16 bits, mono o estéreo.");
        }

        int sampleCount = dataLength / 2;
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = BitConverter.ToInt16(wav, dataOffset + i * 2) / 32768f;
        }

        int frameCount = sampleCount / channels;
        if (frameCount == 0)
        {
            throw new InvalidDataException("El WAV no contiene muestras de audio.");
        }

        if (currentAudioClip != null)
        {
            audioSource.Stop();
            Destroy(currentAudioClip);
        }

        currentAudioClip = AudioClip.Create(
            "PatientReply",
            frameCount,
            channels,
            sampleRate,
            false
        );
        currentAudioClip.SetData(samples, 0);
        audioSource.clip = currentAudioClip;
        audioSource.Play();
    }

    private static string ReadFourCc(byte[] data, int offset)
    {
        return System.Text.Encoding.ASCII.GetString(data, offset, 4);
    }

    private string GetBaseUrl()
    {
        return baseUrl.TrimEnd('/');
    }

    private void ReportError(string message)
    {
        Debug.LogError($"[Conexión_Bun] {message}");
        onError.Invoke(message);
    }
}
