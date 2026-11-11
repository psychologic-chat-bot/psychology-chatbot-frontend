using System.Collections;
using UnityEngine;

public class Prueba : MonoBehaviour
{
    public Animator Animator;
    private Rigidbody Rigidbody;
    public bool movimiento = false;
    public bool destino = false;

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
        Animator.SetBool("Mover", movimiento);
        Animator.SetBool("Sentar", destino);
    }
    IEnumerator Sentarse()
    {
        yield return new WaitForSeconds(10);
        destino = true;
        Rigidbody.MovePosition(new Vector3(-1.055f, 0f, 7.353f));
        yield return new WaitForSeconds(1.15f);
        Rigidbody.MoveRotation(Quaternion.Euler(0f, 0f, 0f));
    }
}
