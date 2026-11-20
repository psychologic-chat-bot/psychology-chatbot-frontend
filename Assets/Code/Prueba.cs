using System.Collections;
using UnityEngine;

public class Prueba : MonoBehaviour
{
    public Animator Animator;
    private Rigidbody Rigidbody;
    public bool Mover = false;
    public bool Sentar = false;

    private void Awake()
    {
        Animator = GetComponentInChildren<Animator>();
        Rigidbody = GetComponent<Rigidbody>();
    }
    private void Start()
    {
        StartCoroutine(Sentarse());
    }
    private void Update()
    {
        Animator.SetBool("Mover", Mover);
        Animator.SetBool("Sentar", Sentar);
    }
    IEnumerator Sentarse()
    {
        float tiempo = 0f;
        yield return new WaitForSeconds(2f);
        Mover = true;
        while (tiempo < 2f)
        {
            Rigidbody.MoveRotation(Quaternion.Euler(0f, Mathf.Lerp(90f, 115f, tiempo * 2), 0f));
            Rigidbody.linearVelocity = new Vector3(1.14f / 2, 0f, -0.595f / 2);
            tiempo += Time.deltaTime;
            if (tiempo >= 1f)
            {
                Rigidbody.MoveRotation(Quaternion.Euler(0f, Mathf.Lerp(115f, 90f, tiempo - 1), 0f));
            }
            yield return null;
        }
        Mover = false;
        Sentar = true;
        yield return new WaitForSeconds(1.4f);
        Animator.SetTrigger("Giro");
        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
    }
}
