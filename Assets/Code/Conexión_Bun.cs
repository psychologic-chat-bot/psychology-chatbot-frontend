using System;
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
    }

    [Serializable]
    private class ChatResponse
    {
        public string reply;
        public string error;
    }

    private void Awake()
    {
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
            message = message
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
        }
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
<<<<<<< HEAD
}
=======
}
>>>>>>> 513fcde7c091c7c583e2c155de2ed34e4f6441c6
