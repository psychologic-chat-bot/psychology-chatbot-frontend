using UnityEngine;

public class Conexión_Ullama : MonoBehaviour
{
    [SerializeField] private Conexión_Bun backend;

    public void AskModel(string prompt)
    {
        if (backend == null)
        {
            Debug.LogError("[Conexión_Ullama] Asigna el componente Conexión_Bun en el Inspector.");
            return;
        }

        backend.SendMessageToPatient(prompt);
    }
}
