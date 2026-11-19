using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

// Optional one-way developer telemetry. Never reads a sheet or uses Google account credentials.
public sealed class GoogleSheetsTelemetryService : MonoBehaviour
{
    [Serializable] private sealed class Config
    {
        public bool enabled;
        public string endpoint, ingestKey;
        public bool sendFromEditor;
        public int batchSize = 5;
        public int requestTimeoutSeconds = 15;
    }
    [Serializable] private sealed class Batch
    {
        public int protocolVersion = 1;
        public string ingestKey;
        public List<SheetsTelemetryRun> runs;
    }
    [Serializable] private sealed class Reply
    {
        public bool ok;
        public string[] acceptedIds, rejectedIds;
        public bool retryable;
        public string error;
    }
    private static Config config;
    private static bool configLoaded, quitting;
    private static GoogleSheetsTelemetryService instance;
    private static string applicationSessionId;
    private SheetsTelemetryOutbox outbox;
    private UnityWebRequest activeRequest;
    private float nextAttempt;
    private int failures;
    private static string QueuePath => Path.Combine(RunTelemetryStorage.DirectoryPath, "sheets_pending.json");
    public static string Status { get; private set; } = "Sin envios pendientes";
    public static bool IsConfigured
    {
        get
        {
            LoadConfig();
            return config != null && config.enabled && config.ingestKey != null && config.ingestKey.Length >= 24 &&
                Regex.IsMatch(config.endpoint ?? "", @"^https://script\.google\.com/macros/s/[A-Za-z0-9_-]+/exec$");
        }
    }
    private static bool MaySend
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false; // Apps Script's browser redirects/CORS need a separate proxy for WebGL.
#else
            return UserSettings.GetShareGameplayTelemetry() && IsConfigured && (!Application.isEditor || config.sendFromEditor);
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null; config = null; configLoaded = false; quitting = false;
        applicationSessionId = Guid.NewGuid().ToString("N");
        Status = "Sin envios pendientes";
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() { if (MaySend) EnsureInstance(); }

    private static void LoadConfig()
    {
        if (configLoaded) return;
        configLoaded = true;
        try
        {
            var asset = Resources.Load<TextAsset>("google-sheets-telemetry");
            if (asset != null) config = JsonUtility.FromJson<Config>(asset.text);
        }
        catch { Status = "Configuracion de telemetria invalida"; }
    }
    private static GoogleSheetsTelemetryService EnsureInstance()
    {
        if (instance != null) return instance;
        var go = new GameObject("GoogleSheetsTelemetryService");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GoogleSheetsTelemetryService>();
        return instance;
    }
    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this; DontDestroyOnLoad(gameObject);
        LoadPending(); StartCoroutine(SendLoop());
    }
    private void OnApplicationQuit() { quitting = true; }
    private void OnDestroy()
    {
        activeRequest?.Abort();
        if (instance == this) instance = null;
    }

    public static void QueueFinishedRun(string json)
    {
        if (!MaySend) return; // Never imports or uploads historical local runs on opt-in.
        try
        {
            var run = JsonUtility.FromJson<SheetsTelemetryRun>(json);
            if (run == null) return;
            run.installationId = UserSettings.GetTelemetryInstallationId();
            run.applicationSessionId = applicationSessionId ?? (applicationSessionId = Guid.NewGuid().ToString("N"));
            run.platform = Application.platform.ToString();
            if (quitting)
            {
                var pending = instance != null ? instance.outbox : ReadPending();
                if (pending.Enqueue(run, DateTime.UtcNow)) Save(pending);
                return; // Never creates scene objects or starts an HTTP request during shutdown.
            }
            var service = EnsureInstance();
            if (service.outbox.Enqueue(run, DateTime.UtcNow)) Save(service.outbox);
            Status = "Partida pendiente de envio";
        }
        catch { Status = "No se pudo preparar la telemetria"; }
    }

    public static void ConsentChanged(bool allowed)
    {
        if (!allowed) { ClearPending(); return; }
        if (MaySend && !quitting) EnsureInstance();
    }
    public static void ClearPending()
    {
        UserSettings.RotateTelemetryConsentEpoch(); // Also invalidates an in-flight acknowledgement after reset.
        if (instance != null)
        {
            instance.activeRequest?.Abort();
            instance.outbox = NewOutbox();
        }
        try
        {
            if (File.Exists(QueuePath)) File.Delete(QueuePath);
            if (File.Exists(QueuePath + ".tmp")) File.Delete(QueuePath + ".tmp");
        }
        catch { Debug.LogWarning("No se pudo borrar la cola de telemetria. Los pendientes antiguos quedan invalidados."); }
        Status = "Envios pendientes borrados";
    }
    private static SheetsTelemetryOutbox NewOutbox() => new SheetsTelemetryOutbox
    { consentEpoch = UserSettings.GetTelemetryConsentEpoch() };
    private void LoadPending() { outbox = ReadPending(); }
    private static SheetsTelemetryOutbox ReadPending()
    {
        try
        {
            if (!File.Exists(QueuePath)) return NewOutbox();
            if (new FileInfo(QueuePath).Length > 2 * 1024 * 1024) return NewOutbox();
            var pending = JsonUtility.FromJson<SheetsTelemetryOutbox>(File.ReadAllText(QueuePath));
            if (pending == null || pending.consentEpoch != UserSettings.GetTelemetryConsentEpoch()) return NewOutbox();
            int previousCount = pending.runs != null ? pending.runs.Count : -1;
            pending.Prune(DateTime.UtcNow);
            if (pending.runs.Count != previousCount) Save(pending);
            return pending;
        }
        catch { Status = "Cola local invalida; se conservan los registros de runs"; return NewOutbox(); }
    }
    private static void Save(SheetsTelemetryOutbox pending)
    {
        try
        {
            Directory.CreateDirectory(RunTelemetryStorage.DirectoryPath);
            string temp = QueuePath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(pending));
            if (!File.Exists(QueuePath)) File.Move(temp, QueuePath);
            else
            {
                try { File.Replace(temp, QueuePath, null); }
                catch (PlatformNotSupportedException) { File.Copy(temp, QueuePath, true); File.Delete(temp); }
            }
        }
        catch { Debug.LogWarning("No se pudo persistir la cola de telemetria; el registro local de runs es independiente."); }
    }
    private IEnumerator SendLoop()
    {
        while (true)
        {
            if (!quitting && MaySend && outbox.runs.Count > 0 && Time.realtimeSinceStartup >= nextAttempt)
                yield return SendBatch();
            yield return new WaitForSecondsRealtime(2f);
        }
    }
    private IEnumerator SendBatch()
    {
        outbox.Prune(DateTime.UtcNow);
        if (outbox.runs.Count == 0) yield break;
        var sent = outbox.runs.GetRange(0, Mathf.Min(Mathf.Clamp(config.batchSize, 1, 5), outbox.runs.Count));
        string epoch = outbox.consentEpoch;
        var batch = new Batch { ingestKey = config.ingestKey, runs = sent };
        using (var request = new UnityWebRequest(config.endpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(batch)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = Mathf.Clamp(config.requestTimeoutSeconds, 5, 30);
            request.redirectLimit = 5;
            activeRequest = request;
            yield return request.SendWebRequest();
            activeRequest = null;
            if (!MaySend || epoch != UserSettings.GetTelemetryConsentEpoch()) yield break;
            Reply reply = null;
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string body = request.downloadHandler.text;
                    if (body.Length <= 65536) reply = JsonUtility.FromJson<Reply>(body);
                }
                catch { }
            }
            if (reply != null)
            {
                int removed = outbox.Acknowledge(sent, reply.acceptedIds, reply.rejectedIds);
                if (removed > 0) Save(outbox);
                if (reply.ok && removed == sent.Count)
                {
                    failures = 0; nextAttempt = Time.realtimeSinceStartup + 10f;
                    Status = reply.rejectedIds != null && reply.rejectedIds.Length > 0 ? "El receptor rechazo registros invalidos"
                        : outbox.runs.Count == 0 ? "Telemetria enviada" : "Quedan partidas pendientes";
                    yield break;
                }
            }
            failures = Mathf.Min(failures + 1, 6);
            nextAttempt = Time.realtimeSinceStartup + Mathf.Min(900f, 30f * Mathf.Pow(2f, failures - 1)) + UnityEngine.Random.Range(0f, 8f);
            switch (reply != null ? reply.error : "")
            {
                case "unauthorized": Status = "Clave del receptor incorrecta; cola conservada"; break;
                case "capacity": Status = "Limite del receptor; cola conservada"; break;
                case "busy": Status = "Receptor ocupado; se reintentara"; break;
                case "server_error": Status = "Revisar configuracion del receptor"; break;
                default: Status = "Envio pendiente; revisar conexion y despliegue"; break;
            }
            if (failures == 1) Debug.LogWarning("Telemetria Sheets: " + Status + " (HTTP " + request.responseCode + ").");
            // Never logs the URL, ingest key, raw response, account IDs or the payload.
        }
    }
}
