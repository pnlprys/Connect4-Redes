using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public void EscolherVermelho()
    {
        PlayerPreferences.Instance.SetPlayerAsRed();
        SceneManager.LoadScene("Game");
    }

    public void EscolherVerde()
    {
        PlayerPreferences.Instance.SetPlayerAsGreen();
        SceneManager.LoadScene("Game");
    }
}