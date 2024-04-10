using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SliderLogic : MonoBehaviour
{
    [SerializeField]
    private Slider slider;

    [SerializeField]
    private Transform[] scalables;

    private void Update()
    {
        foreach (var s in scalables)
        {
            s.localScale = new Vector3(slider.value, slider.value, slider.value);
        }
    }
}
