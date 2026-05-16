# SkportOneBot

[English](README_EN.md) | 中文

这是一个基于 .NET 10开发的明日方舟：终末地自动签到机器人，通过OneBotv11协议接入 QQ。

## 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- 一款兼容 OneBot v11 标准的客户端（例如 LLOneBot, NapCat 等）。

## 快速开始

1. **克隆项目**
   ```bash
   git clone <你的仓库地址>
   cd SkportOneBot
   ```

2. **配置**
   复制 `config.json.example` 并重命名为 `config.json`：
   ```json
   {
     "onebot": {
       "ws_url": "ws://127.0.0.1:6700", // 你的 OneBot 客户端反向 WebSocket 地址
       "access_token": ""               // WebSocket 访问令牌（可选）
     },
     "bind_port": 7777,                 // 机器人 Web 服务运行端口
     "bind_url": "http://127.0.0.1:7777", // 发给用户的绑定链接域名（配合反代时使用）
     "cron_schedule": "0 10 * * *",     // 自动签到定时器（默认每天上午10点）
     "allowed_groups": []               // 允许使用指令的 QQ 群白名单（空数组为不限制）
   }
   ```

3. **编译与运行**
   ```bash
   # 直接运行
   dotnet run -c Release

   # 或编译为独立的单文件程序
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
   ```

## 机器人指令列表

发送指令给机器人（支持群聊与私聊）：

- **`终末地绑定`** 或 **`zmdbd`** 或 **`skport绑定`**
  获取一个用于网页绑定的临时专属链接（10分钟内有效）。
- **`终末地签到`** 或 **`zmdqd`** 或 **`skport签到`**
  手动触发你账号下的所有终末地角色签到。
- **`绑定列表`** 或 **`终末地绑定列表`** 或 **`zmdbdlb`**
  查看当前你已绑定的所有角色列表与对应序号。
- **`删除绑定 [序号]`**
  精准删除某个指定的角色绑定。
- **`删除全部绑定`** 或 **`解绑全部`**
  一键清空你在此机器人的所有账号数据与凭据。
