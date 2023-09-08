using System;
using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Management;

namespace MutexKiller
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {
        private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_MEMORY_COUNTERS_EX
        {
            public uint cb;
            public uint PageFaultCount;
            public ulong PeakWorkingSetSize;
            public ulong WorkingSetSize;
            public ulong QuotaPeakPagedPoolUsage;
            public ulong QuotaPagedPoolUsage;
            public ulong QuotaPeakNonPagedPoolUsage;
            public ulong QuotaNonPagedPoolUsage;
            public ulong PagefileUsage;
            public ulong PeakPagefileUsage;
            public ulong PrivateUsage;
        }

        [DllImport("kernel32.dll")]
        public static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS_EX counters, uint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [Flags]
        public enum ProcessAccessFlags : uint
        {
            QueryInformation = 0x0400
        }


        public MainWindow()
        {
            InitializeComponent();
        }

        private void ExitBut_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }

        private void MinimizeBut_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Toolbar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void Start()
        {
            ProcessListBox.Items.Clear();
            Process[] processes = Process.GetProcesses();
            Array.Sort(processes, (x, y) => string.Compare(x.ProcessName, y.ProcessName));
            foreach (Process process in processes)
            {
                ProcessListBox.Items.Add(process.ProcessName);
            }
        }
        private void StartBut_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Start();
        }
        private void AddBut_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var addEntryWindow = new StartProccessWindow();
            if (addEntryWindow.ShowDialog() == true)
            {
                try
                {
                    string enteredData = addEntryWindow.EnteredData;
                    Process.Start(enteredData);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка: " + ex.Message);
                }

            }
        }

        private void ProcessListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ModulsProcessListBox.Items.Clear();
            ThreadsProcessListBox.Items.Clear();
            InfoBox.Text ="";
            ProcessModuleCollection modules = null;
            Process selectedProcess = null;
            ProcessThreadCollection threads = null;

            // Получение выбранного процесса
            if (ProcessListBox.SelectedItem != null)
            {
                try
                {
                    string selectedProcessName = ProcessListBox.SelectedItem.ToString();
                    selectedProcess = Process.GetProcessesByName(selectedProcessName)[0];
                    Process[] processes = Process.GetProcessesByName(selectedProcessName);
                    Process process = processes[0];
                    try
                    {
                        ProcessInfo();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }

                    try
                    {
                        threads = selectedProcess.Threads;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Получение потоков: " + ex.Message);
                    }

                    try
                    {
                        modules = selectedProcess.Modules;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Получение Модулей: " + ex.Message);
                    }

                    try
                    {
                        PROCESS_MEMORY_COUNTERS_EX counters;
                        IntPtr processHandle = OpenProcess(ProcessAccessFlags.QueryInformation, false, (uint)process.Id);
                        GetProcessMemoryInfo(processHandle, out counters, (uint)Marshal.SizeOf<PROCESS_MEMORY_COUNTERS_EX>());
                        ulong workingSet = counters.WorkingSetSize;
                        CloseHandle(processHandle);
                    }
                    catch (Exception ex)
                    {
                        EfficiencyBox.Text = "Не удалось получить информацию" + ex.Message;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

            }

            // Добавление имен подпроцессов в ListBox для подпроцессов
            if (modules != null)
            {
                foreach (ProcessModule module in modules)
                {
                    ModulsProcessListBox.Items.Add(module.ModuleName);
                }
            }
            if (threads != null)
            {
                foreach (ProcessThread thread in threads)
                {
                    ThreadsProcessListBox.Items.Add("Thread ID: " + thread.Id);
                }
            }
        }

        private void ProcessInfo()
        {
            Process[] processes = Process.GetProcesses();
            InfoBox.AppendText("\nЗагрузка процессора:\n");
            PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            float cpuUsage = cpuCounter.NextValue();
            InfoBox.AppendText($"Использование процессора: {cpuUsage}%\n");

            InfoBox.AppendText("\nИнформация о системе:\n");
            var query = new ObjectQuery("SELECT * FROM Win32_OperatingSystem");
            var searcher = new ManagementObjectSearcher(query);
            foreach (ManagementObject os in searcher.Get())
            {
                InfoBox.AppendText($"Операционная система: {os["Caption"]}\n");
                InfoBox.AppendText($"Версия операционной системы: {os["Version"]}\n");
                InfoBox.AppendText($"Архитектура процессора: {os["OSArchitecture"]}\n");
                InfoBox.AppendText($"Общая физическая память: {os["TotalVisibleMemorySize"]} КБ\n");
            }

            InfoBox.AppendText("\nСобытия из журнала приложений (первые 5):\n");
            EventLog eventLog = new EventLog("Application");
            int count = 0;  
            foreach (EventLogEntry entry in eventLog.Entries)
            {
                InfoBox.AppendText($"Событие: {entry.EntryType}, Время: {entry.TimeGenerated}, Сообщение: {entry.Message}\n");
                count++;
                if (count >= 5) break;
            }
        }




        private void KillItem_Click(object sender, RoutedEventArgs e)
        {
            string selectedProcessName = ProcessListBox.SelectedItem.ToString();
            Process selectedProcess = Process.GetProcessesByName(selectedProcessName)[0];

            // Попытка остановить выбранный процесс
            try
            {
                selectedProcess.Kill();
                MessageBox.Show("Процесс с именем " + selectedProcessName + " остановлен.");
                Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при остановке процесса: " + ex.Message);
            }
        }
        private async void IsolationItem_Click(object sender, RoutedEventArgs e)
        {
            string selectedProcessName = ProcessListBox.SelectedItem.ToString();
            Process selectedProcess = Process.GetProcessesByName(selectedProcessName)[0];
               
            //try
            //{
                IsolationProccessListBox.Items.Add(selectedProcessName);
                await Task.Run(() =>
                {
                    while (!cancellationTokenSource.Token.IsCancellationRequested)
                    {

                        Process[] processes = Process.GetProcessesByName(selectedProcessName);
                        if (processes.Length > 0)
                        {

                            foreach (Process process in processes)
                            {
                                try
                                {
                                    process.Kill();
                                    Console.WriteLine($"Процесс {selectedProcessName} был завершен.");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Не удалось завершить процесс {selectedProcessName}: {ex.Message}");
                                }
                            }
                        }

                        // Задержка
                        System.Threading.Thread.Sleep(1000);
                    }
                }, cancellationTokenSource.Token);

                Start();
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(ex.Message);
            //}
        }
        private void DeleteZone_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                cancellationTokenSource.Cancel();
                IsolationProccessListBox.Items.Remove(IsolationProccessListBox.SelectedItem);
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            
        }
    }
}
