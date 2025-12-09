using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

// Protótipo de chat de voz UDP para Unity (PCM16, mono).
// Em produção, substitua a codificação por Opus (ex.: Concentus) e adicione criptografia/jitter buffer mais robusto.
public class VoiceChat : MonoBehaviour
{
    public int localPort = 50005;
    public string remoteIp = "10.57.1.131";
    public int remotePort = 50005;
    public int sampleRate = 48000;
    public int frameMs = 20; // 20 ms por pacote
    public bool useMicrophone = true;
    public string micDevice = null; // null = default

    UdpClient udpClient;
    IPEndPoint remoteEndpoint;

    int samplesPerFrame;
    AudioClip micClip;
    int micPosition = 0;
    float[] micBuffer;
    short[] pcmBuffer;

    // Playback
    const int playbackBufferSeconds = 2;
    float[] playbackBuffer;
    int playbackWritePos = 0;
    int playbackReadPos = 0;
    object playbackLock = new object();

    Thread recvThread;
    bool running = false;

    void Start()
    {
        samplesPerFrame = (sampleRate * frameMs) / 1000;
        micBuffer = new float[samplesPerFrame];
        pcmBuffer = new short[samplesPerFrame];

        playbackBuffer = new float[playbackBufferSeconds * sampleRate];

        remoteEndpoint = new IPEndPoint(IPAddress.Parse(remoteIp), remotePort);
        udpClient = new UdpClient(localPort);
        udpClient.Client.ReceiveTimeout = 1000;

        StartMicrophone();
        running = true;
        recvThread = new Thread(ReceiveLoop);
        recvThread.IsBackground = true;
        recvThread.Start();
    }

    void StartMicrophone()
    {
        if (!useMicrophone) return;
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("Nenhum microfone encontrado.");
            useMicrophone = false;
            return;
        }
        micDevice = micDevice ?? Microphone.devices[0];
        micClip = Microphone.Start(micDevice, true, 1, sampleRate);
        while (!(Microphone.GetPosition(micDevice) > 0)) { }
        Debug.Log("Microfone iniciado: " + micDevice);
    }

    void Update()
    {
        if (useMicrophone && micClip != null)
        {
            int pos = Microphone.GetPosition(micDevice);
            int diff = pos - micPosition;
            if (diff < 0) diff += micClip.samples;

            while (diff >= samplesPerFrame)
            {
                micClip.GetData(micBuffer, micPosition);
                // Converte float -> PCM16
                for (int i = 0; i < samplesPerFrame; i++)
                {
                    float f = Mathf.Clamp(micBuffer[i], -1f, 1f);
                    pcmBuffer[i] = (short)(f * short.MaxValue);
                }
                // Enviar por UDP (bloqueante pequeno). Em produção, agrupe pacotes ou use send queue.
                try
                {
                    byte[] payload = new byte[samplesPerFrame * 2];
                    Buffer.BlockCopy(pcmBuffer, 0, payload, 0, payload.Length);
                    udpClient.Send(payload, payload.Length, remoteEndpoint);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Erro ao enviar UDP: " + e);
                }

                micPosition += samplesPerFrame;
                micPosition %= micClip.samples;
                diff -= samplesPerFrame;
            }
        }
    }

    void ReceiveLoop()
    {
        IPEndPoint any = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] data = udpClient.Receive(ref any);
                if (data != null && data.Length >= 2)
                {
                    int sampleCount = data.Length / 2;
                    float[] frame = new float[sampleCount];
                    short[] sdata = new short[sampleCount];
                    Buffer.BlockCopy(data, 0, sdata, 0, data.Length);
                    for (int i = 0; i < sampleCount; i++)
                    {
                        frame[i] = sdata[i] / (float)short.MaxValue;
                    }
                    // Enfileira no buffer de reprodução
                    lock (playbackLock)
                    {
                        for (int i = 0; i < frame.Length; i++)
                        {
                            playbackBuffer[playbackWritePos] = frame[i];
                            playbackWritePos = (playbackWritePos + 1) % playbackBuffer.Length;
                            // Se buffer estiver cheio, avançar o read para evitar overlap
                            if (playbackWritePos == playbackReadPos)
                                playbackReadPos = (playbackReadPos + 1) % playbackBuffer.Length;
                        }
                    }
                }
            }
            catch (SocketException)
            {
                // timeout possivelmente; continue
            }
            catch (Exception e)
            {
                Debug.LogWarning("ReceiveLoop erro: " + e);
            }
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        // Preencher saída com o conteúdo do playbackBuffer
        int len = data.Length / channels;
        lock (playbackLock)
        {
            for (int i = 0; i < len; i++)
            {
                float sample = playbackBuffer[playbackReadPos];
                playbackReadPos = (playbackReadPos + 1) % playbackBuffer.Length;
                for (int ch = 0; ch < channels; ch++)
                {
                    data[i * channels + ch] = sample;
                }
            }
        }
    }

    void OnDestroy()
    {
        running = false;
        if (recvThread != null && recvThread.IsAlive) recvThread.Join(500);
        if (udpClient != null) udpClient.Close();
        if (useMicrophone && micDevice != null)
        {
            Microphone.End(micDevice);
        }
    }

    // Chamadas utilitárias para iniciar/parar e configurar endpoints em runtime
    public void SetRemoteEndpoint(string ip, int port)
    {
        remoteIp = ip;
        remotePort = port;
        remoteEndpoint = new IPEndPoint(IPAddress.Parse(remoteIp), remotePort);
    }
}