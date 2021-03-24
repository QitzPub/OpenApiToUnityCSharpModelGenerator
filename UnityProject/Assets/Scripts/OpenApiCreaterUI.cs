using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OpenApiCreaterUI : MonoBehaviour
{
    [SerializeField]
    OpneApiLoader opneApiLoader;
    [SerializeField]
    Toggle createInterfaceToggle;
    [SerializeField]
    InputField input;
    [SerializeField]
    Button genelateButton;
    [SerializeField]
    Text errorText;
    [SerializeField]
    Text successText;

    // Start is called before the first frame update
    void Start()
    {
        errorText.gameObject.SetActive(false);
        successText.gameObject.SetActive(false);
        genelateButton.onClick.AddListener(()=> {
            StartCoroutine(opneApiLoader.GetJsonAndCreateModel(input.text, createInterfaceToggle.isOn,(error)=>{ errorText.gameObject.SetActive(true); errorText.text = "エラー："+error; },()=> { successText.gameObject.SetActive(true); }));
        });
    }

}
