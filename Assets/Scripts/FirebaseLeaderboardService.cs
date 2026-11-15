using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

public sealed class FirebaseLeaderboardService : MonoBehaviour
{
    [Serializable] private class Config { public string databaseUrl; public string apiKey; public int cacheSeconds = 120; }
    [Serializable] private class AuthResponse { public string idToken; public string refreshToken; public string localId; public string expiresIn; }
    [Serializable] private class RefreshResponse { public string id_token; public string refresh_token; public string user_id; public string expires_in; }

    private static FirebaseLeaderboardService instance;
    private Config config;
    private string idToken;
    private string refreshToken;
    private string uid;
    private long tokenExpiresUtc;
    private bool busy;
    private bool submitInFlight;
    private bool authInFlight;
    public static string Status { get; private set; } = "Firebase listo";

    public static bool IsConfigured => EnsureInstance().HasConfig;

    public static void RefreshTop(bool force = false)
    {
        FirebaseLeaderboardService service = EnsureInstance();
        if (service.HasConfig && !service.busy) service.StartCoroutine(service.RefreshRoutine(force));
    }

    public static void SubmitPersonalBest(RankingEntry entry)
    {
        FirebaseLeaderboardService service = EnsureInstance();
        if (service.HasConfig && !service.submitInFlight) service.StartCoroutine(service.SubmitQueuedRoutine(entry));
    }

    private bool HasConfig => config != null && !string.IsNullOrWhiteSpace(config.databaseUrl) && !string.IsNullOrWhiteSpace(config.apiKey) && !config.databaseUrl.Contains("YOUR_");

    private static FirebaseLeaderboardService EnsureInstance()
    {
        if (instance != null) return instance;
        GameObject root = new GameObject("FirebaseLeaderboardService");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<FirebaseLeaderboardService>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        TextAsset asset = Resources.Load<TextAsset>("firebase-leaderboard");
        if (asset != null) config = JsonUtility.FromJson<Config>(asset.text);
        Status = HasConfig ? "Firebase configurado" : "Firebase sin configurar";
        idToken = PlayerPrefs.GetString("firebase_lb_id_token", string.Empty);
        refreshToken = PlayerPrefs.GetString("firebase_lb_refresh_token", string.Empty);
        uid = PlayerPrefs.GetString("firebase_lb_uid", string.Empty);
        long.TryParse(PlayerPrefs.GetString("firebase_lb_expiry", "0"), out tokenExpiresUtc);
    }

    private IEnumerator EnsureAuth()
    {
        if (!string.IsNullOrEmpty(idToken) && !string.IsNullOrEmpty(uid) && !string.IsNullOrEmpty(refreshToken) &&
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() < tokenExpiresUtc - 60) yield break;
        if (authInFlight)
        {
            while (authInFlight) yield return null;
            yield break; // Share that attempt, including its failure; do not create a retry storm.
        }
        authInFlight = true;
        try { yield return AuthenticateIdentity(); }
        finally { authInFlight = false; }
    }

    private IEnumerator AuthenticateIdentity()
    {
        // Never let callers use the stale access token when renewal fails.
        idToken = string.Empty;
        tokenExpiresUtc = 0;

        if (!string.IsNullOrEmpty(refreshToken))
        {
            string body = "grant_type=refresh_token&refresh_token=" + UnityWebRequest.EscapeURL(refreshToken);
            using (UnityWebRequest request = new UnityWebRequest("https://securetoken.googleapis.com/v1/token?key=" + UnityWebRequest.EscapeURL(config.apiKey), "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    RefreshResponse response = JsonUtility.FromJson<RefreshResponse>(request.downloadHandler.text);
                    if (response != null && FirebaseIdentityPolicy.CanAccept(response.id_token, response.refresh_token, response.user_id, uid))
                        SaveAuth(response.id_token, response.refresh_token, response.user_id, response.expires_in);
                    else
                        Status = "Renovación inválida; cuenta conservada";
                    yield break;
                }
                Status = "No se pudo renovar la sesión; cuenta conservada";
                Debug.LogWarning($"Firebase token refresh: HTTP {request.responseCode}. Se conserva la identidad local.");
                yield break;
            }
        }

        if (!FirebaseIdentityPolicy.CanCreateAnonymous(uid, refreshToken))
        {
            Status = "Cuenta conservada: falta credencial de renovación";
            yield break;
        }

        using (UnityWebRequest request = CreateJsonRequest("https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" + UnityWebRequest.EscapeURL(config.apiKey), "POST", "{\"returnSecureToken\":true}"))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Status = "Error de autenticación";
                Debug.LogWarning($"Firebase anonymous auth: {request.responseCode} {request.error} {request.downloadHandler.text}");
                yield break;
            }
            AuthResponse response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
            if (response != null && FirebaseIdentityPolicy.CanAccept(response.idToken, response.refreshToken, response.localId, uid))
                SaveAuth(response.idToken, response.refreshToken, response.localId, response.expiresIn);
            else
                Status = "Respuesta de autenticación incompleta";
        }
    }

    private IEnumerator RefreshRoutine(bool force)
    {
        if (!force && RankingStorage.HasFreshGlobalCache(Mathf.Max(30, config.cacheSeconds))) yield break;
        busy = true;
        Status = "Conectando...";
        yield return EnsureAuth();
        if (string.IsNullOrEmpty(idToken))
        {
            if (Status == "Conectando...") Status = "Sin autenticación";
            busy = false;
            yield break;
        }
        string query = "?auth=" + UnityWebRequest.EscapeURL(idToken) + "&orderBy=%22score%22&limitToLast=15";
        using (UnityWebRequest request = UnityWebRequest.Get(DatabaseRoot + "/leaderboard.json" + query))
        {
            yield return request.SendWebRequest();
            if (request.responseCode == 401)
            {
                InvalidateAccessToken();
                yield return EnsureAuth();
                if (!string.IsNullOrEmpty(idToken))
                {
                    string retryQuery = "?auth=" + UnityWebRequest.EscapeURL(idToken) + "&orderBy=%22score%22&limitToLast=15";
                    using (UnityWebRequest retry = UnityWebRequest.Get(DatabaseRoot + "/leaderboard.json" + retryQuery))
                    {
                        yield return retry.SendWebRequest();
                        if (retry.result == UnityWebRequest.Result.Success)
                        {
                            RankingStorage.ReplaceGlobalCache(ParseEntries(retry.downloadHandler.text));
                            Status = "Ranking actualizado";
                            busy = false;
                            if (RankingStorage.TryGetPendingBest(out RankingEntry retryPending))
                                SubmitPersonalBest(retryPending);
                            yield break;
                        }
                        Debug.LogWarning($"Firebase leaderboard retry: {retry.responseCode} {retry.error} {retry.downloadHandler.text}");
                    }
                }
            }
            if (request.result == UnityWebRequest.Result.Success)
            {
                RankingStorage.ReplaceGlobalCache(ParseEntries(request.downloadHandler.text));
                Status = "Ranking actualizado";
            }
            else
            {
                Status = $"Error Firebase {request.responseCode}";
                Debug.LogWarning($"Firebase leaderboard read: {request.responseCode} {request.error} {request.downloadHandler.text}");
            }
        }
        busy = false;
        if (Status == "Ranking actualizado" && RankingStorage.TryGetPendingBest(out RankingEntry pending))
            SubmitPersonalBest(pending);
    }

    private IEnumerator SubmitQueuedRoutine(RankingEntry entry)
    {
        if (submitInFlight) yield break;
        submitInFlight = true;
        bool anyWritten = false;
        try
        {
            // Keep one uploader. A better run can update the persisted pending
            // entry while a request is in flight; pick it up after that response.
            if (RankingStorage.TryGetPendingBest(out RankingEntry newest) && newest.score > entry.score)
                entry = newest;
            while (true)
            {
                bool confirmed = false;
                yield return SubmitRoutine(entry, written => { confirmed = true; anyWritten |= written; });
                if (!confirmed) break; // Preserve pending data; no retry loop on errors.
                if (!RankingStorage.TryGetPendingBest(out entry)) break;
            }
        }
        finally
        {
            submitInFlight = false;
        }
        // One top-list refresh per successful batch, not per intermediate record.
        if (anyWritten) RefreshTop(true);
    }

    private IEnumerator SubmitRoutine(RankingEntry entry, Action<bool> onConfirmed)
    {
        yield return EnsureAuth();
        if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(uid)) yield break;
        string url = DatabaseRoot + "/leaderboard/" + UnityWebRequest.EscapeURL(uid) + ".json?auth=" + UnityWebRequest.EscapeURL(idToken);
        string etag = null;
        int remoteScore = -1;
        using (UnityWebRequest get = UnityWebRequest.Get(url))
        {
            get.SetRequestHeader("X-Firebase-ETag", "true");
            yield return get.SendWebRequest();
            if (get.result != UnityWebRequest.Result.Success)
            {
                Status = $"Error Firebase {get.responseCode}";
                Debug.LogWarning($"Firebase personal best read: {get.responseCode} {get.error} {get.downloadHandler.text}");
                yield break;
            }
            etag = get.GetResponseHeader("ETag");
            if (get.downloadHandler.text != "null") remoteScore = JsonUtility.FromJson<RankingEntry>(get.downloadHandler.text).score;
        }
        RankingStorage.UpdatePersonalBestFloor(remoteScore);
        // Coalesce a better score that arrived during authentication/the GET,
        // before spending a write on the older snapshot.
        if (RankingStorage.TryGetPendingBest(out RankingEntry latest) && latest.score > entry.score)
            entry = latest;
        if (entry.score <= remoteScore)
        {
            RankingStorage.ClearPendingBestUpTo(remoteScore);
            Status = "Récord global sincronizado";
            onConfirmed(false);
            yield break;
        }
        if (string.IsNullOrEmpty(etag))
        {
            Status = "Récord pendiente: falta ETag";
            yield break;
        }
        entry.playerName = RankingStorage.SanitizePlayerName(entry.playerName);
        using (UnityWebRequest put = CreateJsonRequest(url, "PUT", JsonUtility.ToJson(entry)))
        {
            put.SetRequestHeader("if-match", etag);
            yield return put.SendWebRequest();
            if (put.responseCode == 200 || put.responseCode == 204)
            {
                RankingStorage.UpdatePersonalBestFloor(entry.score);
                RankingStorage.ClearPendingBestUpTo(entry.score);
                Status = "Récord global guardado";
                onConfirmed(true);
            }
            else
            {
                Status = $"Error al guardar {put.responseCode}";
                Debug.LogWarning($"Firebase leaderboard write: {put.responseCode} {put.error} {put.downloadHandler.text}");
            }
        }
    }

    private void SaveAuth(string token, string refresh, string user, string expires)
    {
        idToken = token ?? string.Empty; refreshToken = refresh ?? string.Empty; uid = user ?? string.Empty;
        long seconds = 3600; long.TryParse(expires, out seconds);
        tokenExpiresUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(60, seconds);
        PlayerPrefs.SetString("firebase_lb_id_token", idToken); PlayerPrefs.SetString("firebase_lb_refresh_token", refreshToken);
        PlayerPrefs.SetString("firebase_lb_uid", uid); PlayerPrefs.SetString("firebase_lb_expiry", tokenExpiresUtc.ToString()); PlayerPrefs.Save();
    }

    private void InvalidateAccessToken()
    {
        idToken = string.Empty;
        tokenExpiresUtc = 0;
        PlayerPrefs.DeleteKey("firebase_lb_id_token");
        PlayerPrefs.DeleteKey("firebase_lb_expiry");
        PlayerPrefs.Save();
    }

    private string DatabaseRoot => config.databaseUrl.TrimEnd('/');

    private static UnityWebRequest CreateJsonRequest(string url, string method, string json)
    {
        UnityWebRequest request = new UnityWebRequest(url, method);
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        return request;
    }

    private static List<RankingEntry> ParseEntries(string json)
    {
        List<RankingEntry> entries = new List<RankingEntry>();
        if (string.IsNullOrWhiteSpace(json) || json == "null") return entries;
        foreach (Match match in Regex.Matches(json, "\\\"[^\\\"]+\\\"\\s*:\\s*(\\{[^{}]*\\})"))
        {
            try { entries.Add(JsonUtility.FromJson<RankingEntry>(match.Groups[1].Value)); } catch { }
        }
        return entries;
    }
}
