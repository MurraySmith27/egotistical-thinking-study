using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClickSFXPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource m_mouseClickSFX;
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            m_mouseClickSFX.Play();
        }
    }
}
