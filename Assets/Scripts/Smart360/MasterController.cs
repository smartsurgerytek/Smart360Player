﻿using Sirenix.OdinInspector;
using UnityEngine;

public class MasterController : SerializedMonoBehaviour
{
    [SerializeField] ICredentialVerifier _credentialVerifier;
    [SerializeField] IVerificationResultReceiver _verificationResultReceiver;
    [SerializeField] IEditionButtonPreinitializer _editionButtonPreinitializer;

    public IEditionButtonPreinitializer editionButtonPreinitializer { get => _editionButtonPreinitializer; }
    public ICredentialVerifier credentialVerifier { get => _credentialVerifier; }

    internal void Initialize(MasterContext context)
    {
        context.verification.result = credentialVerifier.Verify(context.credential.credential);
    }

    internal void InternalUpdate(MasterContext context, MasterView view)
    {

    }
}
