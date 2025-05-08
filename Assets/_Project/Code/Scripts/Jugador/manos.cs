using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class manos : MonoBehaviour
{
    public InputActionReference botonAgarrar;
    public GameObject[] goManos;
    //0 - mano abierta
    //1 - mano cerrada

    void Start()
    {
        goManos[1].SetActive(false);
        goManos[0].SetActive(true);
        botonAgarrar.action.started += ActivarAgarrar;
        botonAgarrar.action.canceled += DesactivarAgarrar;
    }

    private void DesactivarAgarrar(InputAction.CallbackContext context)
    {
        goManos[1].SetActive(false);
        goManos[0].SetActive(true);
    }

    private void ActivarAgarrar(InputAction.CallbackContext context)
    {
        goManos[0].SetActive(false);
        goManos[1].SetActive(true);
    }
}
