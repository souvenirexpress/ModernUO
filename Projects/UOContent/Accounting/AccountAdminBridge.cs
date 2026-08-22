using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server.Misc;

namespace Server.Accounting;

public static class AccountAdminBridge
{
    private const string EmailTag = "AbadoriaAdminEmail";
    private const string AccessTag = "AbadoriaServerAccess";
    private const string CharacterBlockPrefix = "AbadoriaCharacterBlocked:";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static string _root;
    private static DateTime _nextSnapshot = DateTime.MinValue;
    private static bool _enabled;

    public static void Configure()
    {
        _enabled = string.Equals(Environment.GetEnvironmentVariable("ABADORIA_ADMIN_BRIDGE_ENABLED"), "true", StringComparison.OrdinalIgnoreCase);
        _root = Environment.GetEnvironmentVariable("ABADORIA_ADMIN_BRIDGE_ROOT");
    }

    public static void Initialize()
    {
        if (!_enabled || string.IsNullOrWhiteSpace(_root))
        {
            return;
        }

        Directory.CreateDirectory(CommandRoot);
        Directory.CreateDirectory(ResultRoot);
        Timer.DelayCall(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), Tick);
    }

    public static bool IsCharacterBlocked(Account account, Mobile mobile) =>
        string.Equals(account?.GetTag(CharacterBlockPrefix + mobile?.Serial.Value), "true", StringComparison.OrdinalIgnoreCase);

    public static bool HasServerAccess(Account account) =>
        !string.Equals(account?.GetTag(AccessTag), "false", StringComparison.OrdinalIgnoreCase);

    private static string CommandRoot => Path.Combine(_root, "commands");
    private static string ResultRoot => Path.Combine(_root, "results");
    private static string SnapshotPath => Path.Combine(_root, "snapshot.json");

    private static void Tick()
    {
        foreach (var path in Directory.EnumerateFiles(CommandRoot, "*.json").OrderBy(File.GetCreationTimeUtc))
        {
            Process(path);
        }

        if (Core.Now >= _nextSnapshot)
        {
            WriteSnapshot();
            _nextSnapshot = Core.Now + TimeSpan.FromSeconds(5);
        }
    }

    private static void Process(string path)
    {
        AccountAdminCommand command = null;
        AccountAdminResult result;
        try
        {
            command = JsonSerializer.Deserialize<AccountAdminCommand>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidDataException("Leerer Admin-Befehl.");
            if (!Guid.TryParse(command.Id, out _))
            {
                throw new InvalidDataException("Ungueltige Befehls-ID.");
            }

            var message = Execute(command);
            World.Save();
            WriteSnapshot();
            result = new AccountAdminResult(command.Id, true, message, null, Core.Now);
        }
        catch (Exception exception)
        {
            result = new AccountAdminResult(command?.Id ?? Path.GetFileNameWithoutExtension(path), false, null, exception.Message, Core.Now);
        }

        WriteAtomic(Path.Combine(ResultRoot, result.Id + ".json"), result);
        File.Delete(path);
    }

    private static string Execute(AccountAdminCommand command) => command.Action switch
    {
        "createAccount" => CreateAccount(command),
        "renameAccount" => RenameAccount(command),
        "setAccount" => SetAccount(command),
        "resetPassword" => ResetPassword(command),
        "deleteAccount" => DeleteAccount(command),
        "renameCharacter" => RenameCharacter(command),
        "setCharacterBlocked" => SetCharacterBlocked(command),
        "deleteCharacter" => DeleteCharacter(command),
        _ => throw new InvalidDataException("Unbekannte Admin-Aktion.")
    };

    private static string CreateAccount(AccountAdminCommand command)
    {
        var username = RequiredUsername(command);
        if (Accounts.GetAccount(username) != null)
        {
            throw new InvalidOperationException("Das Spielkonto existiert bereits.");
        }

        if (!AccountHandler.IsValidUsername(username) || !AccountHandler.IsValidPassword(command.Password))
        {
            throw new InvalidDataException("Benutzername oder Passwort ist ungueltig.");
        }

        var account = new Account(username, command.Password);
        ApplyAccount(account, command);
        return "Spielkonto wurde angelegt.";
    }

    private static string SetAccount(AccountAdminCommand command)
    {
        var account = RequiredAccount(command);
        ApplyAccount(account, command);
        return "Spielkonto wurde aktualisiert.";
    }

    private static string RenameAccount(AccountAdminCommand command)
    {
        var account = RequiredAccount(command);
        EnsureOffline(account);
        var currentUsername = account.Username;
        var newUsername = command.NewUsername?.Trim();
        if (string.IsNullOrWhiteSpace(newUsername) ||
            !AccountHandler.IsValidUsername(newUsername) ||
            !AccountHandler.IsValidPassword(command.Password) ||
            currentUsername.Equals(newUsername, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Benutzername oder Passwort ist ungueltig.");
        }
        if (!account.TrySetUsername(newUsername))
        {
            throw new InvalidOperationException("Der neue Benutzername ist bereits vergeben.");
        }

        try
        {
            // Legacy SHA password formats include the username in their input.
            account.SetPassword(command.Password);
        }
        catch
        {
            account.TrySetUsername(currentUsername);
            throw;
        }
        return "Spielkonto wurde umbenannt.";
    }

    private static void ApplyAccount(Account account, AccountAdminCommand command)
    {
        if (command.Email != null)
        {
            account.Email = command.Email.Trim();
            account.SetTag(EmailTag, command.Email.Trim());
        }
        if (command.Role != null)
        {
            account.AccessLevel = command.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                ? AccessLevel.Administrator
                : AccessLevel.Player;
        }
        if (command.Active.HasValue)
        {
            account.SetTag(AccessTag, command.Active.Value ? "true" : "false");
            if (!command.Active.Value) Disconnect(account, "Serverzugriff wurde deaktiviert.");
        }
        if (command.Blocked.HasValue)
        {
            account.Banned = command.Blocked.Value;
            if (command.Blocked.Value) account.SetUnspecifiedBan(null);
            else account.SetBanTags(null, DateTime.MinValue, TimeSpan.Zero);
            if (command.Blocked.Value) Disconnect(account, "Konto wurde gesperrt.");
        }
    }

    private static string ResetPassword(AccountAdminCommand command)
    {
        if (!AccountHandler.IsValidPassword(command.Password))
        {
            throw new InvalidDataException("Das Passwort ist ungueltig.");
        }
        RequiredAccount(command).SetPassword(command.Password);
        return "Passwort wurde zurueckgesetzt.";
    }

    private static string DeleteAccount(AccountAdminCommand command)
    {
        var account = RequiredAccount(command);
        EnsureOffline(account);
        account.Delete();
        return "Spielkonto und Charaktere wurden geloescht.";
    }

    private static string RenameCharacter(AccountAdminCommand command)
    {
        var (_, mobile) = RequiredCharacter(command);
        var name = command.CharacterName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length is < 2 or > 30 || name.Any(char.IsControl))
        {
            throw new InvalidDataException("Der Charaktername ist ungueltig.");
        }
        mobile.Name = name;
        return "Charakter wurde umbenannt.";
    }

    private static string SetCharacterBlocked(AccountAdminCommand command)
    {
        var (account, mobile) = RequiredCharacter(command);
        if (!command.CharacterBlocked.HasValue)
        {
            throw new InvalidDataException("Sperrstatus fehlt.");
        }
        var key = CharacterBlockPrefix + mobile.Serial.Value;
        if (command.CharacterBlocked.Value)
        {
            account.SetTag(key, "true");
            mobile.NetState?.Disconnect("Charakter wurde gesperrt.");
        }
        else
        {
            account.RemoveTag(key);
        }
        return command.CharacterBlocked.Value ? "Charakter wurde gesperrt." : "Charakter wurde entsperrt.";
    }

    private static string DeleteCharacter(AccountAdminCommand command)
    {
        var (account, mobile) = RequiredCharacter(command);
        if (mobile.NetState != null)
        {
            throw new InvalidOperationException("Ein eingeloggter Charakter kann nicht geloescht werden.");
        }
        for (var index = 0; index < account.Length; index++)
        {
            if (account[index] != mobile) continue;
            mobile.Delete();
            account[index] = null;
            account.RemoveTag(CharacterBlockPrefix + mobile.Serial.Value);
            return "Charakter wurde geloescht.";
        }
        throw new InvalidOperationException("Charakter wurde nicht gefunden.");
    }

    private static Account RequiredAccount(AccountAdminCommand command) =>
        Accounts.GetAccount(RequiredUsername(command)) as Account
        ?? throw new InvalidOperationException("Spielkonto wurde nicht gefunden.");

    private static string RequiredUsername(AccountAdminCommand command)
    {
        var username = command.Username?.Trim();
        return string.IsNullOrWhiteSpace(username) ? throw new InvalidDataException("Benutzername fehlt.") : username;
    }

    private static (Account Account, Mobile Mobile) RequiredCharacter(AccountAdminCommand command)
    {
        var account = RequiredAccount(command);
        if (!command.CharacterSerial.HasValue)
        {
            throw new InvalidDataException("Charakter-ID fehlt.");
        }
        for (var index = 0; index < account.Length; index++)
        {
            var mobile = account[index];
            if (mobile?.Serial.Value == command.CharacterSerial.Value) return (account, mobile);
        }
        throw new InvalidOperationException("Charakter wurde nicht gefunden.");
    }

    private static void EnsureOffline(Account account)
    {
        for (var index = 0; index < account.Length; index++)
        {
            if (account[index]?.NetState != null)
                throw new InvalidOperationException("Das Konto ist gerade eingeloggt.");
        }
    }

    private static void Disconnect(Account account, string reason)
    {
        for (var index = 0; index < account.Length; index++) account[index]?.NetState?.Disconnect(reason);
    }

    private static void WriteSnapshot()
    {
        var accounts = Accounts.GetAccounts().OfType<Account>().OrderBy(account => account.Username).Select(account =>
        {
            var characters = new List<AccountAdminCharacter>();
            for (var index = 0; index < account.Length; index++)
            {
                var mobile = account[index];
                if (mobile == null) continue;
                characters.Add(new AccountAdminCharacter(
                    mobile.Serial.Value,
                    index,
                    mobile.Name ?? "Unbenannt",
                    IsCharacterBlocked(account, mobile),
                    mobile.NetState != null,
                    mobile.Map?.Name ?? mobile.LogoutMap?.Name ?? "Internal",
                    mobile.X,
                    mobile.Y,
                    mobile.Z,
                    mobile.Created
                ));
            }
            return new AccountAdminSnapshotAccount(
                account.Username,
                account.Email ?? account.GetTag(EmailTag) ?? string.Empty,
                account.AccessLevel >= AccessLevel.Administrator ? "Admin" : "User",
                account.Banned,
                HasServerAccess(account),
                account.Created,
                account.LastLogin,
                characters
            );
        }).ToArray();
        WriteAtomic(SnapshotPath, new AccountAdminSnapshot(Core.Now, accounts));
    }

    private static void WriteAtomic<T>(string path, T value)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, JsonOptions));
        File.Move(temporary, path, true);
    }
}

public sealed record AccountAdminCommand
{
    public string Id { get; init; }
    public string Action { get; init; }
    public string Username { get; init; }
    public string NewUsername { get; init; }
    public string Password { get; init; }
    public string Email { get; init; }
    public string Role { get; init; }
    public bool? Active { get; init; }
    public bool? Blocked { get; init; }
    public uint? CharacterSerial { get; init; }
    public string CharacterName { get; init; }
    public bool? CharacterBlocked { get; init; }
}

public sealed record AccountAdminResult(string Id, bool Success, string Message, string Error, DateTime CreatedAt);
public sealed record AccountAdminSnapshot(DateTime CreatedAt, IReadOnlyList<AccountAdminSnapshotAccount> Accounts);
public sealed record AccountAdminSnapshotAccount(string Username, string Email, string Role, bool Blocked, bool Active, DateTime CreatedAt, DateTime LastLoginAt, IReadOnlyList<AccountAdminCharacter> Characters);
public sealed record AccountAdminCharacter(uint Serial, int Slot, string Name, bool Blocked, bool Online, string Map, int X, int Y, int Z, DateTime CreatedAt);
