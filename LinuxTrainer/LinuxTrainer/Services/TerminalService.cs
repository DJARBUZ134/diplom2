using System.Text;
using System.Text.Json;
using LinuxTrainer.Data;
using LinuxTrainer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinuxTrainer.Services;

public record TerminalView(string Cwd, string Prompt, IReadOnlyList<string> History);

public interface ITerminalService
{
    Task<TerminalView> LoadAsync(int userId, string login, string role, int? questId = null);
    Task<(string Output, TerminalView View)> ExecuteAsync(int userId, string login, string role, string command, int? questId = null);
    Task ResetAsync(int userId, int? questId = null);
}

public class TerminalService : ITerminalService
{
    private readonly AppDbContext _db;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public TerminalService(AppDbContext db) => _db = db;

    public async Task<TerminalView> LoadAsync(int userId, string login, string role, int? questId = null)
    {
        var (cwd, _, historyJson) = await GetOrCreateAsync(userId, login, questId);
        var history = JsonSerializer.Deserialize<List<string>>(historyJson) ?? new();
        return new TerminalView(cwd, BuildPrompt(login, role, cwd), history);
    }

    public async Task<(string Output, TerminalView View)> ExecuteAsync(int userId, string login, string role, string command, int? questId = null)
    {
        var (cwd, fsJson, historyJson) = await GetOrCreateAsync(userId, login, questId);

        var fs = JsonSerializer.Deserialize<VfsNode>(fsJson, JsonOpts) ?? BuildDefaultFs(login);
        FixupParents(fs, null);

        var history = JsonSerializer.Deserialize<List<string>>(historyJson) ?? new();
        if (!string.IsNullOrWhiteSpace(command))
        {
            history.Add(command);
            if (history.Count > 200) history.RemoveAt(0);
        }

        var output = Run(fs, ref cwd, command, login, role);

        var newFsJson = JsonSerializer.Serialize(fs, JsonOpts);
        var newHistoryJson = JsonSerializer.Serialize(history);

        await SaveAsync(userId, questId, cwd, newFsJson, newHistoryJson);

        return (output, new TerminalView(cwd, BuildPrompt(login, role, cwd), history));
    }

    public async Task ResetAsync(int userId, int? questId = null)
    {
        if (questId is null)
        {
            var state = await _db.TerminalStates.FirstOrDefaultAsync(t => t.UserId == userId);
            if (state is null) return;
            var login = (await _db.Users.FindAsync(userId))?.Login ?? "user";
            state.Cwd = $"/home/{login}";
            state.HistoryJson = "[]";
            state.FileSystemJson = JsonSerializer.Serialize(BuildDefaultFs(login), JsonOpts);
            await _db.SaveChangesAsync();
        }
        else
        {
            var state = await _db.QuestTerminalStates
                .FirstOrDefaultAsync(t => t.UserId == userId && t.QuestId == questId);
            if (state is null) return;
            var login = (await _db.Users.FindAsync(userId))?.Login ?? "user";
            var quest = await _db.Quests.FindAsync(questId.Value);
            state.Cwd = $"/home/{login}";
            state.HistoryJson = "[]";
            state.FileSystemJson = !string.IsNullOrEmpty(quest?.InitialFileSystemJson)
                ? quest!.InitialFileSystemJson
                : JsonSerializer.Serialize(BuildDefaultFs(login), JsonOpts);
            await _db.SaveChangesAsync();
        }
    }

    private async Task<(string Cwd, string FsJson, string HistoryJson)> GetOrCreateAsync(int userId, string login, int? questId)
    {
        if (questId is null)
        {
            var state = await _db.TerminalStates.FirstOrDefaultAsync(t => t.UserId == userId);
            if (state is null)
            {
                state = new TerminalState
                {
                    UserId = userId,
                    Cwd = $"/home/{login}",
                    HistoryJson = "[]",
                    FileSystemJson = JsonSerializer.Serialize(BuildDefaultFs(login), JsonOpts)
                };
                _db.TerminalStates.Add(state);
                await _db.SaveChangesAsync();
            }
            else if (string.IsNullOrEmpty(state.FileSystemJson))
            {
                state.FileSystemJson = JsonSerializer.Serialize(BuildDefaultFs(login), JsonOpts);
                await _db.SaveChangesAsync();
            }
            return (state.Cwd, state.FileSystemJson, state.HistoryJson);
        }
        else
        {
            var state = await _db.QuestTerminalStates
                .FirstOrDefaultAsync(t => t.UserId == userId && t.QuestId == questId);
            if (state is null)
            {
                var quest = await _db.Quests.FindAsync(questId.Value);
                var fs = !string.IsNullOrEmpty(quest?.InitialFileSystemJson)
                    ? quest!.InitialFileSystemJson
                    : JsonSerializer.Serialize(BuildDefaultFs(login), JsonOpts);
                state = new QuestTerminalState
                {
                    UserId = userId,
                    QuestId = questId.Value,
                    Cwd = $"/home/{login}",
                    HistoryJson = "[]",
                    FileSystemJson = fs
                };
                _db.QuestTerminalStates.Add(state);
                await _db.SaveChangesAsync();
            }
            return (state.Cwd, state.FileSystemJson, state.HistoryJson);
        }
    }

    private async Task SaveAsync(int userId, int? questId, string cwd, string fsJson, string historyJson)
    {
        if (questId is null)
        {
            var state = await _db.TerminalStates.FirstAsync(t => t.UserId == userId);
            state.Cwd = cwd;
            state.FileSystemJson = fsJson;
            state.HistoryJson = historyJson;
        }
        else
        {
            var state = await _db.QuestTerminalStates
                .FirstAsync(t => t.UserId == userId && t.QuestId == questId);
            state.Cwd = cwd;
            state.FileSystemJson = fsJson;
            state.HistoryJson = historyJson;
        }
        await _db.SaveChangesAsync();
    }

    private static string BuildPrompt(string login, string role, string cwd)
    {
        var home = $"/home/{login}";
        var shown = cwd == home ? "~" : cwd.StartsWith(home + "/") ? "~" + cwd[home.Length..] : cwd;
        var symbol = role == Roles.Admin ? "#" : "$";
        var user = role == Roles.Admin ? "root" : login;
        return $"{user}@ubuntu:{shown}{symbol} ";
    }

    private static VfsNode BuildDefaultFs(string login) => VfsNode.Dir("",
        VfsNode.Dir("etc",
            VfsNode.File("os-release",
                "NAME=\"Ubuntu\"\nVERSION=\"22.04.3 LTS (Jammy Jellyfish)\"\nID=ubuntu\nVERSION_ID=\"22.04\"\n"),
            VfsNode.File("hostname", "ubuntu\n")),
        VfsNode.Dir("var",
            VfsNode.Dir("log",
                VfsNode.File("syslog", "May 25 10:00:00 ubuntu systemd[1]: Started Session.\n"))),
        VfsNode.Dir("home",
            VfsNode.Dir(login,
                VfsNode.Dir("Documents",
                    VfsNode.File("notes.txt", "Добро пожаловать в LinuxTrainer!\nПопробуй команды: ls, cd, cat, echo, history, man.\n")),
                VfsNode.Dir("Downloads"),
                VfsNode.File(".bashrc", "# default bashrc\nalias ll='ls -l'\n"),
                VfsNode.File("README.md", "# LinuxTrainer\nЭто демо-сессия Ubuntu 22.04.\n"))));

    private static void FixupParents(VfsNode node, VfsNode? parent)
    {
        node.Parent = parent;
        foreach (var c in node.Children) FixupParents(c, node);
    }

    private static readonly Dictionary<string, string> ManPages = new()
    {
        ["ls"] = "ls — вывод содержимого директории. Использование: ls [путь]",
        ["cd"] = "cd — смена текущей директории. Использование: cd [путь]. cd без аргументов — в домашнюю.",
        ["pwd"] = "pwd — печать абсолютного пути текущей директории.",
        ["cat"] = "cat — вывод содержимого файла. Использование: cat <файл>",
        ["echo"] = "echo — печать аргументов. Поддерживается echo текст > файл и echo текст >> файл.",
        ["mkdir"] = "mkdir — создание директории. Использование: mkdir <имя>",
        ["touch"] = "touch — создание пустого файла. Использование: touch <имя>",
        ["rm"] = "rm — удаление файла. С -r/-rf удаляет директории рекурсивно.",
        ["history"] = "history — список ранее введённых команд этой сессии.",
        ["clear"] = "clear — очистка экрана терминала.",
        ["whoami"] = "whoami — печать имени текущего пользователя.",
        ["uname"] = "uname — информация о системе. uname -a — расширенный вывод.",
        ["help"] = "help — список доступных в эмуляторе команд.",
        ["reset"] = "reset — кнопка «Сбросить» возвращает виртуальную ФС в исходное состояние.",
        ["man"] = "man — краткая справка по команде. Использование: man <команда>"
    };

    private static string Run(VfsNode root, ref string cwd, string raw, string login, string role)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var parts = SplitCommand(raw.Trim());
        var cmd = parts[0];
        var args = parts.Skip(1).ToList();

        return cmd switch
        {
            "help" => "Доступные команды: ls, cd, pwd, cat, echo, mkdir, touch, rm, history, clear, whoami, uname, man, help, reset",
            "pwd" => cwd,
            "whoami" => role == Roles.Admin ? "root" : login,
            "uname" => args.Contains("-a")
                ? "Linux ubuntu 5.15.0-generic #1 SMP Ubuntu 22.04.3 LTS x86_64 GNU/Linux"
                : "Linux",
            "ls" => Ls(root, cwd, args),
            "cd" => Cd(root, ref cwd, login, args),
            "cat" => Cat(root, cwd, args),
            "echo" => Echo(root, cwd, raw, args),
            "mkdir" => Mkdir(root, cwd, args),
            "touch" => Touch(root, cwd, args),
            "rm" => Rm(root, cwd, args),
            "history" => "",
            "clear" => "CLEAR",
            "man" => Man(args),
            _ => $"{cmd}: command not found"
        };
    }

    private static string Man(List<string> args)
    {
        if (args.Count == 0) return "What manual page do you want?\nFor example, try 'man man'.";
        var key = args[0];
        return ManPages.TryGetValue(key, out var page)
            ? page
            : $"No manual entry for {key}";
    }

    private static List<string> SplitCommand(string raw)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuote = false;
        char quote = '"';
        foreach (var ch in raw)
        {
            if (inQuote)
            {
                if (ch == quote) { inQuote = false; }
                else sb.Append(ch);
            }
            else if (ch == '"' || ch == '\'') { inQuote = true; quote = ch; }
            else if (char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0) { result.Add(sb.ToString()); sb.Clear(); }
            }
            else sb.Append(ch);
        }
        if (sb.Length > 0) result.Add(sb.ToString());
        return result;
    }

    private static VfsNode? Resolve(VfsNode root, string cwd, string path)
    {
        var full = NormalizePath(cwd, path);
        if (full == "/") return root;
        var node = root;
        foreach (var seg in full.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var next = node.Find(seg);
            if (next is null) return null;
            node = next;
        }
        return node;
    }

    private static string NormalizePath(string cwd, string path)
    {
        if (string.IsNullOrEmpty(path)) return cwd;
        if (path == "~") return cwd.StartsWith("/home/") ? cwd : "/home";
        if (path.StartsWith("~/")) path = "/home" + path[1..];

        var combined = path.StartsWith('/') ? path : (cwd.TrimEnd('/') + "/" + path);
        var parts = new List<string>();
        foreach (var seg in combined.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (seg == ".") continue;
            if (seg == "..")
            {
                if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
                continue;
            }
            parts.Add(seg);
        }
        return "/" + string.Join('/', parts);
    }

    private static string Ls(VfsNode root, string cwd, List<string> args)
    {
        var path = args.FirstOrDefault(a => !a.StartsWith('-')) ?? cwd;
        var node = Resolve(root, cwd, path);
        if (node is null) return $"ls: cannot access '{path}': No such file or directory";
        if (!node.IsDir) return node.Name;
        var entries = node.Children
            .OrderBy(c => !c.IsDir)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .Select(c => c.IsDir ? c.Name + "/" : c.Name);
        return string.Join("  ", entries);
    }

    private static string Cd(VfsNode root, ref string cwd, string login, List<string> args)
    {
        var target = args.FirstOrDefault() ?? $"/home/{login}";
        var full = NormalizePath(cwd, target);
        var node = Resolve(root, cwd, full);
        if (node is null) return $"cd: no such file or directory: {target}";
        if (!node.IsDir) return $"cd: not a directory: {target}";
        cwd = full == "" ? "/" : full;
        return "";
    }

    private static string Cat(VfsNode root, string cwd, List<string> args)
    {
        if (args.Count == 0) return "cat: missing operand";
        var node = Resolve(root, cwd, args[0]);
        if (node is null) return $"cat: {args[0]}: No such file or directory";
        if (node.IsDir) return $"cat: {args[0]}: Is a directory";
        return node.Content ?? "";
    }

    private static string Echo(VfsNode root, string cwd, string raw, List<string> args)
    {
        var redirIdx = args.FindIndex(a => a == ">" || a == ">>");
        if (redirIdx == -1) return string.Join(' ', args);

        var text = string.Join(' ', args.Take(redirIdx));
        if (redirIdx + 1 >= args.Count) return "echo: missing file operand";
        var target = args[redirIdx + 1];
        var append = args[redirIdx] == ">>";

        var dirPath = NormalizePath(cwd, ".");
        var dir = Resolve(root, cwd, dirPath);
        if (dir is null || !dir.IsDir) return $"echo: cannot write to {target}";

        var slash = target.LastIndexOf('/');
        if (slash >= 0)
        {
            var dirOnly = target[..slash];
            var fileOnly = target[(slash + 1)..];
            dir = Resolve(root, cwd, dirOnly);
            if (dir is null || !dir.IsDir) return $"echo: {target}: No such file or directory";
            target = fileOnly;
        }

        var existing = dir.Find(target);
        if (existing is null)
        {
            dir.Children.Add(new VfsNode { Name = target, IsDir = false, Content = text + "\n", Parent = dir });
        }
        else if (existing.IsDir)
        {
            return $"echo: {target}: Is a directory";
        }
        else
        {
            existing.Content = append ? (existing.Content ?? "") + text + "\n" : text + "\n";
        }
        return "";
    }

    private static string Mkdir(VfsNode root, string cwd, List<string> args)
    {
        if (args.Count == 0) return "mkdir: missing operand";
        var name = args[0];
        var slash = name.LastIndexOf('/');
        VfsNode? parent;
        string newName;
        if (slash >= 0)
        {
            parent = Resolve(root, cwd, name[..slash]);
            newName = name[(slash + 1)..];
        }
        else
        {
            parent = Resolve(root, cwd, ".");
            newName = name;
        }
        if (parent is null || !parent.IsDir) return $"mkdir: cannot create directory '{name}': No such file or directory";
        if (parent.Find(newName) is not null) return $"mkdir: cannot create directory '{name}': File exists";
        parent.Children.Add(new VfsNode { Name = newName, IsDir = true, Parent = parent });
        return "";
    }

    private static string Touch(VfsNode root, string cwd, List<string> args)
    {
        if (args.Count == 0) return "touch: missing file operand";
        var name = args[0];
        var dir = Resolve(root, cwd, ".");
        if (dir is null || !dir.IsDir) return "touch: failed";
        if (dir.Find(name) is null)
            dir.Children.Add(new VfsNode { Name = name, IsDir = false, Content = "", Parent = dir });
        return "";
    }

    private static string Rm(VfsNode root, string cwd, List<string> args)
    {
        var recursive = args.Any(a => a == "-r" || a == "-rf" || a == "-fr");
        var name = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (name is null) return "rm: missing operand";
        var node = Resolve(root, cwd, name);
        if (node is null) return $"rm: cannot remove '{name}': No such file or directory";
        if (node.IsDir && !recursive) return $"rm: cannot remove '{name}': Is a directory";
        node.Parent?.Children.Remove(node);
        return "";
    }
}
