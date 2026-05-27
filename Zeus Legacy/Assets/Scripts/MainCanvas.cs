using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class MainCanvas : MonoBehaviour
{
    [SerializeField] GameObject gamePanel;
    [SerializeField] GameObject settingPanel;
    [SerializeField] GameObject privacyPanel;
    [SerializeField] GameObject loadingPanel;


    [SerializeField] GameObject soundOn;
    [SerializeField] GameObject soundOff;
    [SerializeField] GameObject soundOn1;
    [SerializeField] GameObject soundOff1;

    [SerializeField] AudioSource mainMusic;
    [SerializeField] AudioSource soundClick;


    [SerializeField] TextMeshProUGUI loadingText;
    [SerializeField] Slider slider;
    [SerializeField] GameObject barYellow;
    [SerializeField] GameObject barGreen;

    [SerializeField] int gameToPlay = 0;


    public void Start()
    {
        slider.maxValue = 100;
        //StartLoading();
    }

    public void StartLoading()
    {
        InvokeRepeating(nameof(SliderTime), 0.1f, 0.05f);
    }

    void SliderTime()
    {
        float num = 0f;
        slider.value ++;
        num = slider.value;
        loadingText.text = num.ToString() + "%";

        if(num == 100)
        {
            barYellow.SetActive(false);
            barGreen.SetActive(true);
            CancelInvoke("SliderTime");
            Invoke(nameof(LoadGame), 1f);
        }
    }

    void LoadGame()
    {
        switch (gameToPlay)
        {
            case 1:
            SceneManager.LoadScene("1Map");
            break;

            case 2:
            SceneManager.LoadScene("2Map");
            break;

            case 3:
            SceneManager.LoadScene("3Map");
            break;

            case 4:
            SceneManager.LoadScene("4Map");
            break;
        }
        
    }

    public void OpenGamePanel()
    {
        gamePanel.SetActive(true);
        settingPanel.SetActive(false);
        privacyPanel.SetActive(false);

        soundClick.Play();
    }

    public void OpenSettingPanel()
    {
        settingPanel.SetActive(true);
        gamePanel.SetActive(false);
        privacyPanel.SetActive(false);

        soundClick.Play();
    }

    public void OpenPrivacyPanel()
    {
        privacyPanel.SetActive(true);
        settingPanel.SetActive(false);
        gamePanel.SetActive(false);

        soundClick.Play();
    }

    public void MuteButton()
    {
        soundOff.SetActive(false);
        soundOff1.SetActive(true);
        soundOn.SetActive(true);
        soundOn1.SetActive(false);

        mainMusic.mute = true;
        soundClick.Play();

        Debug.Log("Sound Muted");
    }

    public void UnMuteButton()
    {
        soundOff.SetActive(true);
        soundOff1.SetActive(false);
        soundOn.SetActive(false);
        soundOn1.SetActive(true);

        mainMusic.mute = false;
        soundClick.Play();

        Debug.Log("Sound UnMuted");
    }

    void LoadingScreen()
    {
        gamePanel.SetActive(false);
        loadingPanel.SetActive(true);

        StartLoading();
    }

    //Games
    public void Map1Scene()
    {
        soundClick.Play();
        gameToPlay = 1;
        LoadingScreen();
    }

    public void Map2Scene()
    {
        soundClick.Play();
        gameToPlay = 2;
        LoadingScreen();
    }

    public void Map3Scene()
    {
        soundClick.Play();
        gameToPlay = 3;
        LoadingScreen();
    }

    public void Map4Scene()
    {
        soundClick.Play();
        gameToPlay = 4;
        LoadingScreen();
    }

    public void Exit()
    {
        Application.Quit();
    }
}
