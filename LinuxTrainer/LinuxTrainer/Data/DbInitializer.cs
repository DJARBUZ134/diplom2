using System.Text.Json;
using LinuxTrainer.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace LinuxTrainer.Data;

public static class DbInitializer
{
    public static void Seed(AppDbContext db)
    {
        db.Database.EnsureCreated();

        var hasher = new PasswordHasher<User>();

        if (!db.Users.Any())
        {
            var admin = new User { Login = "admin", Role = Roles.Admin, DisplayName = "Администратор" };
            admin.PasswordHash = hasher.HashPassword(admin, "admin123");

            var teacher = new User { Login = "teacher", Role = Roles.Teacher, DisplayName = "Преподаватель" };
            teacher.PasswordHash = hasher.HashPassword(teacher, "teacher123");

            var student = new User { Login = "student", Role = Roles.Student, DisplayName = "Студент" };
            student.PasswordHash = hasher.HashPassword(student, "student123");

            db.Users.AddRange(admin, teacher, student);
            db.SaveChanges();
        }

        if (!db.Quests.Any())
        {
            var ipFs = JsonSerializer.Serialize(VfsNode.Dir("",
                VfsNode.Dir("etc",
                    VfsNode.File("hostname", "ubuntu\n"),
                    VfsNode.Dir("network",
                        VfsNode.File("interfaces",
                            "auto lo\niface lo inet loopback\n\nauto eth0\niface eth0 inet static\n  address 192.168.1.42\n  netmask 255.255.255.0\n  gateway 192.168.1.1\n"))),
                VfsNode.Dir("home",
                    VfsNode.Dir("student",
                        VfsNode.File("hint.txt",
                            "Подсказка: посмотри файл /etc/network/interfaces (cat /etc/network/interfaces).\n")))));

            var permsFs = JsonSerializer.Serialize(VfsNode.Dir("",
                VfsNode.Dir("home",
                    VfsNode.Dir("student",
                        VfsNode.File("secret.txt", "rwxr-xr--\n")))));

            db.Quests.AddRange(
                new Quest
                {
                    Name = "Основы Linux",
                    Description = "Познакомьтесь с командной строкой Ubuntu и узнайте, в какой папке вы находитесь.",
                    Difficulty = "Легкая",
                    Order = 1,
                    Question = "Какой командой можно вывести текущую директорию?",
                    ExpectedAnswer = "pwd"
                },
                new Quest
                {
                    Name = "Работа с файлами",
                    Description = "Создайте файл командой touch и выведите его содержимое командой cat.",
                    Difficulty = "Легкая",
                    Order = 2,
                    Question = "Какой командой создаётся пустой файл?",
                    ExpectedAnswer = "touch"
                },
                new Quest
                {
                    Name = "Права доступа",
                    Description = "Изучите вывод прав файла в формате rwxr-xr-- и определите числовое представление.",
                    Difficulty = "Средняя",
                    Order = 3,
                    Question = "Числовое представление прав rwxr-xr-- (3 цифры без пробелов).",
                    ExpectedAnswer = "754",
                    InitialFileSystemJson = permsFs
                },
                new Quest
                {
                    Name = "Настройка сети",
                    Description = "Откройте файл /etc/network/interfaces и найдите IP-адрес устройства.",
                    Difficulty = "Средняя",
                    Order = 4,
                    Question = "Какой IPv4-адрес устройства настроен в /etc/network/interfaces?",
                    ExpectedAnswer = "192.168.1.42",
                    InitialFileSystemJson = ipFs
                },
                new Quest
                {
                    Name = "Bash скрипты",
                    Description = "Узнайте о shebang — первой строке bash-скрипта.",
                    Difficulty = "Сложная",
                    Order = 5,
                    Question = "Стандартный shebang для bash-скрипта (с символом #).",
                    ExpectedAnswer = "#!/bin/bash"
                },
                new Quest
                {
                    Name = "Администрирование",
                    Description = "Под кем работают системные сервисы по умолчанию?",
                    Difficulty = "Сложная",
                    Order = 6,
                    Question = "Имя суперпользователя в Linux.",
                    ExpectedAnswer = "root"
                }
            );
            db.SaveChanges();
        }

        if (!db.Progress.Any())
        {
            var student = db.Users.First(u => u.Login == "student");
            var quests = db.Quests.OrderBy(q => q.Order).ToList();

            db.Progress.Add(new UserQuestProgress
            {
                UserId = student.Id,
                QuestId = quests[0].Id,
                Status = QuestStatus.NotStarted
            });
            db.SaveChanges();
        }
    }
}
