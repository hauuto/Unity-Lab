using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class Lab10Detection
{
    public string label;
    public float confidence;
    public int x1, y1, x2, y2; // pixel anh goc, goc tren-trai
}

[Serializable]
public class Lab10Response
{
    public int width, height;
    public float inference_ms;
    public Lab10Detection[] detections;
    public string error;
}

/// Gui anh toi REST API (qua Nginx) va tra ket qua JSON da parse.
public class Lab10ApiClient : MonoBehaviour
{
    [SerializeField] string baseUrl = "http://localhost:8080";
    [SerializeField, Range(0f, 1f)] float minConfidence = 0.25f;
    [SerializeField] int timeoutSeconds = 30;

    public IEnumerator Predict(byte[] jpg, string fileName, Action<Lab10Response, string> done)
    {
        var form = new WWWForm();
        form.AddBinaryData("image", jpg, fileName, "image/jpeg");
        // Ep dau cham thap phan: may Viet Nam co the format 0,25 lam server tra 400.
        form.AddField("min_confidence", minConfidence.ToString(System.Globalization.CultureInfo.InvariantCulture));

        using var request = UnityWebRequest.Post(baseUrl + "/predict", form);
        request.timeout = timeoutSeconds;
        yield return request.SendWebRequest();

        Lab10Response body = null;
        try { body = JsonUtility.FromJson<Lab10Response>(request.downloadHandler.text); }
        catch (ArgumentException) { /* Loi mang hoac HTML tu Nginx: khong phai JSON */ }

        if (request.result == UnityWebRequest.Result.Success && body != null) done(body, null);
        else done(null, !string.IsNullOrEmpty(body?.error) ? body.error : request.error);
    }
}
