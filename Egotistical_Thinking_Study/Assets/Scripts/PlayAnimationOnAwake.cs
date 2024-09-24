using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayAnimationOnAwake : MonoBehaviour
{
    void Start()
    {
        GetComponent<Animation>().Play();
    }
}
