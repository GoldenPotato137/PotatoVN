using System;

namespace GalgameManager.Models;

public class PvnException(string msg) : Exception(msg)
{
    public string FullMsg { get; protected set; } = msg;
}