using System;

namespace Vip.RestClient.Tests.Utils.Models;

internal sealed class CultureValues
{
    #region Propriedades

    public decimal Decimal { get; set; }
    public float Float { get; set; }
    public double Double { get; set; }
    public DateTime Date { get; set; }
    public int? Optional { get; set; }
    public LegacyValue Legacy { get; set; }

    #endregion
}