using System;

namespace Vip.RestClient.Tests.Utils.Models;

internal sealed class ThrowingPayload
{
    #region Propriedades

    public string Value
    {
        get
        {
            GetterCalled = true;
            throw new InvalidOperationException("getter");
        }
    }

    #endregion

    #region Estaticos

    public static bool GetterCalled;

    #endregion
}