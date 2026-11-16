using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class Conexión_Bun : MonoBehaviour
{
    IEnumerator Start()
    {
        UnityWebRequest request =
            UnityWebRequest.Get("http://localhost:3000");

        yield return request.SendWebRequest();

        Debug.Log(request.downloadHandler.text);
    }
}
