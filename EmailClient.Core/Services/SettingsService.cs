using System.Text.Json;
using EmailClient.Core.Models;

public class SettingsService
{
    private const string SettingsFileName = "emailclient.settings.json";
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public SettingsService()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EmailClient",
            SettingsFileName);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    public void SaveAccounts(List<EmailAccount> accounts)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory!);

            var json = JsonSerializer.Serialize(accounts, _jsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to save accounts: {ex.Message}", ex);
        }
    }

    public List<EmailAccount> LoadAccounts()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new List<EmailAccount>();

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<List<EmailAccount>>(json, _jsonOptions) ?? new List<EmailAccount>();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to load accounts: {ex.Message}", ex);
        }
    }
}