using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class RuntimeTtsClient : MonoBehaviour
{
    [System.Serializable]
    private class TtsRequest
    {
        public string text;
    }

    public string serverUrl = "http://127.0.0.1:5005/tts";

    public IEnumerator SynthesizeToClip(string text, Action<AudioClip> onClipReady)
    {
        TtsRequest requestData = new TtsRequest
        {
            text = text
        };

        string json = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using UnityWebRequest request = new UnityWebRequest(serverUrl, "POST");

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerAudioClip(serverUrl, AudioType.WAV);
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("RuntimeTtsClient error: " + request.error);
            Debug.LogWarning("Response: " + request.downloadHandler.text);
            onClipReady?.Invoke(null);
            yield break;
        }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(request);

        if (clip == null)
        {
            Debug.LogWarning("RuntimeTtsClient: received null AudioClip.");
            onClipReady?.Invoke(null);
            yield break;
        }

        onClipReady?.Invoke(clip);
    }
}