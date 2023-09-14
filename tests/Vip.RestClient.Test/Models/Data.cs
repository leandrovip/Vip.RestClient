using System;

namespace Vip.RestClient.Test.Models;

public class Data
{
    #region Properties

    public int Id { get; set; }
    public Guid Uid { get; set; }
    public string Text { get; set; }
    public int Number { get; set; }
    public double DoubleFloatPoint { get; set; }

    #endregion

    #region Methods

    public override string ToString()
    {
        return $"Id: {Id} - UID: {Uid} - Text: {Text} - Number: {Number}";
    }

    #endregion
}