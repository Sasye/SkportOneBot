# SkportOneBot

English | [中文](README.md)

This is a fully automated daily sign-in bot for *Arknights: Endfield*, developed in .NET 10 and connected to QQ via the OneBot v11 protocol.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A OneBot v11 compatible client (e.g., LLOneBot, NapCat).

## Getting Started

1. **Clone the project**
   ```bash
   git clone <your-repository-url>
   cd SkportOneBot
   ```

2. **Configuration**
   Copy `config.json.example` and rename it to `config.json`:
   ```json
   {
     "onebot": {
       "ws_url": "ws://127.0.0.1:6700", // Your OneBot client WebSocket URL
       "access_token": ""               // WebSocket access token (optional)
     },
     "bind_port": 7777,                 // The port for the internal Web server
     "bind_url": "http://127.0.0.1:7777", // The public URL sent to users for binding
     "cron_schedule": "0 10 * * *",     // Auto sign-in schedule (default: every day at 10:00 AM)
     "allowed_groups": []               // Whitelisted QQ groups (empty means all allowed)
   }
   ```

3. **Build & Run**
   ```bash
   # Run directly
   dotnet run -c Release

   # Or publish as a standalone single-file executable
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
   ```

## Bot Commands

Send the following commands to the bot (works in both group and private chats):

- **`终末地绑定`** / **`zmdbd`** / **`skport绑定`**
  Request a personal, temporary web link to bind your account (valid for 10 minutes).
- **`终末地签到`** / **`zmdqd`** / **`skport签到`**
  Manually trigger the sign-in process for all your bound characters.
- **`绑定列表`** / **`终末地绑定列表`** / **`zmdbdlb`**
  View the list of your currently bound characters along with their index numbers.
- **`删除绑定 [index]`**
  Delete a specific character binding using its index number.
- **`删除全部绑定`** / **`解绑全部`**
  Completely wipe all your account data and credentials from the bot.
