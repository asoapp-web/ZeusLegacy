using System;
using UnityEngine;
using UnityEngine.UI;

public class InGameCanvas : MonoBehaviour
{
    [SerializeField] Button spinButton;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] GameObject pauseButton;
    [SerializeField] GameObject resumeButton;
    [SerializeField] AudioSource soundClick;


    public void Pause()
    {
        spinButton.interactable = false;
        soundClick.Play();

        pauseButton.SetActive(false);
        resumeButton.SetActive(true);
    }

    public void Resume()
    {
        spinButton.interactable = true;
        soundClick.Play();

        pauseButton.SetActive(true);
        resumeButton.SetActive(false);
    }
}
