using System;
using System.Collections;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

public class Conexión_Bun : MonoBehaviour
{
    public Micrófono micrófono;

    [SerializeField] private string baseUrl = "http://localhost:3000";
    [SerializeField] private string patientId = "shared_conversation_Valentina";
    private string userId;

    [Tooltip("Folder that contains package.json")]
    public string projectPath = @"C:\Users\gregorio.rivas\Documents\Proyectos\psychology-chatbot-backend";

    [Tooltip("Port your dev server listens on")]
    public int port = 3000;

    [Tooltip("Kill the server when Unity stops playing")]
    public bool stopOnQuit = true;

    private static Process bunProcess;

    public class PreguntaPsicólogo
    {
        public string userId;
        public string patientId;
        public string message;
    }

    private class RespuestaPaciente
    {
        public string reply;
    }

    void Awake()
    {
        const string userIdKey = "psychology-chatbot-user-id";
        userId = PlayerPrefs.GetString(userIdKey, "");

        if (string.IsNullOrEmpty(userId))
        {
            userId = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(userIdKey, userId);
        }

        if (IsPortOpen(port))
        {
            Debug.Log($"Bun is already running on port {port}, not starting another one.");
            return;
        }

        if (bunProcess != null && !bunProcess.HasExited) return;

        var psi = new ProcessStartInfo
        {
            FileName = "bun",                 // must be in PATH (or use the full path to bun.exe)
            Arguments = "run dev",
            WorkingDirectory = projectPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        bunProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
        bunProcess.OutputDataReceived += (s, e) => { if (e.Data != null) Debug.Log("[bun] " + e.Data); };
        bunProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) Debug.LogWarning("[bun] " + e.Data); };

        try
        {
            bunProcess.Start();
            bunProcess.BeginOutputReadLine();
            bunProcess.BeginErrorReadLine();
            Debug.Log("Started 'bun run dev'");
        }
        catch (Exception ex)
        {
            Debug.LogError("Could not start Bun: " + ex.Message);
        }
    }

    void OnApplicationQuit()
    {
        if (!stopOnQuit || bunProcess == null || bunProcess.HasExited) return;

        try
        {
            // Kills bun AND any child processes it spawned (Windows)
            Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/PID {bunProcess.Id} /T /F",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        catch { bunProcess.Kill(); }
    }

    private static bool IsPortOpen(int port)
    {
        try
        {
            using var client = new TcpClient();
            var result = client.BeginConnect("127.0.0.1", port, null, null);
            bool ok = result.AsyncWaitHandle.WaitOne(200);
            if (ok) client.EndConnect(result);
            return ok && client.Connected;
        }
        catch { return false; }
    }

    public IEnumerator Text(string text)
    {
        PreguntaPsicólogo pregunta = new PreguntaPsicólogo
        {
            userId = userId,
            patientId = patientId,
            message = text,
        };

        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(pregunta));
        Debug.Log($"userId: {userId}" + $"paciente: {patientId}" + $"message: {text}");

        using (var request = new UnityWebRequest(
            baseUrl.TrimEnd('/') + "/chat",
            UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"Error al llamar al backend: {request.responseCode} {request.error}\n" +
                    request.downloadHandler.text);
                yield break;
            }
            var response =
                JsonUtility.FromJson<RespuestaPaciente>(request.downloadHandler.text);

            Debug.Log("Respuesta del paciente: " + response.reply);
        }
    }
}
