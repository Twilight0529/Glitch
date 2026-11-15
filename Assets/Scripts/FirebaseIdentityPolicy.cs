public static class FirebaseIdentityPolicy
{
    public static bool CanCreateAnonymous(string savedUid, string savedRefreshToken)
    {
        return string.IsNullOrWhiteSpace(savedUid) && string.IsNullOrWhiteSpace(savedRefreshToken);
    }

    public static bool CanAccept(string token, string refresh, string receivedUid, string savedUid)
    {
        return !string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(refresh) &&
            !string.IsNullOrWhiteSpace(receivedUid) &&
            (string.IsNullOrWhiteSpace(savedUid) || savedUid == receivedUid);
    }
}
