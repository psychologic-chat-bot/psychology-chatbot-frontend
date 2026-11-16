using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

public class Conexión_Ullama : MonoBehaviour
{
    public void AskModel(string prompt)
    {
        StartCoroutine(SendPrompt(prompt));
    }

    IEnumerator SendPrompt(string prompt)
    {
        string json = @"{
            ""model"":""llama3.2"",
            ""prompt"":""" + prompt + @""",
            ""stream"":false
        }";

        UnityWebRequest request = new UnityWebRequest(
            "http://localhost:11434/api/generate",
            "POST");

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log(request.downloadHandler.text);
        }
        else
        {
            Debug.LogError(request.error);
        }
    }
}
