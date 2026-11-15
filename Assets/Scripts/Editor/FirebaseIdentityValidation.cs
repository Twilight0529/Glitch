using System;
using UnityEditor;
using UnityEngine;

public static class FirebaseIdentityValidation
{
    [MenuItem("GLITCH/Validation/Firebase identity preservation")]
    public static void Run() => Debug.Log($"Firebase identity: {Validate()} checks passed.");
    public static int Validate()
    {
        Check(FirebaseIdentityPolicy.CanCreateAnonymous(null, null), "new installation may register");
        Check(FirebaseIdentityPolicy.CanCreateAnonymous("", ""), "empty installation may register");
        Check(!FirebaseIdentityPolicy.CanCreateAnonymous("uid", "refresh"), "existing identity cannot be replaced");
        Check(!FirebaseIdentityPolicy.CanCreateAnonymous("uid", null), "missing renewal credential does not erase UID");
        Check(!FirebaseIdentityPolicy.CanCreateAnonymous(null, "refresh"), "recoverable renewal is preserved");
        Check(FirebaseIdentityPolicy.CanAccept("token", "refresh", "uid", "uid"), "same UID renewal accepted");
        Check(FirebaseIdentityPolicy.CanAccept("token", "refresh", "uid", null), "initial complete response accepted");
        Check(!FirebaseIdentityPolicy.CanAccept("token", "refresh", "other", "uid"), "different UID cannot silently replace identity");
        Check(!FirebaseIdentityPolicy.CanAccept(null, "refresh", "uid", "uid"), "missing token rejected");
        Check(!FirebaseIdentityPolicy.CanAccept("token", null, "uid", "uid"), "missing renewal rejected");
        Check(!FirebaseIdentityPolicy.CanAccept("token", "refresh", null, null), "missing UID rejected");
        return 11;
    }
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("Firebase identity: " + label);
    }
}
