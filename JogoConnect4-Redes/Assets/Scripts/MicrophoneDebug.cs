using System.Collections;
using UnityEngine;

// Script de debug para detectar microfones e tentar iniciar o microfone padrão.
// Coloque este script em um GameObject na cena. (Opcionalmente arraste um AudioSource para "audioSource" para ouvir loopback.)
public class MicrophoneDebug : MonoBehaviour
{
    public AudioSource audioSource;           // opcional, para ouvir o microfone (loopback)
    public int sampleRate = 48000;
    public bool autoStart = true;
    public float startTimeoutSeconds = 3f;

    void Start()
    {
        if (autoStart)
            StartCoroutine(CheckAndStartMicrophone());
    }

    IEnumerator CheckAndStartMicrophone()
    {
        Debug.Log("MicrophoneDebug: verificando dispositivos...");
        string[] devices = Microphone.devices;

        if (devices == null || devices.Length == 0)
        {
            Debug.LogWarning("MicrophoneDebug: nenhum microfone encontrado via Microphone.devices.");

#if UNITY_ANDROID
            // Tenta pedir permissão em Android (se aplicável)
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                Debug.Log("MicrophoneDebug: solicitando permissão de microfone no Android...");
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                // aguardar um pouco para o usuário responder
                yield return new WaitForSeconds(1f);
                devices = Microphone.devices;
                if (devices == null || devices.Length == 0)
                {
                    Debug.LogWarning("MicrophoneDebug: após pedido de permissão, ainda nenhum microfone listado.");
                }
            }
#endif

            // Mensagem de ajuda rápida
            Debug.LogWarning("MicrophoneDebug: possíveis causas:\n" +
                             "- Microfone desligado ou sem drivers\n" +
                             "- Permissão do SO negada (Windows/macOS/Android/iOS)\n" +
                             "- Outro app ocupando o microfone\n" +
                             "- Plataforma não suporta Microphone (WebGL limitado)\n" +
                             "Verifique as permissões do sistema e reinicie o Unity se necessário.");
            yield break;
        }

        Debug.Log($"MicrophoneDebug: {devices.Length} dispositivo(s) encontrados.");
        for (int i = 0; i < devices.Length; i++)
            Debug.Log($" - [{i}] {devices[i]}");

        // Seleciona o primeiro dispositivo por padrão
        string chosen = devices[0];
        Debug.Log("MicrophoneDebug: tentando iniciar microfone: " + chosen);

        AudioClip clip = Microphone.Start(chosen, true, 1, sampleRate);
        float start = Time.realtimeSinceStartup;
        while (Microphone.GetPosition(chosen) <= 0)
        {
            if (Time.realtimeSinceStartup - start > startTimeoutSeconds)
            {
                Debug.LogWarning("MicrophoneDebug: timeout ao iniciar o microfone (GetPosition ainda 0).");
                Microphone.End(chosen);
                yield break;
            }
            yield return null;
        }

        Debug.Log("MicrophoneDebug: microfone iniciado com sucesso. Position: " + Microphone.GetPosition(chosen));

        if (audioSource != null)
        {
            audioSource.loop = true;
            audioSource.clip = clip;
            audioSource.Play();
            Debug.Log("MicrophoneDebug: loopback de áudio ativado via AudioSource.");
        }
        else
        {
            Debug.Log("MicrophoneDebug: nenhum AudioSource definido — o microfone foi iniciado, mas não será reproduzido localmente.");
        }
    }
}