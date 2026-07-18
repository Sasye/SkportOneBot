using System;
using System.Threading.Tasks;
using SkportOneBot.Models;

namespace SkportOneBot.Services;

public class SkportAuthManager
{
    private readonly SkportService _skportService;
    private readonly ProfileManager _profileManager;

    public SkportAuthManager(SkportService skportService, ProfileManager profileManager)
    {
        _skportService = skportService;
        _profileManager = profileManager;
    }

    /// <summary>
    /// 使用指定的账号配置执行 Skport API 调用。如果遇到 Token 过期，则自动进行密码续期并重试。
    /// </summary>
    public async Task<T> ExecuteWithSessionAsync<T>(Profile profile, Func<SkportSession, Task<T>> apiCall)
    {
        var session = new SkportSession(profile.Cred, profile.Token);
        try
        {
            return await apiCall(session);
        }
        catch (TokenExpiredException)
        {
            if (string.IsNullOrEmpty(profile.Account) || string.IsNullOrEmpty(profile.Password))
            {
                throw new InvalidOperationException("Token已过期，且未保存登录密码，请重新绑定！");
            }

            Console.WriteLine($"[Auth] {profile.AccountName} 的 Token 已过期，正在尝试使用密码自动刷新...");
            try
            {
                var authResult = await _skportService.LoginByPasswordAsync(profile.Account, profile.Password);
                var skSession = await _skportService.LoginByTokenAsync(authResult.token);
                
                profile.Cred = skSession.Cred;
                profile.Token = skSession.SignToken;
                _profileManager.AddOrUpdateProfile(profile);

                Console.WriteLine($"[Auth] {profile.AccountName} 的 Token 刷新成功，正在继续执行挂起的请求...");
                session = new SkportSession(profile.Cred, profile.Token);
                return await apiCall(session);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"自动刷新 Token 失败，密码可能已被修改，请重新绑定！({ex.Message})");
            }
        }
    }
}
