namespace SkportOneBot.Models;

public record MessageContext(string Text, string? MessageType, long UserId, long? GroupId);
