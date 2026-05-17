using System;

namespace SkportOneBot.Models;

public class TokenExpiredException : Exception
{
    public TokenExpiredException(string message) : base(message) { }
}
