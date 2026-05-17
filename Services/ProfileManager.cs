using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SkportOneBot.Models;

namespace SkportOneBot.Services;

public class ProfileManager
{
    private readonly string _profilesPath;
    private List<Profile> _profiles = new();

    public ProfileManager(string profilesPath = "profiles.json")
    {
        _profilesPath = profilesPath;
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_profilesPath))
            {
                var json = File.ReadAllText(_profilesPath);
                _profiles = JsonSerializer.Deserialize<List<Profile>>(json) ?? new List<Profile>();
            }
            else
            {
                File.WriteAllText(_profilesPath, "[]");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载 profiles.json 失败: {ex.Message}");
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_profiles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_profilesPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存 profiles.json 失败: {ex.Message}");
        }
    }

    public List<Profile> GetAllProfiles() => _profiles.ToList();

    public List<Profile> GetProfilesByUser(long userId) => _profiles.Where(p => p.UserId == userId).ToList();

    public void AddOrUpdateProfile(Profile profile)
    {
        var existing = _profiles.FirstOrDefault(p => p.RoleId == profile.RoleId && p.ServerId == profile.ServerId);
        if (existing != null)
        {
            existing.Account = profile.Account;
            existing.Password = profile.Password;
            existing.Cred = profile.Cred;
            existing.Token = profile.Token;
            existing.UserId = profile.UserId;
            existing.BindMessageType = profile.BindMessageType;
            existing.BindGroupId = profile.BindGroupId;
        }
        else
        {
            _profiles.Add(profile);
        }
        Save();
    }

    public void RemoveProfilesByUser(long userId)
    {
        _profiles.RemoveAll(p => p.UserId == userId);
        Save();
    }
    
    public void ReplaceAllProfiles(List<Profile> newProfiles)
    {
        _profiles = newProfiles;
        Save();
    }
}
