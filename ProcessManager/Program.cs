using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace ProcessManager
{
    /// <summary>
    /// Reprezintă informații despre un proces pentru afișare
    /// </summary>
    public class ProcessInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string FullPath { get; set; }
        public long MemoryUsage { get; set; } // în KB
        public TimeSpan CpuTime { get; set; }
        public string StartTime { get; set; }
        public string Priority { get; set; }
        public int ThreadCount { get; set; }
    }

    /// <summary>
    /// Aplicație consolă pentru gestionarea proceselor locale (similar Task Manager)
    /// </summary>
    class Program
    {
        private static bool _isRunning = true;
        private static int _refreshInterval = 5000; // ms
        private static SortColumn _currentSort = SortColumn.Name;
        private static bool _sortAscending = true;

        enum SortColumn
        {
            Name,
            Id,
            Memory,
            CpuTime,
            StartTime,
            Priority
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            SetCurrentCulture();

            Console.WriteLine("===========================================");
            Console.WriteLine("   MANAGER DE PROCESE - .NET Framework 4.7.2");
            Console.WriteLine("===========================================");
            Console.WriteLine();

            ShowHelp();

            // Thread pentru refresh automat
            Thread refreshThread = new Thread(RefreshLoop)
            {
                IsBackground = true
            };
            refreshThread.Start();

            // Bucla principală de comenzi
            while (_isRunning)
            {
                Console.Write("\nComandă: ");
                string input = Console.ReadLine()?.Trim().ToLower();

                if (string.IsNullOrEmpty(input))
                    continue;

                ProcessCommand(input);
            }

            Console.WriteLine("\nAplicația a fost închisă. La revedere!");
        }

        static void SetCurrentCulture()
        {
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("ro-RO");
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("ro-RO");
            }
            catch
            {
                // Folosește cultura implicită dacă ro-RO nu este disponibilă
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("COMENZI DISPONIBILE:");
            Console.WriteLine("  list / ls              - Listează toate procesele");
            Console.WriteLine("  detail <id>            - Afișează detalii despre un proces");
            Console.WriteLine("  kill <id>              - Termină un proces");
            Console.WriteLine("  search <nume>          - Caută procese după nume");
            Console.WriteLine("  top [n]                - Afișează primele n procese după memorie");
            Console.WriteLine("  sort <criteriu>        - Schimbă criteriu de sortare (name,id,memory,cpu,priority,start)");
            Console.WriteLine("  refresh <ms>           - Setează intervalul de refresh (implicit 5000ms)");
            Console.WriteLine("  auto on/off            - Activează/dezactivează refresh automat");
            Console.WriteLine("  help / h               - Afișează acest ajutor");
            Console.WriteLine("  exit / quit / q        - Ieși din aplicație");
            Console.WriteLine();
        }

        static void ProcessCommand(string command)
        {
            string[] parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0];

            switch (cmd)
            {
                case "list":
                case "ls":
                    ListProcesses();
                    break;

                case "detail":
                    if (parts.Length > 1 && int.TryParse(parts[1], out int id))
                        ShowDetail(id);
                    else
                        Console.WriteLine("Utilizare: detail <id_proces>");
                    break;

                case "kill":
                    if (parts.Length > 1 && int.TryParse(parts[1], out int killId))
                        KillProcess(killId);
                    else
                        Console.WriteLine("Utilizare: kill <id_proces>");
                    break;

                case "search":
                    if (parts.Length > 1)
                        SearchProcesses(string.Join(" ", parts.Skip(1)));
                    else
                        Console.WriteLine("Utilizare: search <nume_proces>");
                    break;

                case "top":
                    int count = parts.Length > 1 && int.TryParse(parts[1], out count) ? count : 10;
                    ShowTop(count);
                    break;

                case "sort":
                    if (parts.Length > 1)
                        SetSortCriteria(parts[1]);
                    else
                        Console.WriteLine("Utilizare: sort <name|id|memory|cpu|priority|start>");
                    break;

                case "refresh":
                    if (parts.Length > 1 && int.TryParse(parts[1], out int interval))
                    {
                        _refreshInterval = Math.Max(1000, interval);
                        Console.WriteLine($"Interval refresh setat la {_refreshInterval}ms");
                    }
                    else
                        Console.WriteLine("Utilizare: refresh <interval_ms>");
                    break;

                case "auto":
                    if (parts.Length > 1)
                    {
                        string state = parts[1].ToLower();
                        if (state == "on")
                            Console.WriteLine("Refresh automat activat (pornit)");
                        else if (state == "off")
                            Console.WriteLine("Refresh automat dezactivat (oprit)");
                        else
                            Console.WriteLine("Utilizare: auto on/off");
                    }
                    break;

                case "help":
                case "h":
                    ShowHelp();
                    break;

                case "exit":
                case "quit":
                case "q":
                    _isRunning = false;
                    break;

                default:
                    Console.WriteLine($"Comandă necunoscută: {cmd}. Scrie 'help' pentru lista de comenzi.");
                    break;
            }
        }

        static List<ProcessInfo> GetProcessList()
        {
            var processes = Process.GetProcesses();
            var processList = new List<ProcessInfo>();

            foreach (var proc in processes)
            {
                try
                {
                    var info = new ProcessInfo
                    {
                        Id = proc.Id,
                        Name = proc.ProcessName,
                        ThreadCount = proc.Threads.Count
                    };

                    // Încearcă să obțină path-ul complet
                    try
                    {
                        info.FullPath = proc.MainModule?.FileName ?? "N/A";
                    }
                    catch
                    {
                        info.FullPath = "N/A (acces restricționat)";
                    }

                    // Memorie utilizată
                    try
                    {
                        info.MemoryUsage = proc.WorkingSet64 / 1024; // KB
                    }
                    catch
                    {
                        info.MemoryUsage = 0;
                    }

                    // Timp CPU
                    try
                    {
                        info.CpuTime = proc.TotalProcessorTime;
                    }
                    catch
                    {
                        info.CpuTime = TimeSpan.Zero;
                    }

                    // Timp pornire
                    try
                    {
                        info.StartTime = proc.StartTime.ToString("dd.MM.yyyy HH:mm:ss");
                    }
                    catch
                    {
                        info.StartTime = "N/A";
                    }

                    // Prioritate
                    try
                    {
                        info.Priority = proc.PriorityClass.ToString();
                    }
                    catch
                    {
                        info.Priority = "N/A";
                    }

                    processList.Add(info);
                }
                catch
                {
                    // Ignoră procesele care nu pot fi accesate
                }
                finally
                {
                    proc.Dispose();
                }
            }

            // Sortare
            return SortProcessList(processList);
        }

        static List<ProcessInfo> SortProcessList(List<ProcessInfo> list)
        {
            IOrderedEnumerable<ProcessInfo> sorted;

            switch (_currentSort)
            {
                case SortColumn.Id:
                    sorted = _sortAscending ? list.OrderBy(p => p.Id) : list.OrderByDescending(p => p.Id);
                    break;
                case SortColumn.Memory:
                    sorted = _sortAscending ? list.OrderBy(p => p.MemoryUsage) : list.OrderByDescending(p => p.MemoryUsage);
                    break;
                case SortColumn.CpuTime:
                    sorted = _sortAscending ? list.OrderBy(p => p.CpuTime) : list.OrderByDescending(p => p.CpuTime);
                    break;
                case SortColumn.StartTime:
                    sorted = _sortAscending ? list.OrderBy(p => p.StartTime) : list.OrderByDescending(p => p.StartTime);
                    break;
                case SortColumn.Priority:
                    sorted = _sortAscending ? list.OrderBy(p => p.Priority) : list.OrderByDescending(p => p.Priority);
                    break;
                case SortColumn.Name:
                default:
                    sorted = _sortAscending ? list.OrderBy(p => p.Name) : list.OrderByDescending(p => p.Name);
                    break;
            }

            return sorted.ToList();
        }

        static void ListProcesses()
        {
            Console.WriteLine();
            Console.WriteLine("Se încarcă lista de procese...");

            var processes = GetProcessList();

            Console.WriteLine();
            Console.WriteLine($"Total procese: {processes.Count}");
            Console.WriteLine($"Sortare: {_currentSort} ({(_sortAscending ? "asc" : "desc")})");
            Console.WriteLine();

            PrintProcessTable(processes.Take(50).ToList());

            if (processes.Count > 50)
            {
                Console.WriteLine($"\n... și încă {processes.Count - 50} procese. Folosește 'search' pentru a găsi un proces anume.");
            }
        }

        static void PrintProcessTable(List<ProcessInfo> processes)
        {
            if (processes.Count == 0)
            {
                Console.WriteLine("Nu există procese de afișat.");
                return;
            }

            // Formatare tabel
            int colIdWidth = 8;
            int colNameWidth = 25;
            int colMemWidth = 12;
            int colCpuWidth = 12;
            int colPriorityWidth = 15;

            Console.WriteLine(new string('-', colIdWidth + colNameWidth + colMemWidth + colCpuWidth + colPriorityWidth + 5));
            Console.WriteLine($"{ "ID".PadRight(colIdWidth)} {"Nume".PadRight(colNameWidth)} {"Memorie(KB)".PadRight(colMemWidth)} {"Timp CPU".PadRight(colCpuWidth)} {"Prioritate".PadRight(colPriorityWidth)}");
            Console.WriteLine(new string('-', colIdWidth + colNameWidth + colMemWidth + colCpuWidth + colPriorityWidth + 5));

            foreach (var p in processes)
            {
                string name = p.Name.Length > colNameWidth - 1 ? p.Name.Substring(0, colNameWidth - 1) + "…" : p.Name;
                Console.WriteLine(
                    $"{p.Id.ToString().PadRight(colIdWidth)}" +
                    $"{name.PadRight(colNameWidth)}" +
                    $"{p.MemoryUsage.ToString("N0").PadRight(colMemWidth)}" +
                    $"{FormatTimeSpan(p.CpuTime).PadRight(colCpuWidth)}" +
                    $"{p.Priority.PadRight(colPriorityWidth)}");
            }

            Console.WriteLine(new string('-', colIdWidth + colNameWidth + colMemWidth + colCpuWidth + colPriorityWidth + 5));
        }

        static string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            else if (ts.TotalMinutes >= 1)
                return $"{ts.Minutes}:{ts.Seconds:D2}.{ts.Milliseconds / 100}";
            else
                return $"{ts.Seconds}.{ts.Milliseconds / 100}s";
        }

        static void ShowDetail(int processId)
        {
            try
            {
                var proc = Process.GetProcessById(processId);
                
                Console.WriteLine();
                Console.WriteLine("===========================================");
                Console.WriteLine($"DETALII PROCES ID: {processId}");
                Console.WriteLine("===========================================");
                
                try { Console.WriteLine($"Nume: {proc.ProcessName}"); } catch { }
                try { Console.WriteLine($"Path complet: {proc.MainModule?.FileName ?? "N/A"}"); } catch { }
                try { Console.WriteLine($"Descriere: {proc.MainModule?.FileVersionInfo.FileDescription ?? "N/A"}"); } catch { }
                try { Console.WriteLine($"ID Proces: {proc.Id}"); } catch { }
                try { Console.WriteLine($"ID Sesiu: {proc.SessionId}"); } catch { }
                try { Console.WriteLine($"Nume mașină: {proc.MachineName}"); } catch { }
                try { Console.WriteLine($"Fire (threads): {proc.Threads.Count}"); } catch { }
                try { Console.WriteLine($"Handle-uri: {proc.HandleCount}"); } catch { }
                try { Console.WriteLine($"Memorie (Working Set): {proc.WorkingSet64 / 1024:N0} KB"); } catch { }
                try { Console.WriteLine($"Memorie privată: {proc.PrivateMemorySize64 / 1024:N0} KB"); } catch { }
                try { Console.WriteLine($"Memorie virtuală: {proc.VirtualMemorySize64 / 1024:N0} KB"); } catch { }
                try { Console.WriteLine($"Timp CPU total: {FormatTimeSpan(proc.TotalProcessorTime)}"); } catch { }
                try { Console.WriteLine($"Timp CPU user: {FormatTimeSpan(proc.UserProcessorTime)}"); } catch { }
                try { Console.WriteLine($"Timp CPU kernel: {FormatTimeSpan(proc.PrivilegedProcessorTime)}"); } catch { }
                try { Console.WriteLine($"Pornit la: {proc.StartTime.ToString("dd.MM.yyyy HH:mm:ss")}"); } catch { }
                try { Console.WriteLine($"Timp rulare: {DateTime.Now - proc.StartTime}"); } catch { }
                try { Console.WriteLine($"Prioritate: {proc.PriorityClass}"); } catch { }
                try { Console.WriteLine($"Prioritate thread: {proc.BasePriority}"); } catch { }
                try { Console.WriteLine($"Răspunde: {proc.Responding}"); } catch { }
                try { Console.WriteLine($"Are UI: {proc.HasExited == false && !string.IsNullOrEmpty(proc.MainWindowTitle)}"); } catch { }
                try { Console.WriteLine($"Titlu fereastră: {(proc.MainWindowTitle?.Length > 0 ? proc.MainWindowTitle : "N/A")}"); } catch { }
                
                Console.WriteLine("===========================================");
                
                proc.Dispose();
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"Procesul cu ID {processId} nu a fost găsit.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la obținerea detaliilor: {ex.Message}");
            }
        }

        static void KillProcess(int processId)
        {
            try
            {
                var proc = Process.GetProcessById(processId);
                string processName = proc.ProcessName;
                
                Console.Write($"Sigur doriți să terminați procesul '{processName}' (ID: {processId})? (y/n): ");
                var response = Console.ReadLine()?.Trim().ToLower();
                
                if (response == "y" || response == "da")
                {
                    proc.Kill();
                    proc.WaitForExit(5000);
                    
                    if (proc.HasExited)
                        Console.WriteLine($"Procesul '{processName}' a fost terminat cu succes.");
                    else
                        Console.WriteLine($"Procesul nu a putut fi terminat.");
                }
                else
                {
                    Console.WriteLine("Operația a fost anulată.");
                }
                
                proc.Dispose();
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"Procesul cu ID {processId} nu a fost găsit.");
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("Procesul a fost deja terminat.");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Console.WriteLine($"Acces refuzat: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la terminarea procesului: {ex.Message}");
            }
        }

        static void SearchProcesses(string searchTerm)
        {
            var allProcesses = GetProcessList();
            var filtered = allProcesses
                .Where(p => p.Name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                           p.FullPath.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            Console.WriteLine();
            Console.WriteLine($"Rezultate căutare pentru '{searchTerm}': {filtered.Count} procese găsite");
            Console.WriteLine();

            if (filtered.Count > 0)
            {
                PrintProcessTable(filtered);
            }
            else
            {
                Console.WriteLine("Nu au fost găsite procese care să corespundă criteriului.");
            }
        }

        static void ShowTop(int count)
        {
            var allProcesses = GetProcessList();
            var top = allProcesses
                .OrderByDescending(p => p.MemoryUsage)
                .Take(count)
                .ToList();

            Console.WriteLine();
            Console.WriteLine($"Top {count} procese după consumul de memorie:");
            Console.WriteLine();

            PrintProcessTable(top);
        }

        static void SetSortCriteria(string criteria)
        {
            switch (criteria.ToLower())
            {
                case "name":
                    _currentSort = SortColumn.Name;
                    break;
                case "id":
                    _currentSort = SortColumn.Id;
                    break;
                case "memory":
                case "mem":
                    _currentSort = SortColumn.Memory;
                    break;
                case "cpu":
                    _currentSort = SortColumn.CpuTime;
                    break;
                case "priority":
                case "prio":
                    _currentSort = SortColumn.Priority;
                    break;
                case "start":
                    _currentSort = SortColumn.StartTime;
                    break;
                default:
                    Console.WriteLine("Criteriu invalid. Opțiuni: name, id, memory, cpu, priority, start");
                    return;
            }

            // Inversează ordinea dacă se setează același criteriu
            Console.WriteLine($"Sortare setată la: {_currentSort} ({(_sortAscending ? "asc" : "desc")})");
        }

        static void RefreshLoop()
        {
            bool autoRefresh = false;

            while (_isRunning)
            {
                Thread.Sleep(_refreshInterval);

                if (autoRefresh)
                {
                    Console.Clear();
                    Console.WriteLine("=== REFRESH AUTOMAT ===");
                    ListProcesses();
                }
            }
        }
    }
}
