using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace SchoolControl.Simulation;

/// <summary>
/// 20 ta o‘quvchi kompyuterini tarmoqda simulyatsiya qilish uchun test skripti.
/// Eslatma: Ushbu simulyatsiya laboratoriya va test muhiti uchun mo‘ljallangan.
/// </summary>
public static class SimulateClassroom
{
    private static readonly string ServerUrl = "http://localhost:5123";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("   SchoolControl — 20 ta kompyuter simulyatsiyasi");
        Console.WriteLine("=================================================");
        Console.WriteLine($"Server: {ServerUrl}");

        var students = new List<(string FirstName, string LastName, string PcName, string DeviceId)>
        {
            ("Alisher", "Navoiy", "PC-01", "11111111111111111111111111111101"),
            ("Zahiriddin", "Bobur", "PC-02", "11111111111111111111111111111102"),
            ("Mirzo", "Ulug‘bek", "PC-03", "11111111111111111111111111111103"),
            ("Abu Ali", "Ibn Sino", "PC-04", "11111111111111111111111111111104"),
            ("Abu Rayhon", "Beruniy", "PC-05", "11111111111111111111111111111105"),
            ("Muhammad", "Xorazmiy", "PC-06", "11111111111111111111111111111106"),
            ("Jaloliddin", "Manguberdi", "PC-07", "11111111111111111111111111111107"),
            ("Amir", "Temur", "PC-08", "11111111111111111111111111111108"),
            ("Nodira", "Begim", "PC-09", "11111111111111111111111111111109"),
            ("Zulfiya", "Isroilova", "PC-10", "11111111111111111111111111111110"),
            ("Abdulla", "Qodiriy", "PC-11", "11111111111111111111111111111111"),
            ("Abdulhamid", "Cho‘lpon", "PC-12", "11111111111111111111111111111112"),
            ("Usmon", "Nosir", "PC-13", "11111111111111111111111111111113"),
            ("G‘afur", "G‘ulom", "PC-14", "11111111111111111111111111111114"),
            ("Oybek", "Toshmuhammad", "PC-15", "11111111111111111111111111111115"),
            ("Erkin", "Vohidov", "PC-16", "11111111111111111111111111111116"),
            ("Abdulla", "Oripov", "PC-17", "11111111111111111111111111111117"),
            ("Muhammad", "Yusuf", "PC-18", "11111111111111111111111111111118"),
            ("O‘tkir", "Hoshimov", "PC-19", "11111111111111111111111111111119"),
            ("Tohir", "Malik", "PC-20", "11111111111111111111111111111120"),
        };

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        var tasks = new List<Task>();

        foreach (var s in students)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    // 1. API orqali ro'yxatdan o'tish
                    await http.PostAsJsonAsync($"{ServerUrl}/api/students/register", new
                    {
                        firstName = s.FirstName,
                        lastName = s.LastName,
                        deviceId = s.DeviceId,
                        deviceName = s.PcName
                    });

                    // 2. SignalR Hub ga ulanish
                    var hub = new HubConnectionBuilder()
                        .WithUrl($"{ServerUrl}/hubs/school")
                        .WithAutomaticReconnect()
                        .Build();

                    hub.On<string, string>("ReceiveCommand", (cmd, payload) =>
                    {
                        Console.WriteLine($"[{s.PcName} - {s.FirstName}] Buyruq qabul qilindi: {cmd}");
                    });

                    await hub.StartAsync();
                    await hub.SendAsync("RegisterStudent", s.DeviceId, s.PcName, s.FirstName, s.LastName);

                    Console.WriteLine($"[✓] {s.PcName} ({s.FirstName} {s.LastName}) serverga muvaffaqiyatli ulandi.");

                    // Heartbeat yuborib turish
                    while (true)
                    {
                        await Task.Delay(15000);
                        if (hub.State == HubConnectionState.Connected)
                        {
                            await hub.SendAsync("Heartbeat", s.DeviceId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[X] {s.PcName} xatolik: {ex.Message}");
                }
            }));
        }

        await Task.WhenAll(tasks);
    }
}
