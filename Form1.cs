using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WinSparkleDotNet;
using HardwareHelperLib;
using WiseFlasher.Properties;
using System.Security.Policy;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Security.Cryptography;
using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using WiseFlasher;
namespace WiseMSP
{

    public partial class Form1 : Form
    {
           
        private const string Format = "X2";
        public static string datePatt = @"d/M/yyyy hh:mm:ss tt";
        private string path;
        private string log;
        private string firmwarePath;
        public string jtag_name = "";
        public string jtag_serial = "";
        public string jtag_port = "";
        private TypeProgrammer jtag_type = TypeProgrammer.NONE;
        private int DeviceSelectedIndex = -1;
        private List<Device> Devices;
        bool advanced = false;
        enum TypeDevice { UNK, Wpan, TE , MatrizBornera, Wisevalve, WTDrop, C1, M1, ADP_GW, C1_EXP, ADP_GPS, V1, C1_LORA };
        TypeDevice device;
        public enum TypeProgrammer { NONE, OLIMEX, TEXAS, STLINK, JLINK };
        HH_Lib hwh = new HH_Lib();
        bool ui_state = true;


        public struct XbeeInfoConfig
        {
            public uint code;                // 4 bytes
            public byte gprs;                // 1 byte
            public byte router;              // 1 byte
            public byte lone_coord;          // 1 byte
            public byte identifier;          // 1 byte
            public ushort mod_Config;       // 2 bytes
            public byte Channel;             // 1 byte
            public byte MeshNetworkRetries;  // 1 byte
            public ulong ChannelMask;        // 8 bytes
            public byte NetworkHops;         // 1 byte
            public byte NetworkDelaySlots;   // 1 byte
            public byte UnicastMacRetries;   // 1 byte
            public uint SleepTime;           // 4 bytes
            public uint WakeTime;            // 4 bytes
            public byte SleepOptions;        // 1 byte 
            public byte SleepMode;           // 1 byte
            public byte PowerLevel;          // 1 byte 
            public byte coordinator;         // 1 byte
            public byte SoloGW;              // 1 byte
            public byte PreambleID;          // 1 byte
            public byte SecurityEnable;      // 1 byte
        }


        public static XbeeInfoConfig ParseXbeeInfoConfig(byte[] data)
        {
            XbeeInfoConfig config = new XbeeInfoConfig();
            config.code = BitConverter.ToUInt32(data, 0);
            config.gprs = data[4];
            config.router = data[5];
            config.lone_coord = data[6];
            config.identifier = data[7];
            config.mod_Config = BitConverter.ToUInt16(data, 8);
            config.Channel = data[24];
            config.MeshNetworkRetries = data[25];
            config.ChannelMask = BitConverter.ToUInt64(data, 26);
            config.NetworkHops = data[34];
            config.NetworkDelaySlots = data[35];
            config.UnicastMacRetries = data[36];
            config.SleepTime = BitConverter.ToUInt32(data, 40);
            config.WakeTime = BitConverter.ToUInt32(data, 44);
            config.SleepOptions = data[48];
            config.SleepMode = data[49];
            config.PowerLevel = data[50];
            config.coordinator = data[51];
            config.SoloGW = data[52];
            config.PreambleID = data[53];
            config.SecurityEnable = data[54];
            return config;
        }


        public static byte[] SerializeXbeeInfoConfig(XbeeInfoConfig config)
        {
            byte[] data = new byte[64];
            BitConverter.GetBytes(config.code).CopyTo(data, 0);
            data[4] = config.gprs;
            data[5] = config.router;
            data[6] = config.lone_coord;
            data[7] = config.identifier;
            BitConverter.GetBytes(config.mod_Config).CopyTo(data, 8);
            data[24] = config.Channel;
            data[25] = config.MeshNetworkRetries;
            BitConverter.GetBytes(config.ChannelMask).CopyTo(data, 26);
            data[34] = config.NetworkHops;
            data[35] = config.NetworkDelaySlots;
            data[36] = config.UnicastMacRetries;
            BitConverter.GetBytes(config.SleepTime).CopyTo(data, 40);
            BitConverter.GetBytes(config.WakeTime).CopyTo(data, 44);
            data[48] = config.SleepOptions;
            data[49] = config.SleepMode;
            data[50] = config.PowerLevel;
            data[51] = config.coordinator;
            data[52] = config.SoloGW;
            data[53] = config.PreambleID;
            data[54] = config.SecurityEnable;
            return data;
        }


        public class Device
        {
            public string name { get; set; }
            public string program_start { get; set; }
            public string program_end { get; set; }
            public string data_start { get; set; }
            public string data_end { get; set; }
            public string type { get; set; }
            public string boot_version { get; set; }

        }

        static private bool checkCallback()
        {
            return CheckUpdateEnable;
        }

        static private void ShutDownRequestCallback()
        {
            try
            {
                if (System.Windows.Forms.Application.MessageLoop)
                {
                    System.Windows.Forms.Application.Exit();
                }
                else
                {
                    System.Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        static bool CheckUpdateEnable = true;

        static private void didnotFindCallback()
        {
            CheckUpdateEnable = true;
        }

        static private void didFindCallback()
        {
            CheckUpdateEnable = true;
        }

        static private void errorCallback()
        {
            CheckUpdateEnable = true;
        }

        WinSparkle.CanShutdownCallback can_shutdown = new WinSparkle.CanShutdownCallback(checkCallback);
        WinSparkle.ShutdownRequestCallback shutdown = new WinSparkle.ShutdownRequestCallback(ShutDownRequestCallback);
        WinSparkle.DidNotFindUpdateCallback didnotFind = new WinSparkle.DidNotFindUpdateCallback(didnotFindCallback);
        WinSparkle.DidFindUpdateCallback didFind = new WinSparkle.DidFindUpdateCallback(didFindCallback);
        WinSparkle.ErrorCallback error = new WinSparkle.ErrorCallback(errorCallback);

        private readonly WinSparkleNet _sparkleNet;
        public string version;

        public Form1()
        {
            this.StartPosition = FormStartPosition.CenterScreen;
            Devices = new List<Device>();
            InitializeComponent();

            panel5.Enabled = false;
            panel6.Enabled = false;
            checkHardReset.Checked = true;
            checkRun.Checked = true;
            checkEraseProgram.Checked = true;
            checkVerifyProgram.Checked = true;
            checkWriteProgram.Checked = true;
            checkReadProgram.Checked = true;
            checkReadData.Checked = true;
            pictureBox2.Visible = false;
            pictureBox3.Visible = false;
            pictureBox4.Visible = false;
            comboBox2.Enabled = false;
            //comboBox2.Items.Add("OLIMEX");
            //comboBox2.Items.Add("TEXAS INSTRUMENTS");
            jtag_type = TypeProgrammer.NONE;
            firmwarePath = "";
            log = "";

            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            version = fvi.FileVersion;
            label12.Text = "version " + version;
            path = Application.StartupPath;
            Console.WriteLine(path);
            try
            {
                System.IO.StreamReader myFile = new System.IO.StreamReader(path + /*"\\Olimex" +*/ "\\devices.ini");
                string myString;

                while ((myString = myFile.ReadLine()) != null)
                {
                    if (myString.StartsWith(":"))
                    {
                        string temp = myString.Substring(1);
                        char[] delimiters = new char[] { '\t' };
                        string[] words = temp.Split(delimiters);
                        if (words.Length == 7)
                        {
                            if (!comboBox1.Items.Contains(words[0]))
                            {
                                comboBox1.Items.Add(words[0]);
                            }
                            Device tempDevice = new Device() { name = words[0], type = words[1], boot_version = words[2], program_start = words[3], program_end = words[4], data_start = words[5], data_end = words[6] };
                            if (tempDevice != null)
                                Devices.Add(tempDevice);

                            Console.WriteLine(words[0] + " " + words[1] + " " + words[2] + " " + words[3] + " " + words[4] + " " + words[5] + " " + words[6]);
                        }
                        else
                        {
                            MessageBox.Show("devices.ini incorrectly formatted, please verify and restart the application.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
                myFile.Close();
                label11.Text = "Status: Ready.";
                label11.Refresh();
            }
            catch (Exception FileLoadException)
            {
                MessageBox.Show("File devices.ini not found. Please verify the file is in the application directory and restart the application.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                label11.Text = "Status: Error ocurred!";
                label11.Refresh();
            }

            if (System.IO.File.Exists(path + "\\memoryTI_INFO.txt"))
            {
                try
                {
                    System.IO.File.Delete(path + "\\memoryTI_INFO.txt");
                }
                catch (System.IO.IOException e)
                {
                    MessageBox.Show(e.Message);
                    return;
                }
            }
            if (System.IO.File.Exists(path + "\\memoryTI_MAIN.txt"))
            {
                try
                {
                    System.IO.File.Delete(path + "\\memoryTI_MAIN.txt");
                }
                catch (System.IO.IOException e)
                {
                    MessageBox.Show(e.Message);
                    return;
                }
            }
            if (System.IO.File.Exists(path + "\\memory.txt"))
            {
                try
                {
                    System.IO.File.Delete(path + "\\memory.txt");
                }
                catch (System.IO.IOException e)
                {
                    MessageBox.Show(e.Message);
                    return;
                }
            }

            try
            {
                _sparkleNet = new WinSparkleNet();
            }
            catch (Exception ex)
            {
                MessageBox.Show(@"Error was : " + ex, @"Error during SparkleNet instantiation",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            _sparkleNet.SetCanShutdownCallback(can_shutdown);
            _sparkleNet.SetShutdownRequestCallback(shutdown);
            _sparkleNet.SetDidNotFindUpdateCallback(didnotFind);
            _sparkleNet.SetDidFindUpdateCallback(didFind);
            _sparkleNet.SetErrorCallback(error);
            _sparkleNet.AutomaticCheckForUpdates = true;
            _sparkleNet.UpdateInterval = new TimeSpan(1, 0, 0);
            _sparkleNet.SetLanguage("en_US");
        }

        int success_index = -2;


        void TimeoutJtag(Process proc, int time)
        {
            Thread.Sleep(2000);
            int count = 0;
            int value = progressBar1.Value;

            while (count < 40 && !proc.HasExited)
            {
                value += 1;
                if (value > 100)
                {
                    value = 100;
                }
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = value;
                });
                Thread.Sleep(time);
                count++;
            }

            if (count >= 40)
            {

                try
                {
                    if (!proc.HasExited)
                    {
                        //proc.Kill();
                        KillProcessAndChildren(proc.Id);
                    }
                }
                catch (Exception)
                {

                }

            }
        }

        public int IndexOfAny(string test, string[] values)
        {
            int first = -1;
            foreach (string item in values)
            {
                int i = test.IndexOf(item);
                if (i >= 0)
                {
                    if (first > 0)
                    {
                        if (i < first)
                        {
                            first = i;
                        }
                    }
                    else
                    {
                        first = i;
                    }
                }
            }
            return first;
        }

        private static void KillProcessAndChildren(int pid)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher
              ("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
            }
            try
            {
                Process proc = Process.GetProcessById(pid);
                Debug.WriteLine("Kill: "+proc.ProcessName+" PID: "+pid);
                proc.Kill();
            }
            catch (ArgumentException)
            {
                // Process already exited.
            }
        }

        void WriteToDebug(object sendingProcess, DataReceivedEventArgs outLine)
        {
            Debug.WriteLine(outLine.Data);
            result_global += outLine.Data + "\n";
        }

        string result_global = "";

        /// <span class="code-SummaryComment"><summary></span>
        /// Executes a shell command synchronously.
        /// <span class="code-SummaryComment"></summary></span>
        /// <span class="code-SummaryComment"><param name="command">string command</param></span>
        /// <span class="code-SummaryComment"><returns>string, as output of the command.</returns></span>
        public string ExecuteCommandSync(object command, int time = 1100)
        {
            result_global = "";
            string resultado = "";
            try
            {
                // create the ProcessStartInfo using "cmd" as the program to be run,
                // and "/c " as the parameters.
                // Incidentally, /c tells cmd that we want it to execute the command that follows,
                // and then exit.
                System.Diagnostics.ProcessStartInfo procStartInfo =
                    new System.Diagnostics.ProcessStartInfo("cmd", "/c " + "\"" + command + "\"");

                Debug.WriteLine(command);
                // The following commands are needed to redirect the standard output.
                // This means that it will be redirected to the Process.StandardOutput StreamReader.
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.RedirectStandardInput = true;
                procStartInfo.UseShellExecute = false;
                // Do not create the black window.
                procStartInfo.CreateNoWindow = true;
                // Now we create a process, assign its ProcessStartInfo and start it
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo = procStartInfo;

                DataReceivedEventHandler dat = new DataReceivedEventHandler(WriteToDebug);
                proc.OutputDataReceived += dat;

                Thread thread = new Thread(() => TimeoutJtag(proc, time));
                thread.Name = "TimeoutJtag";
                thread.Start();
                proc.Start();
                StreamWriter myStreamWriter = proc.StandardInput;
                myStreamWriter.WriteLine("\n");
                proc.BeginOutputReadLine();
                while (!proc.HasExited)
                {
                    Thread.Sleep(100);
                }

                int f = 1;
                try
                {
                    f = proc.ExitCode;
                }
                catch (InvalidOperationException)
                {
                    KillProcessAndChildren(proc.Id);
                    f = proc.ExitCode;
                }
                
                if (f != -1)
                {
                    Thread.Sleep(100);
                    if (thread.IsAlive)
                    {
                        while (thread.IsAlive)
                        {
                            Thread.Sleep(100);
                        }
                    }
                }

                if (f == -1)
                {
                    result_global = "ERROR: Timeout writing Firmware.\nPlease, reconnect the JTAG from PC.\n";
                }

                //this.Invoke((MethodInvoker)delegate()
                //{
                //    progressBar1.Value = 100;
                //});

                DateTime saveNow = DateTime.Now;
                string dateStr = saveNow.ToString(datePatt);
                result_global = dateStr + System.Environment.NewLine + result_global;
                int error_index = result_global.IndexOf("ERROR");
                if (error_index >= 0 )
                {
                    this.Invoke((MethodInvoker)delegate()
                    {
                        label11.Text = "Status: ERROR";
                        pictureBox3.Visible = true;
                        pictureBox2.Visible = false;
                        pictureBox4.Visible = false;
                    });
                    string error_msg = result_global.Substring(error_index, result_global.IndexOf("\n", error_index) - error_index + 1);
                    if (error_msg.Contains("ERROR: Could not open port") || error_msg.Contains("ERROR: Cannot identify a device") || error_msg.Contains("ERROR: Timeout writing Firmware"))
                    {
                        error_msg += "\nPlease, reconnect the JTAG from PC.";
                    }
                    MessageBox.Show(error_msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    if (jtag_type == TypeProgrammer.OLIMEX) //Olimex
                    {
                        this.Invoke((MethodInvoker)delegate()
                        {
                            //pictureBox2.Visible = true;
                            pictureBox3.Visible = false;
                            pictureBox4.Visible = false;
                        });
                    }
                    else if (jtag_type == TypeProgrammer.TEXAS) //TI
                    {
                        if (f == 0)
                        {
                            if (success_index < 1)
                            {
                                success_index = result_global.IndexOf("Programming successful");
                            }

                            if (success_index > 0)
                            {
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    pictureBox2.Visible = true;
                                    pictureBox3.Visible = false;
                                    pictureBox4.Visible = false;
                                });
                            }
                            else
                            {
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    label11.Text = "Status: Check LOG";
                                    pictureBox4.Visible = true;
                                    pictureBox2.Visible = false;
                                    pictureBox3.Visible = false;
                                });
                            }
                        }
                        else
                        {
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                label11.Text = "Status: ERROR";
                                pictureBox3.Visible = true;
                                pictureBox2.Visible = false;
                                pictureBox4.Visible = false;
                            });
                        }
                    }
                    else if (jtag_type == TypeProgrammer.JLINK) // J-LINK
                    {

                        string[] errores = new string[] { "Cannot connect to J-Link", "Verify failed", "Cannot connect to target", "Writing target memory failed", "Error while parsing parameter" };
                        //Verification...OK  Programming Complete.
                        int error_JL = IndexOfAny(result_global, errores);
                        int programming = result_global.IndexOf("Flash Programming");
                        int ok_programmimg = result_global.IndexOf("Verification...OK");
                        if (error_JL >= 0 || (programming >= 0 && ok_programmimg < 0))
                        {
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                label11.Text = "Status: ERROR";
                                pictureBox3.Visible = true;
                                pictureBox2.Visible = false;
                                pictureBox4.Visible = false;
                            });
                            if (error_JL >= 0)
                            {
                                string error_msg = result_global.Substring(error_JL, result_global.IndexOf("\n", error_JL) - error_JL + 1);
                                if (result_global.Contains("FAILED: Cannot connect to J-Link."))
                                {
                                    error_msg = "\nFAILED: Cannot connect to J-Link.";
                                }

                                MessageBox.Show(error_msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        else
                        {
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                //pictureBox2.Visible = true;
                                pictureBox3.Visible = false;
                                pictureBox4.Visible = false;
                            });
                        }

                    }
                    else if (jtag_type == TypeProgrammer.STLINK)
                    {
                        string[] errores = new string[] { "Unexpected error", "Error occured during program operation!", "Unable to open file!", "Unable to connect to ST-LINK!", "No ST-LINK detected!", "No ST-LINK [ID=" };
                        //Verification...OK  Programming Complete.
                        int error_ST = IndexOfAny(result_global, errores);
                        int programming = result_global.IndexOf("Flash Programming");
                        int ok_programmimg = result_global.IndexOf("Verification...OK");
                        if (error_ST >= 0 || (programming >= 0 && ok_programmimg<0))
                        {
                            this.Invoke((MethodInvoker)delegate()
                            {
                                label11.Text = "Status: ERROR";
                                pictureBox3.Visible = true;
                                pictureBox2.Visible = false;
                                pictureBox4.Visible = false;
                            });
                            if (error_ST >= 0)
                            {
                                string error_msg = result_global.Substring(error_ST, result_global.IndexOf("\n", error_ST) - error_ST + 1);
                                if (result_global.Contains("Unable to connect to ST-LINK!"))
                                {
                                    error_msg += "\nUnable to connect to ST-LINK!";
                                }
                                MessageBox.Show(error_msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        else
                        {
                            this.Invoke((MethodInvoker)delegate()
                            {
                                //pictureBox2.Visible = true;
                                pictureBox3.Visible = false;
                                pictureBox4.Visible = false;
                            });
                        }
                    }                  
                }
                resultado = result_global;
                log = log + "\n" + result_global;
                // Display the command output.
                this.Invoke((MethodInvoker)delegate()
                {
                    richTextBox1.Text = log;
                });

                if (jtag_type == TypeProgrammer.OLIMEX)
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(path + "\\Log_Olimex");
                        string logPath = path + "\\Log_Olimex\\Olimex_log.txt";
                        if (!File.Exists(logPath))
                        {
                            // Create a file to write to.
                            using (StreamWriter sw = File.CreateText(logPath))
                            {
                                sw.WriteLine("--------------------------------------------------\n");
                                sw.WriteLine("           OLIMEX LOG -- WiseFlasher\n");
                                sw.WriteLine("--------------------------------------------------\n");
                                sw.WriteLine(System.Environment.NewLine);
                            }
                        }

                        using (StreamWriter sw = File.AppendText(logPath))
                        {
                            sw.WriteLine(result_global);
                        }	
                    }
                    catch (Exception FileLoadException)
                    {
                        MessageBox.Show("Error occurred when trying write log to disk.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.Invoke((MethodInvoker)delegate()
                        {
                            label11.Text = "Status: Error occurred!";
                            label11.Refresh();
                        });
                    }

                }
                if (jtag_type == TypeProgrammer.STLINK) //ST
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(path + "\\Log_ST");
                        string logPath = path + "\\Log_ST\\ST_log.txt";
                        if (!File.Exists(logPath))
                        {
                            // Create a file to write to.
                            using (StreamWriter sw = File.CreateText(logPath))
                            {
                                sw.WriteLine("--------------------------------------------------\n");
                                sw.WriteLine("           ST-LINK LOG -- WiseFlasher\n");
                                sw.WriteLine("--------------------------------------------------\n");
                                sw.WriteLine(System.Environment.NewLine);
                            }
                        }

                        using (StreamWriter sw = File.AppendText(logPath))
                        {
                            sw.WriteLine(result_global);
                        }
                    }
                    catch (Exception FileLoadException)
                    {
                        MessageBox.Show("Error occurred when trying write log to disk.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.Invoke((MethodInvoker)delegate()
                        {
                            label11.Text = "Status: Error occurred!";
                            label11.Refresh();
                        });
                    }

                }

                if (jtag_type == TypeProgrammer.JLINK) // J-LINK
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(path + "\\Log_J-LINK");
                        string logPath = path + "\\Log_J-LINK\\J-LINK_log.txt";
                        if (!File.Exists(logPath))
                        {
                            // Create a file to write to.
                            using (StreamWriter sw = File.CreateText(logPath))
                            {
                                sw.WriteLine("--------------------------------------------------\n");
                                sw.WriteLine("           J-LINK LOG -- WiseFlasher\n");
                                sw.WriteLine("--------------------------------------------------\n");
                                sw.WriteLine(System.Environment.NewLine);
                            }
                        }

                        using (StreamWriter sw = File.AppendText(logPath))
                        {
                            sw.WriteLine(result_global);
                        }
                    }
                    catch (Exception FileLoadException)
                    {
                        MessageBox.Show("Error occurred when trying write log to disk.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            label11.Text = "Status: Error occurred!";
                            label11.Refresh();
                        });
                    }
                }

                Console.WriteLine(result_global);
            }
            catch (Exception objException)
            {
                MessageBox.Show("Error occurred when trying to execute command.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Invoke((MethodInvoker)delegate()
                {
                    label11.Text = "Status: Error occurred!";
                    label11.Refresh();
                });
            }
            return resultado;
        }

        private void basicToolStripMenuItem_Click(object sender, EventArgs e)
        {
            panel5.Enabled = false;
            panel6.Enabled = false;
            advanced = false;
        }

        private void advanceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            advanced = true;
            panel5.Enabled = true;
            panel6.Enabled = true;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            OpenFile();
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            if (DeviceSelectedIndex > -1)
                display_empty_mem();
            label8.Text = "";
            this.Text = "WiseFlasher";
            label11.Text = "Status: Ready.";
            firmwarePath = "";
            pictureBox2.Visible = false;
            pictureBox3.Visible = false;
            pictureBox4.Visible = false;
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private bool patch(string path)
        {
            bool Execute = false;
            bool result = true;

            string address = "";
            string crc = "";
            string address2 = "";
            string id = "";
            string id2 = "";
            System.IO.StreamReader sr = new
                  System.IO.StreamReader(path);
            try
            {           
                address = sr.ReadLine();
                crc = sr.ReadLine();
                address2 = sr.ReadLine();
                char[] buffer = new char[39504];
                sr.ReadBlock(buffer, 0, 39504);
                for (int x = 39407; x < 39456; x++)
                    id += buffer[x];
                for (int x = 39456; x < 39497; x++)
                    id2 += buffer[x];           
            }
            catch (Exception)
            {

            }
            finally
            {
                sr.Close();
            }

            
            if ( address.Equals("@5C00") && crc.Equals("9D 28 ") && address2.Equals("@5D56") && id.Equals("\n57 50 41 4E 20 76 32 2E 39 2E 37 20 44 72 6F 70\n") && id2.Equals("5F 4A 75 6C 20 32 36 20 32 30 31 38 00 00"))
            {
                Execute = true;
            }
    

            if (Execute)
            {
                try
                {
                    string text = File.ReadAllText(path);
                    text = text.Replace("9D 28 ", "7A A6 ");
                    text = text.Replace("00 40 00 20 00 10 00 08 00 04 00 02 D8 14 5A 5B", "00 40 00 20 00 10 00 08 00 04 00 02 D9 14 5A 5B");
                    text = text.Replace("0E 41 0E 53 3C 40 D8 14 3D 40 5A 5B B3 13 DA A8", "0E 41 0E 53 3C 40 D9 14 3D 40 5A 5B B3 13 DA A8");
                    text = text.Replace("30 12 40 01 30 12 3A 4E 70 12 03 00 B4 13 A0 11", "30 12 5E 01 30 12 3A 4E 70 12 03 00 B4 13 A0 11");
                    text = text.Replace("3C 40 DC 4F 08 3C 30 12 40 01 30 12 3A 4E 70 12", "3C 40 DC 4F 08 3C 30 12 5E 01 30 12 3A 4E 70 12");
                    File.WriteAllText(path, text);
                    result = true;
                }           
                catch (IOException)
                {
                    MessageBox.Show("Is not possible open the file because it's being used in another program.");
                    result = false;
                }
            }
            return result;
        }

        private void OpenFile() {
            switch ( device )
            {
                case TypeDevice.TE:
                case TypeDevice.Wpan:
                case TypeDevice.WTDrop:
                case TypeDevice.Wisevalve:
                case TypeDevice.MatrizBornera:
                    openFileDialog1.Filter = "Firmwares txt files|*.txt";
                    break;
                     

                case TypeDevice.C1:
                case TypeDevice.C1_EXP:
                case TypeDevice.M1:
                case TypeDevice.ADP_GW:
                case TypeDevice.ADP_GPS:
                case TypeDevice.V1:
                case TypeDevice.C1_LORA:
                    openFileDialog1.Filter = "Firmwares bin files|*.bin";
                    break;

                default:
                    openFileDialog1.Filter = "Firmwares files|*.txt;*.bin";
                    break;
            }


            if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                /*
                System.IO.StreamReader sr = new
                   System.IO.StreamReader(openFileDialog1.FileName);
                //MessageBox.Show(sr.ReadToEnd());
                sr.Close();
                */
                bool pached = patch(openFileDialog1.FileName);

                if (pached)
                {
                    firmwarePath = openFileDialog1.FileName;
                    this.Text = "WiseFlasher - " + openFileDialog1.FileName;
                    label8.Text = openFileDialog1.SafeFileName;
                    label11.Text = "Status: Ready.";
                    pictureBox2.Visible = false;
                    pictureBox3.Visible = false;
                    pictureBox4.Visible = false;
                    progressBar1.Value = 0;
                    display_empty_mem();
                }
                else
                {
                    firmwarePath = "";
                    this.Text = "WiseFlasher";
                    label8.Text = "";
                    label11.Text = "";
                    pictureBox2.Visible = false;
                    pictureBox3.Visible = false;
                    pictureBox4.Visible = false;
                    progressBar1.Value = 0;
                    display_empty_mem();
                }
            }
        }

        private void SaveFile() {
            Stream myStream;

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                if ((myStream = saveFileDialog1.OpenFile()) != null)
                {
                    string s = "";
                    int index;
                    bool first = true;
                    foreach (string str in richTextBox3.Lines)
                    {
                        if (first)
                        {
                            first = false;
                            index = str.IndexOf(" ");
                            s = "@" + str.Substring(0,index) + System.Environment.NewLine;
                            myStream.Write(Encoding.Default.GetBytes(s), 0, s.Length);
                            s = str.Substring(index+1) + System.Environment.NewLine;
                            myStream.Write(Encoding.Default.GetBytes(s), 0, s.Length);
                        }
                        else
                        {
                            index = str.IndexOf(" ");
                            s = str.Substring(index+1) + System.Environment.NewLine;
                            myStream.Write(Encoding.Default.GetBytes(s), 0, s.Length);
                        }
                    }
                    first = true;
                    bool first_line = true;
                    foreach (string str in richTextBox4.Lines)
                    {
                        if (first)
                        {
                            if (first_line)
                            {
                                index = str.IndexOf(" ");
                                s = "@" + str.Substring(0, index) + System.Environment.NewLine;
                                myStream.Write(Encoding.Default.GetBytes(s), 0, s.Length);
                                s = str.Substring(index + 1);
                                first_line = false;
                            }
                            else
                            {
                                index = str.IndexOf(" ");
                                s = s + " " + str.Substring(index + 1) + System.Environment.NewLine;
                                myStream.Write(Encoding.Default.GetBytes(s), 0, s.Length);
                                first = false;
                                first_line = true;
                            }
                            
                        }
                        else
                        {
                            if (first_line)
                            {
                                index = str.IndexOf(" ");
                                s = str.Substring(index + 1);
                                first_line = false;
                            }
                            else
                            {
                                index = str.IndexOf(" ");
                                s = s + " " + str.Substring(index + 1) + System.Environment.NewLine;
                                myStream.Write(Encoding.Default.GetBytes(s), 0, s.Length);
                                first_line = true;
                            }
                        }
                    }
                    s = "q" + System.Environment.NewLine;
                    myStream.Write(Encoding.Default.GetBytes(s), 0, s.Length);
                    myStream.Close();

                    label11.Text = "Status: File correctly saved!";
                    label11.Refresh();
                }
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFile();
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            SaveFile();
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFile();
        }

        private void enable_ui(bool state)
        {
            ui_state = state;
            this.Invoke((MethodInvoker)delegate() { 
                comboBox1.Enabled = state; 
                comboBox2.Enabled = state;
                buttonEasy.Enabled = state;
                modeToolStripMenuItem.Enabled = state;
                fileToolStripMenuItem.Enabled = state;
                toolStripButton1.Enabled = state;
                toolStripButton2.Enabled = state;
                toolStripButton3.Enabled = state;

                if (state && advanced)
                {
                    panel5.Enabled = true;
                    panel6.Enabled = true;
                }
                else 
                {
                    panel5.Enabled = false;
                    panel6.Enabled = false;
                }
            });
        }

        // Erase & Write & Verify & Run
        private void buttonEasy_Click(object sender, EventArgs e)
        {
            Thread thread = new Thread(() => Easy_Click());
            thread.Name = "Easy_Click";
            thread.Start();
        }

        private void Easy_Click()
        {
            enable_ui(false);
            success_index = -2;
            this.Invoke((MethodInvoker)delegate() { 
                pictureBox2.Visible = false;
                pictureBox3.Visible = false;
                pictureBox4.Visible = false;
            });

            if (DeviceSelectedIndex == -1)
            {
                MessageBox.Show("Please select a device from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                enable_ui(true);
                return;
            }
            switch (jtag_type)
            {
                case TypeProgrammer.NONE:     // no selected adapter
                    MessageBox.Show("Please select a programming adapter from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case TypeProgrammer.OLIMEX:     // olimex programming adapter
                    easy_olimex();
                    break;
                case TypeProgrammer.TEXAS:     // TI programming adapter
                    easy_TI();
                    /*
                    if (device == TypeDevice.TE)
                    {
                        easy_TI();
                    }
                    else
                    {
                        switch (MessageBox.Show("Texas Programmer will erase Data memory zone, previous configuration will be erased. Are you sure continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
                        {
                            case DialogResult.Yes:
                                easy_TI();
                                break;
                            default:
                                break;
                        }
                    }*/
                    break;
                case TypeProgrammer.STLINK:
                    easy_ST();
                    break;
                case TypeProgrammer.JLINK:
                    easy_JL();
                    break;
                default:
                    break;
            }
            enable_ui(true);
        }


        // Erase
        private void buttonErase_Click(object sender, EventArgs e)
        {
            Thread thread = new Thread(() => Erase_Click());
            thread.Name = "Erase_Click";
            thread.Start();
        }

        void Erase_Click()
        {
            enable_ui(false);
            success_index = -2;
            this.Invoke((MethodInvoker)delegate()
            {
                pictureBox2.Visible = false;
                pictureBox3.Visible = false;
                pictureBox4.Visible = false;
            });

            if (DeviceSelectedIndex == -1)
            {
                MessageBox.Show("Please select a device from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                enable_ui(true);
                return;
            }
            switch (jtag_type)
            {
                case TypeProgrammer.NONE:     // no selected adapter
                    MessageBox.Show("Please select a programming adapter from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case TypeProgrammer.OLIMEX:     // olimex programming adapter
                    erase_olimex();
                    break;
                case TypeProgrammer.TEXAS:     // TI programming adapter
                    erase_TI();
                    break;
                case TypeProgrammer.STLINK:
                    erase_ST();
                    break;
                case TypeProgrammer.JLINK:
                    erase_JL();
                    break;
                default:
                    break;
            }
            enable_ui(true);
        }

        // Verify
        private void buttonVerify_Click(object sender, EventArgs e)
        {
            Thread thread = new Thread(() => Verify_Click());
            thread.Name = "Verify_Click";
            thread.Start(); 
        }

        void Verify_Click()
        {
            enable_ui(false);
            success_index = -2;
            this.Invoke((MethodInvoker)delegate()
            {
                pictureBox2.Visible = false;
                pictureBox3.Visible = false;
                pictureBox4.Visible = false;
            });

            if (DeviceSelectedIndex == -1)
            {
                MessageBox.Show("Please select a device from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                enable_ui(true);
                return;
            }
            switch (jtag_type)
            {
                case TypeProgrammer.NONE:     // no selected adapter
                    MessageBox.Show("Please select a programming adapter from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case TypeProgrammer.OLIMEX:     // olimex programming adapter
                    verify_olimex();
                    break;
                case TypeProgrammer.TEXAS:     // TI programming adapter
                    verify_TI();
                    break;
                case TypeProgrammer.STLINK:
                    verify_ST();
                    break;
                case TypeProgrammer.JLINK:     // J-LINK
                    verify_JL();
                    break;
                default:
                    break;
            }
            enable_ui(true);
        }

        // Write
        private void buttonWrite_Click(object sender, EventArgs e)
        {
            Thread thread = new Thread(() => Write_Click());
            thread.Name = "Write_Click";
            thread.Start(); 
        }

        void Write_Click()
        {
            enable_ui(false);
            success_index = -2;
            this.Invoke((MethodInvoker)delegate()
            {
                pictureBox2.Visible = false;
                pictureBox3.Visible = false;
                pictureBox4.Visible = false;
            });

            if (DeviceSelectedIndex == -1)
            {
                MessageBox.Show("Please select a device from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                enable_ui(true);
                return;
            }
            switch (jtag_type)
            {
                case TypeProgrammer.NONE:     // no selected adapter
                    MessageBox.Show("Please select a programming adapter from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case TypeProgrammer.OLIMEX:     // olimex programming adapter
                    write_olimex();
                    break;
                case TypeProgrammer.TEXAS:     // TI programming adapter
                    write_TI();
                    break;
                case TypeProgrammer.STLINK:     // ST
                    write_ST();
                    break;
                case TypeProgrammer.JLINK:     // J-LINK
                    write_JL();
                    break;
                default:
                    break;
            }
            enable_ui(true);
        }

        // Read
        private void buttonRead_Click(object sender, EventArgs e)
        {
            Thread thread = new Thread(() => Read_Click());
            thread.Name = "Read_Click";
            thread.Start();
        }



        public void Write_XBEE_Parameters_Click()
        {

            enable_ui(false);
            success_index = -2;
            this.Invoke((MethodInvoker)delegate ()
            {
                pictureBox2.Visible = false;
                pictureBox3.Visible = false;
                pictureBox4.Visible = false;
            });

            if (DeviceSelectedIndex == -1)
            {
                MessageBox.Show("Please select a device from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                enable_ui(true);
                return;
            }
            switch (jtag_type)
            {
                case TypeProgrammer.NONE:     // no selected adapter
                    MessageBox.Show("Please select a programming adapter from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case TypeProgrammer.OLIMEX:     // olimex programming adapter
                    read_olimex();
                    break;
                case TypeProgrammer.TEXAS:     // TI programming adapter
                    write_XBEE_TI();
                    break;
                case TypeProgrammer.STLINK:     // ST
                    read_ST();
                    break;
                case TypeProgrammer.JLINK:     // J-LINK
                    read_JL();
                    break;
                default:
                    break;
            }
            enable_ui(true);

        }
        void Read_XBEE_Parameters_Click()
        {
            enable_ui(false);
            success_index = -2;
            this.Invoke((MethodInvoker)delegate ()
            {
                pictureBox2.Visible = false;
                pictureBox3.Visible = false;
                pictureBox4.Visible = false;
            });

            if (DeviceSelectedIndex == -1)
            {
                MessageBox.Show("Please select a device from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                enable_ui(true);
                return;
            }
            switch (jtag_type)
            {
                case TypeProgrammer.NONE:     // no selected adapter
                    MessageBox.Show("Please select a programming adapter from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case TypeProgrammer.OLIMEX:     // olimex programming adapter
                    read_olimex();
                    break;
                case TypeProgrammer.TEXAS:     // TI programming adapter
                    read_XBEE_TI();
                    break;
                case TypeProgrammer.STLINK:     // ST
                    read_ST();
                    break;
                case TypeProgrammer.JLINK:     // J-LINK
                    read_JL();
                    break;
                default:
                    break;
            }
            enable_ui(true);
        }

        private void write_XBEE_TI()
        {

            Device temp = Devices.ElementAt(DeviceSelectedIndex);
            int addr_prog = Convert.ToInt32(temp.program_start, 16);
            int addr_data = Convert.ToInt32(temp.data_start, 16);
            bool read_prog_memory = false;
            bool read_data_memory = false;

            string cmd0 = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -s";
            string resultado = ExecuteCommandSync(cmd0, 2200);

            if (label11.Text.Substring(8, 5) != "ERROR")
            {
                int c = resultado.IndexOf(": MSP430F");
                string name_detected = "";
                if (c > 0)
                {
                    int enter = resultado.IndexOf("\n", c);
                    name_detected = resultado.Substring(c + 2, enter - c - 2);
                }
                string name = "";

                bool OK = false;

                if (device == TypeDevice.TE)
                {
                    name = "MSP430F235";
                    if (name_detected == "MSP430F235" || name_detected == "MSP430F247")
                    {
                        OK = true;
                    }
                    if (name_detected == "MSP430F247")
                    {
                        name = name_detected;
                        addr_prog = 0x8000;
                    }
                }
                else if (device == TypeDevice.Wpan)
                {
                    name = "MSP430F5438A";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                    else if (name_detected == "MSP430F5438")
                    {
                        switch (MessageBox.Show("Device detected is MSP430F5438. Are you sure continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
                        {
                            case DialogResult.Yes:
                                name = name_detected;
                                OK = true;
                                break;
                            default:
                                break;
                        }
                    }
                }
                else if (device == TypeDevice.WTDrop)
                {
                    name = "MSP430F5419";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                    else if (name_detected == "MSP430F5419A")
                    {
                        switch (MessageBox.Show("Device detected is MSP430F5419A. Are you sure continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
                        {
                            case DialogResult.Yes:
                                OK = true;
                                name = name_detected;
                                break;
                            default:
                                break;
                        }
                    }
                }
                else
                {
                    name = "MSP430F5419";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                }

                if (OK)
                {
                    string cmd = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -n " + name + " -s";



                    string cmd1 = cmd + " -r [memoryTI_MAIN.txt,MAIN]";
                    string cmd2 = cmd + " -r [memoryTI_INFO.txt,INFO]";

                    if (checkHardReset.Checked || checkRun.Checked)
                    {
                        cmd2 = cmd2 + " -z ";
                        if (checkHardReset.Checked && checkRun.Checked)
                            cmd2 = cmd2 + "[RESET,VCC]";
                        if (checkRun.Checked && !checkHardReset.Checked)
                            cmd2 = cmd2 + "[VCC]";
                        if (!checkRun.Checked && checkHardReset.Checked)
                            cmd2 = cmd2 + "[RESET]";
                    }

                    if (checkReadProgram.Checked)
                    {
                        read_prog_memory = true;
                    }

                    if (checkReadData.Checked)
                    {
                        read_data_memory = true;
                    }

                    if (read_prog_memory)
                    {
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            progressBar1.Value = 0;
                            label11.Text = "Status: Reading program memory...";
                            label11.Refresh();
                        });
                        ExecuteCommandSync(cmd1);
                        if (label11.Text.Substring(8, 5) != "ERROR")
                        {
                            if (read_data_memory)
                            {
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    progressBar1.Value = 50;
                                    pictureBox2.Visible = false;
                                    pictureBox4.Visible = true;
                                    label11.Text = "Status: Reading data memory...";
                                    label11.Refresh();
                                });
                                ExecuteCommandSync(cmd2);

                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    progressBar1.Value = 100;
                                });

                                if (label11.Text.Substring(8, 5) != "ERROR")
                                {
                                    Write_XBEE_content_TI(read_prog_memory, read_data_memory, addr_prog, addr_data);
                                    this.Invoke((MethodInvoker)delegate ()
                                    {
                                        pictureBox2.Visible = true;
                                        pictureBox3.Visible = false;
                                        pictureBox4.Visible = false;
                                        label11.Text = "Status: Reading completed!";
                                        label11.Refresh();
                                    });
                                }
                                else
                                {
                                    this.Invoke((MethodInvoker)delegate ()
                                    {
                                        progressBar1.Value = 100;
                                        pictureBox3.Visible = true;
                                        pictureBox2.Visible = false;
                                        pictureBox4.Visible = false;
                                    });
                                }
                            }
                            else
                            {

                                Write_XBEE_content_TI(read_prog_memory, read_data_memory, addr_prog, addr_data);
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    progressBar1.Value = 100;
                                    pictureBox2.Visible = true;
                                    pictureBox3.Visible = false;
                                    pictureBox4.Visible = false;
                                    label11.Text = "Status: Reading completed!";
                                    label11.Refresh();
                                });
                            }
                        }
                        else
                        {
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                progressBar1.Value = 100;
                                pictureBox3.Visible = true;
                                pictureBox2.Visible = false;
                                pictureBox4.Visible = false;
                            });
                        }
                    }
                    else
                    {
                        if (read_data_memory)
                        {
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                progressBar1.Value = 0;
                                label11.Text = "Status: Reading data memory...";
                                label11.Refresh();
                            });
                            ExecuteCommandSync(cmd2);
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                progressBar1.Value = 100;
                            });
                            if (label11.Text.Substring(8, 5) != "ERROR")
                            {
                                Write_XBEE_content_TI(read_prog_memory, read_data_memory, addr_prog, addr_data);
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    pictureBox2.Visible = true;
                                    pictureBox3.Visible = false;
                                    pictureBox4.Visible = false;
                                    label11.Text = "Status: Reading completed!";
                                    label11.Refresh();
                                });
                            }
                            else
                            {
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    progressBar1.Value = 100;
                                    pictureBox3.Visible = true;
                                    pictureBox2.Visible = false;
                                    pictureBox4.Visible = false;
                                });
                            }
                        }
                    }
                }
                else
                {
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        MessageBox.Show("Found device does not match", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        progressBar1.Value = 100;
                        label11.Text = "Status: ERROR";
                        pictureBox3.Visible = true;
                        pictureBox2.Visible = false;
                        pictureBox4.Visible = false;
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 100;
                });
            }
        }

        private void read_XBEE_TI()
        {
            Device temp = Devices.ElementAt(DeviceSelectedIndex);
            int addr_prog = Convert.ToInt32(temp.program_start, 16);
            int addr_data = Convert.ToInt32(temp.data_start, 16);
            bool read_prog_memory = false;
            bool read_data_memory = false;

            string cmd0 = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -s";
            string resultado = ExecuteCommandSync(cmd0, 2200);

            if (label11.Text.Substring(8, 5) != "ERROR")
            {
                int c = resultado.IndexOf(": MSP430F");
                string name_detected = "";
                if (c > 0)
                {
                    int enter = resultado.IndexOf("\n", c);
                    name_detected = resultado.Substring(c + 2, enter - c - 2);
                }
                string name = "";

                bool OK = false;

                if (device == TypeDevice.TE)
                {
                    name = "MSP430F235";
                    if (name_detected == "MSP430F235" || name_detected == "MSP430F247")
                    {
                        OK = true;
                    }
                    if (name_detected == "MSP430F247")
                    {
                        name = name_detected;
                        addr_prog = 0x8000;
                    }
                }
                else if (device == TypeDevice.Wpan)
                {
                    name = "MSP430F5438A";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                    else if (name_detected == "MSP430F5438")
                    {
                        switch (MessageBox.Show("Device detected is MSP430F5438. Are you sure continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
                        {
                            case DialogResult.Yes:
                                name = name_detected;
                                OK = true;
                                break;
                            default:
                                break;
                        }
                    }
                }
                else if (device == TypeDevice.WTDrop)
                {
                    name = "MSP430F5419";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                    else if (name_detected == "MSP430F5419A")
                    {
                        switch (MessageBox.Show("Device detected is MSP430F5419A. Are you sure continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
                        {
                            case DialogResult.Yes:
                                OK = true;
                                name = name_detected;
                                break;
                            default:
                                break;
                        }
                    }
                }
                else
                {
                    name = "MSP430F5419";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                }

                if (OK)
                {
                    string cmd = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -n " + name + " -s";



                    string cmd1 = cmd + " -r [memoryTI_MAIN.txt,MAIN]";
                    string cmd2 = cmd + " -r [memoryTI_INFO.txt,INFO]";

                    if (checkHardReset.Checked || checkRun.Checked)
                    {
                        cmd2 = cmd2 + " -z ";
                        if (checkHardReset.Checked && checkRun.Checked)
                            cmd2 = cmd2 + "[RESET,VCC]";
                        if (checkRun.Checked && !checkHardReset.Checked)
                            cmd2 = cmd2 + "[VCC]";
                        if (!checkRun.Checked && checkHardReset.Checked)
                            cmd2 = cmd2 + "[RESET]";
                    }

                    if (checkReadProgram.Checked)
                    {
                        read_prog_memory = true;
                    }

                    if (checkReadData.Checked)
                    {
                        read_data_memory = true;
                    }

                    if (read_prog_memory)
                    {
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            progressBar1.Value = 0;
                            label11.Text = "Status: Reading program memory...";
                            label11.Refresh();
                        });
                        ExecuteCommandSync(cmd1);
                        if (label11.Text.Substring(8, 5) != "ERROR")
                        {
                            if (read_data_memory)
                            {
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    progressBar1.Value = 50;
                                    pictureBox2.Visible = false;
                                    pictureBox4.Visible = true;
                                    label11.Text = "Status: Reading data memory...";
                                    label11.Refresh();
                                });
                                ExecuteCommandSync(cmd2);

                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    progressBar1.Value = 100;
                                });

                                if (label11.Text.Substring(8, 5) != "ERROR")
                                {
                                    Lecture_XBEE_content_TI(read_prog_memory, read_data_memory, addr_prog, addr_data);
                                    this.Invoke((MethodInvoker)delegate ()
                                    {
                                        pictureBox2.Visible = true;
                                        pictureBox3.Visible = false;
                                        pictureBox4.Visible = false;
                                        label11.Text = "Status: Reading completed!";
                                        label11.Refresh();
                                    });
                                }
                                else
                                {
                                    this.Invoke((MethodInvoker)delegate ()
                                    {
                                        progressBar1.Value = 100;
                                        pictureBox3.Visible = true;
                                        pictureBox2.Visible = false;
                                        pictureBox4.Visible = false;
                                    });
                                }
                            }
                            else
                            {
              
                                Lecture_XBEE_content_TI(read_prog_memory, read_data_memory, addr_prog, addr_data);
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    progressBar1.Value = 100;
                                    pictureBox2.Visible = true;
                                    pictureBox3.Visible = false;
                                    pictureBox4.Visible = false;
                                    label11.Text = "Status: Reading completed!";
                                    label11.Refresh();
                                });
                            }
                        }
                        else
                        {
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                progressBar1.Value = 100;
                                pictureBox3.Visible = true;
                                pictureBox2.Visible = false;
                                pictureBox4.Visible = false;
                            });
                        }
                    }
                    else
                    {
                        if (read_data_memory)
                        {
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                progressBar1.Value = 0;
                                label11.Text = "Status: Reading data memory...";
                                label11.Refresh();
                            });
                            ExecuteCommandSync(cmd2);
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                progressBar1.Value = 100;
                            });
                            if (label11.Text.Substring(8, 5) != "ERROR")
                            {
                                Lecture_XBEE_content_TI(read_prog_memory, read_data_memory, addr_prog, addr_data);
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    pictureBox2.Visible = true;
                                    pictureBox3.Visible = false;
                                    pictureBox4.Visible = false;
                                    label11.Text = "Status: Reading completed!";
                                    label11.Refresh();
                                });
                            }
                            else
                            {
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    progressBar1.Value = 100;
                                    pictureBox3.Visible = true;
                                    pictureBox2.Visible = false;
                                    pictureBox4.Visible = false;
                                });
                            }
                        }
                    }
                }
                else
                {
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        MessageBox.Show("Found device does not match", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        progressBar1.Value = 100;
                        label11.Text = "Status: ERROR";
                        pictureBox3.Visible = true;
                        pictureBox2.Visible = false;
                        pictureBox4.Visible = false;
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 100;
                });
            }
        }

        void Read_Click()
        {
            enable_ui(false);
            success_index = -2;
            this.Invoke((MethodInvoker)delegate()
            {
                pictureBox2.Visible = false;
                pictureBox3.Visible = false;
                pictureBox4.Visible = false;
            });

            if (DeviceSelectedIndex == -1)
            {
                MessageBox.Show("Please select a device from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                enable_ui(true);
                return;
            }
            switch (jtag_type)
            {
                case TypeProgrammer.NONE:     // no selected adapter
                    MessageBox.Show("Please select a programming adapter from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case TypeProgrammer.OLIMEX:     // olimex programming adapter
                    read_olimex();
                    break;
                case TypeProgrammer.TEXAS:     // TI programming adapter
                    read_TI();
                    break;
                case TypeProgrammer.STLINK:     // ST
                    read_ST();
                    break;
                case TypeProgrammer.JLINK:     // J-LINK
                    read_JL();
                    break;
                default:
                    break;
            }
            enable_ui(true);
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            TypeDevice previousDevice = device;
            Device temp = Devices.Find(delegate (Device dev)
            {
                return dev.name == comboBox1.SelectedItem.ToString();
            });
            DeviceSelectedIndex = Devices.IndexOf(temp);
            if (temp.name == "TE" || temp.name == "X1-EXPANSION")
            {
                device = TypeDevice.TE;
            }
            else if (temp.name == "NODO" || temp.name == "NODE" || temp.name == "WPAN" || temp.name == "RF-X1" || temp.name == "BU-X1")
            {
                device = TypeDevice.Wpan;
            }
            else if (temp.name == "WT-DROP")
            {
                device = TypeDevice.WTDrop;
            }
            else if (temp.name == "WISEVALVE")
            {
                 device = TypeDevice.Wisevalve;
            }
            else if (temp.name == "BU-C1" || temp.name == "RF-C1" || temp.name == "C1")
            {
                device = TypeDevice.C1;
            }
            else if (temp.name == "BU-M1" || temp.name == "RF-M1" || temp.name == "M1")
            {
                device = TypeDevice.M1;
            }
            else if (temp.name == "ADP-GW")
            {
                device = TypeDevice.ADP_GW;
            }
            else if (temp.name == "ADP-GPS")
            {
                device = TypeDevice.ADP_GPS;
            }
            else if (temp.name == "BU-V1")
            {
                device = TypeDevice.V1;
            }
            else if (temp.name == "C1-LORA")
            {
                device = TypeDevice.C1_LORA;
            }
            else if (temp.name.Contains("C1-"))
            {
                device = TypeDevice.C1_EXP;
            }
            else
            {
                device = TypeDevice.MatrizBornera;
            }

            if (temp.type == "MSP430")
            {
                if (!firmwarePath.EndsWith(".txt"))
                {
                    firmwarePath = "";
                    this.Text = "WiseFlasher";
                    label8.Text = "";
                    label11.Text = "";
                    pictureBox2.Visible = false;
                    pictureBox3.Visible = false;
                    pictureBox4.Visible = false;
                    progressBar1.Value = 0;
                }
                if (jtag_type != TypeProgrammer.OLIMEX && jtag_type != TypeProgrammer.TEXAS)
                { 
                    comboBox2.Items.Clear();
                    comboBox2.Enabled = false;
                    System.Windows.Forms.Application.DoEvents();
                    MSP430Disponibles();
                    //comboBox2.Items.Add("TEXAS INSTRUMENTS");
                    //comboBox2.Items.Add("OLIMEX");              
                    comboBox2.Enabled = true;
                    if (comboBox2.Items.Count == 1)
                        comboBox2.SelectedIndex = 0;
                    else
                        jtag_type = TypeProgrammer.NONE;
                    //comboBox2.SelectedIndex = (adapter == TypeProgrammer.OLIMEX || adapter== TypeProgrammer.TEXAS) ? ((adapter== TypeProgrammer.TEXAS) ?0:1) : 0;
                }
            }
            else if (temp.type == "ATSAML21E18B" || temp.type == "STM32G030F6" || temp.type == "AMA3B1KK-KBR")
            {
                if (!firmwarePath.EndsWith(".bin"))
                {
                    firmwarePath = "";
                    this.Text = "WiseFlasher";
                    label8.Text = "";
                    label11.Text = "";
                    pictureBox2.Visible = false;
                    pictureBox3.Visible = false;
                    pictureBox4.Visible = false;
                    progressBar1.Value = 0;
                }
                if (jtag_type != TypeProgrammer.JLINK || (previousDevice != TypeDevice.ADP_GW && previousDevice != TypeDevice.ADP_GPS && previousDevice != TypeDevice.V1 && previousDevice != TypeDevice.C1_LORA))
                {
                    comboBox2.Items.Clear();
                    comboBox2.Enabled = false;
                    System.Windows.Forms.Application.DoEvents();
                    JlinkDisponibles();
                    //comboBox2.Items.Add("J-LINK");
                    comboBox2.Enabled = true;
                    //if (comboBox2.Items.Count == 1 || jtag_type == TypeProgrammer.NONE)
                    if (comboBox2.Items.Count == 1)
                        comboBox2.SelectedIndex = 0;
                    else
                        jtag_type = TypeProgrammer.NONE;
                }
            }
            else
            {
                if (!firmwarePath.EndsWith(".bin"))
                {
                    firmwarePath = "";
                    this.Text = "WiseFlasher";
                    label8.Text = "";
                    label11.Text = "";
                    pictureBox2.Visible = false;
                    pictureBox3.Visible = false;
                    pictureBox4.Visible = false;
                    progressBar1.Value = 0;
                }
                if ((jtag_type != TypeProgrammer.STLINK && jtag_type != TypeProgrammer.JLINK) || previousDevice == TypeDevice.ADP_GW || previousDevice == TypeDevice.ADP_GPS || previousDevice == TypeDevice.V1 || previousDevice == TypeDevice.C1_LORA) 
                {
                    comboBox2.Items.Clear();
                    comboBox2.Enabled = false;
                    System.Windows.Forms.Application.DoEvents();
                    STDisponibles();
                    JlinkDisponibles();
                    //comboBox2.Items.Add("ST-LINK");
                    //comboBox2.Items.Add("J-LINK");
                    comboBox2.Enabled = true;
                    //if (comboBox2.Items.Count == 1 || jtag_type == TypeProgrammer.NONE)
                    if (comboBox2.Items.Count == 1)
                        comboBox2.SelectedIndex = 0;
                    else
                        jtag_type = TypeProgrammer.NONE;
                    //comboBox2.SelectedIndex = (adapter == TypeProgrammer.STLINK || adapter == TypeProgrammer.JLINK) ? ((adapter == TypeProgrammer.STLINK) ? 0 : 1) : 0;
                }
            }
            display_empty_mem();
        }

        string result_global_process = "";

        void WriteToDebug_Process(object sendingProcess, DataReceivedEventArgs outLine)
        {
            Debug.WriteLine(outLine.Data);
            result_global_process += outLine.Data + "\n";
        }

        public string ExecuteProcess(object file, object command)
        {
            result_global_process = "";
            try
            {
                // create the ProcessStartInfo using "cmd" as the program to be run,
                // and "/c " as the parameters.
                // Incidentally, /c tells cmd that we want it to execute the command that follows,
                // and then exit.
                System.Diagnostics.ProcessStartInfo procStartInfo =
                    new System.Diagnostics.ProcessStartInfo((string)file, (string)command); ;

                // The following commands are needed to redirect the standard output.
                // This means that it will be redirected to the Process.StandardOutput StreamReader.
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.UseShellExecute = false;
                // Do not create the black window.
                procStartInfo.CreateNoWindow = true;
                // Now we create a process, assign its ProcessStartInfo and start it
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo = procStartInfo;
                DataReceivedEventHandler dat = new DataReceivedEventHandler(WriteToDebug_Process);
                proc.OutputDataReceived += dat;
                proc.Start();
                proc.BeginOutputReadLine();
                Thread.Sleep(750);
                while (!proc.HasExited)
                {
                    Thread.Sleep(200);
                }
                int f = 1;
                try
                {
                    f = proc.ExitCode;
                }
                catch (InvalidOperationException)
                {
                    KillProcessAndChildren(proc.Id);
                    f = proc.ExitCode;
                }

            }
            catch (Exception)
            {
                MessageBox.Show("Error: System call couldn't be excecuted.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            //para prueba
            return result_global_process;
        }

        public String[] ObtainSerialSTLINK()
        {
            String[] lpa0 = new string[5];
            string lpa = ""; // lista competa a
            string lpb = ""; // lista competa b
            string cmd1_f = path.Replace("\\", "/") + "/ST-LINK Utility/ST-LINK_CLI.exe";
            string cmd1_a = "-list";
            // Buscar serial y cortar cadenas Prueba 2
            lpa = ExecuteProcess(cmd1_f, cmd1_a);
            int j = 1;
            while (lpa.Contains("SN:"))
            {
                int end1 = lpa.Length;
                int star1 = 0;
                int count1 = lpa.IndexOf("SN:", 0, lpa.Length);
                lpa = lpa.Remove(star1, count1);
                lpb = "ST-Link " + lpa.Remove(28, lpa.Length - 28); // Serial Completo
                //*** Serial Corto ***//
                //lpb = lpb.Remove(17,16);
                //*** *********** ***//
                lpa0[j] = lpb;
                lpa = lpa.Remove(0, 4);
                j++;
            }

            lpa0[0] = System.Convert.ToString(j);


            return lpa0;
        }

        public List<string> ObtainSerialJLINK()
        {
            String[] lpa0 = new string[5];
            string lpa = ""; // lista competa a
            string lpb = ""; // lista competa b
            string cmd1_f = path.Replace("\\", "/") + "/J-Link/inspect_emdll.exe";
            string cmd1_a = "-list";
            // Buscar serial y cortar cadenas Prueba 2
            lpa = ExecuteProcess(cmd1_f, cmd1_a);
            int j = 1;
            string[] lines = lpa.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            List<string> Jlink_List = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("J-Link"))
                {
                    string serial = lines[i].Substring(lines[i].IndexOf(": ") + 2) + " SN: " + lines[i].Substring(0, lines[i].IndexOf(": "));
                    Jlink_List.Add(serial);
                }

            }

            return Jlink_List;
        }
        /*
        private void puertosDisponibles()
        {
            string[] HardwareList = hwh.GetCOM();
            string[] news = System.IO.Ports.SerialPort.GetPortNames();
            for (int i = 0; i < news.Length; i++)
            {
                int cont = 0;
                for (int u = 0; u < cbPuertos.Items.Count; u++)
                {
                    string com = cbPuertos.Items[u].ToString();
                    if (com.Contains(news[i]))
                    {
                        cont = 1;
                        break;
                    }
                }

                if (cont == 0)
                {
                    string st = null;
                    if (HardwareList.Length > 0)
                    {
                        try
                        {
                            st = HardwareList.First(s => s.Contains(news[i]));
                        }
                        catch (Exception)
                        {
                        }
                    }
                    if (st != null)
                    {
                        if (!st.Contains("Olimex Tiny") && !st.Contains("MSP-FET430UIF") && !st.Contains("MSP") && !st.Contains("JLink"))
                        {
                            // Para TB M1 y C1 agrega su nombre para mejor identificación
                            try
                            {
                                List<Win32DeviceMgmt.DeviceInfo> deviceInfos = Win32DeviceMgmt.GetAllCOMPorts();
                                for (int devInfoIndex = 0; devInfoIndex < deviceInfos.Count; devInfoIndex++)
                                {
                                    if (deviceInfos[devInfoIndex].bus_description == TestbedC1.testbedDevDescription ||
                                        deviceInfos[devInfoIndex].bus_description == Tester_M1.testbedDevDescription)
                                    {
                                        if (news[i].Contains(deviceInfos[devInfoIndex].name))
                                        {
                                            st = deviceInfos[devInfoIndex].bus_description + " " + st;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                WriteLog(ex.Message);
                            }

                            cbPuertos.Items.Add(st);

                        }
                    }
                }
            }

            for (int i = 0; i < cbPuertos.Items.Count; i++)
            {
                string com = cbPuertos.Items[i].ToString();
                int index = com.IndexOf("(COM");
                string temp = com.Substring(index + 1, com.IndexOf(")", index) - index - 1);

                if (!news.Contains(temp))
                {
                    cbPuertos.Items.RemoveAt(i);
                    if (PuertoCOM == temp)
                    {
                        PuertoCOM = "";
                        PreviousPuertoCOM = PuertoCOM;
                    }
                }
            }
        }
        */

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case HardwareHelperLib.Native.WM_DEVICECHANGE:
                    {
                        if (m.WParam.ToInt32() == HardwareHelperLib.Native.DBT_DEVNODES_CHANGED)
                        {
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                if (ui_state && comboBox1.SelectedIndex != -1)
                                {
                                    Device temp = Devices.Find(delegate (Device dev)
                                    {
                                        return dev.name == comboBox1.SelectedItem.ToString();
                                    });
                                    DeviceSelectedIndex = Devices.IndexOf(temp);
                                    comboBox2.Enabled = false;
                                    System.Windows.Forms.Application.DoEvents();
                                    if (temp.type == "MSP430")
                                    {
                                        MSP430Disponibles();

                                    }
                                    else if (temp.type == "ATSAML21E18B" || temp.type == "STM32G030F6" || temp.type == "AMA3B1KK-KBR")
                                    {
                                        JlinkDisponibles();
                                    }
                                    else
                                    {
                                        STDisponibles();
                                        JlinkDisponibles();
                                    }
                                    if (comboBox2.Items.Count == 1)
                                        comboBox2.SelectedIndex = 0;
                                    if (comboBox2.Items.Count == 0)
                                        jtag_type = TypeProgrammer.NONE;
                                    comboBox2.Enabled = true;
                                }
                            });
                        }
                        break;
                    }
            }
            base.WndProc(ref m);
        }

        private void MSP430Disponibles()
        {
            //reconnectOlimex = false;
            string[] HardwareList = hwh.GetCOM();
            string[] news = System.IO.Ports.SerialPort.GetPortNames();
            for (int i = 0; i < news.Length; i++)
            {
                int cont = 0;
                for (int u = 0; u < comboBox2.Items.Count; u++)
                {
                    string com = comboBox2.Items[u].ToString();
                    if (com.Contains(news[i]))
                    {
                        cont = 1;
                        break;
                    }
                }

                if (cont == 0)
                {
                    string st = null;
                    if (HardwareList.Length > 0)
                    {
                        try
                        {
                            st = HardwareList.First(s => s.Contains(news[i]));
                        }
                        catch (Exception)
                        {
                        }
                    }
                    if (st != null)
                    {
                        if (st.Contains("Olimex Tiny") )
                        {
                            comboBox2.Items.Add(st);
                        }
                        if (st.Contains("MSP Debug Interface") || st.Contains("MSP-FET430UIF"))
                        {
                            comboBox2.Items.Add("Texas " + st);
                        }
                    }
                }
            }
            for (int i = 0; i < comboBox2.Items.Count; i++)
            {
                string name = comboBox2.Items[i].ToString();
                string port = name.Substring(name.IndexOf("(COM") + 1, name.IndexOf(")") - name.IndexOf("(COM") - 1);
                if (!news.Contains(port))
                {
                    comboBox2.Items.RemoveAt(i);
                    if (jtag_port == port)
                    {
                        jtag_port = "";
                        jtag_name = "";
                        jtag_serial = "";
                        jtag_type = TypeProgrammer.NONE;
                    }
                }
            }
        }

        private void ARM_Programmer_Availables()
        {
            STDisponibles();
            JlinkDisponibles();
        }

        private void STDisponibles()
        {
            //************ Puertos ST ***************//
            String[] Puerto = ObtainSerialSTLINK();
            int j = System.Convert.ToInt32(Puerto[0]);
            for (int u = 1; u < j; u++)
            {
                int cont = 0;
                for (int k = 0; k < comboBox2.Items.Count; k++)
                {
                    string com = comboBox2.Items[k].ToString();
                    if (com.Contains(Puerto[u]))
                    {
                        cont = 1;
                        break;
                    }
                }

                if (cont == 0)
                {
                    if (Puerto[u].Contains("SN:"))
                    {
                        comboBox2.Items.Add(Puerto[u]);
                    }
                }
            }
            for (int i = 0; i < comboBox2.Items.Count; i++)
            {
                string name = comboBox2.Items[i].ToString();
                if (!Puerto.Contains(name) && !name.Contains("J-Link"))
                {
                    comboBox2.Items.RemoveAt(i);
                    if (jtag_name == name)
                    {
                        jtag_name = "";
                        jtag_serial = "";
                        jtag_port = "";
                        jtag_type = TypeProgrammer.NONE;
                    }
                }
            }
        }

        private void JlinkDisponibles()
        {
            //************ Puertos ST ***************//
            String[] Puerto = ObtainSerialJLINK().ToArray();
            int j = Puerto.Length;
            for (int u = 0; u < j; u++)
            {
                int cont = 0;
                for (int k = 0; k < comboBox2.Items.Count; k++)
                {
                    string com = comboBox2.Items[k].ToString();
                    if (com.Contains(Puerto[u]))
                    {
                        cont = 1;
                        break;
                    }
                }

                if (cont == 0)
                {
                    if (Puerto[u].Contains("SN:"))
                    {
                        comboBox2.Items.Add(Puerto[u]);
                    }
                }
            }
            for (int i = 0; i < comboBox2.Items.Count; i++)
            {
                string name = comboBox2.Items[i].ToString();
                if (!Puerto.Contains(name) && !name.Contains("ST-Link"))
                {
                    comboBox2.Items.RemoveAt(i);
                    if (jtag_name == name)
                    {
                        jtag_name = "";
                        jtag_serial = "";
                        jtag_port = "";
                        jtag_type = TypeProgrammer.NONE;
                    }
                }
            }
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox2.SelectedIndex != -1)
            {
                if (comboBox2.SelectedItem != null && comboBox2.SelectedItem.ToString() != "")
                {
                    if (comboBox2.SelectedItem.ToString().Contains("COM") && (comboBox2.SelectedItem.ToString().Contains("MSP") || comboBox2.SelectedItem.ToString().Contains("Olimex")))
                    {
                        string name = comboBox2.SelectedItem.ToString();
                        string port = name.Substring(name.IndexOf("(COM") + 1, name.IndexOf(")") - name.IndexOf("(COM") - 1);
                        jtag_name = name;
                        jtag_serial = "";
                        jtag_port = port;
                        jtag_type = (comboBox2.SelectedItem.ToString().Contains("Olimex")) ? TypeProgrammer.OLIMEX : TypeProgrammer.TEXAS;
                    }
                    else if (comboBox2.SelectedItem.ToString().Contains("ST-Link"))
                    {
                        string name = comboBox2.SelectedItem.ToString();
                        jtag_name = name;
                        jtag_serial = name.Substring(name.IndexOf("SN: ") + 4);
                        jtag_port = "";
                        jtag_type = TypeProgrammer.STLINK;
                    }
                    else if (comboBox2.SelectedItem.ToString().Contains("J-Link"))
                    {
                        string name = comboBox2.SelectedItem.ToString();
                        jtag_name = name;
                        jtag_serial = name.Substring(name.IndexOf("SN: ") + 4);
                        jtag_port = "";
                        jtag_type = TypeProgrammer.JLINK;
                    }
                    else
                    {
                        jtag_name = "";
                        jtag_serial = "";
                        jtag_port = "";
                        jtag_type = TypeProgrammer.NONE;
                    }
                }
                else
                {
                    jtag_name = "";
                    jtag_serial = "";
                    jtag_port = "";
                    jtag_type = TypeProgrammer.NONE;
                }
            }

            Device temp = Devices.Find(delegate (Device dev)
            {
                return dev.name == comboBox1.SelectedItem.ToString();
            });
            DeviceSelectedIndex = Devices.IndexOf(temp);
            if (temp.type == "MSP430")
            {
                //jtag_type = (comboBox2.SelectedIndex == 0) ? TypeProgrammer.TEXAS : TypeProgrammer.OLIMEX;    // adapter = OLIMEX (1) or TEXAS INSTRUMENTS (2)
                if (jtag_type == TypeProgrammer.OLIMEX)
                {
                    buttonErase.Enabled = true;
                    checkEraseProgram.Enabled = true;
                    checkEraseProgram.Checked = true;
                    checkEraseAll.Checked = false;
                    checkEraseAll.Enabled = true;

                    buttonVerify.Enabled = true;
                    checkVerifyProgram.Checked = true;
                    checkVerifyProgram.Enabled = true;
                    checkVerifyData.Checked = false;
                    checkVerifyData.Enabled = true;

                    buttonWrite.Enabled = true;
                    checkWriteProgram.Checked = true;
                    checkWriteProgram.Enabled = true;
                    checkWriteData.Checked = false;
                    checkWriteData.Enabled = true;

                    buttonRead.Enabled = true;
                    checkReadProgram.Checked = true;
                    checkReadProgram.Enabled = true;
                    checkReadData.Checked = true;
                    checkReadData.Enabled = true;


                    checkSoftReset.Enabled = true;
                    checkSoftReset.Checked = false;

                    checkHardReset.Enabled = true;
                    checkHardReset.Checked = true;

                    checkPowerReset.Checked = false;
                    checkPowerReset.Enabled = true;

                    checkRun.Checked = true;
                    checkRun.Enabled = true;
                }
                if (jtag_type == TypeProgrammer.TEXAS)
                {
                    buttonErase.Enabled = true;
                    checkEraseProgram.Enabled = true;
                    checkEraseProgram.Checked = true;
                    checkEraseAll.Checked = false;
                    checkEraseAll.Enabled = true;

                    buttonVerify.Enabled = true;
                    checkVerifyProgram.Checked = true;
                    checkVerifyProgram.Enabled = true;
                    checkVerifyData.Checked = false;
                    checkVerifyData.Enabled = false;

                    buttonWrite.Enabled = true;
                    checkWriteProgram.Checked = true;
                    checkWriteProgram.Enabled = true;
                    checkWriteData.Checked = false;
                    checkWriteData.Enabled = true;

                    buttonRead.Enabled = true;
                    checkReadProgram.Checked = true;
                    checkReadProgram.Enabled = true;
                    checkReadData.Checked = true;
                    checkReadData.Enabled = true;


                    checkSoftReset.Enabled = false;
                    checkSoftReset.Checked = false;

                    checkHardReset.Enabled = true;
                    checkHardReset.Checked = true;

                    checkPowerReset.Checked = false;
                    checkPowerReset.Enabled = false;

                    checkRun.Checked = true;
                    checkRun.Enabled = true;
                }
            }
            else if (temp.type == "ATSAML21E18B" || temp.type == "STM32G030F6" || temp.type == "AMA3B1KK-KBR")
            {
                //jtag_type = TypeProgrammer.JLINK;

                buttonErase.Enabled = true;
                checkEraseProgram.Enabled = true;
                checkEraseProgram.Checked = true;
                checkEraseAll.Checked = false;
                checkEraseAll.Enabled = true;

                buttonVerify.Enabled = true;
                checkVerifyProgram.Checked = true;
                checkVerifyProgram.Enabled = true;
                checkVerifyData.Checked = false;
                checkVerifyData.Enabled = true;

                buttonWrite.Enabled = true;
                checkWriteProgram.Checked = true;
                checkWriteProgram.Enabled = true;
                checkWriteData.Checked = false;
                checkWriteData.Enabled = true;

                buttonRead.Enabled = true;
                checkReadProgram.Checked = true;
                checkReadProgram.Enabled = true;
                checkReadData.Checked = true;
                checkReadData.Enabled = true;


                checkSoftReset.Enabled = true;
                checkSoftReset.Checked = false;

                checkHardReset.Enabled = true;
                checkHardReset.Checked = true;

                checkPowerReset.Checked = false;
                checkPowerReset.Enabled = false;

                checkRun.Checked = true;
                checkRun.Enabled = true;
            }
            else
            {
                //jtag_type = (comboBox2.SelectedIndex == 0) ? TypeProgrammer.STLINK : TypeProgrammer.JLINK;    // adapter = STLINK (3) or JLINK (4)

                buttonErase.Enabled = true;
                checkEraseProgram.Enabled = true;
                checkEraseProgram.Checked = true;
                checkEraseAll.Checked = false;
                checkEraseAll.Enabled = true;

                buttonVerify.Enabled = true;
                checkVerifyProgram.Checked = true;
                checkVerifyProgram.Enabled = true;
                checkVerifyData.Checked = false;
                checkVerifyData.Enabled = true;

                buttonWrite.Enabled = true;
                checkWriteProgram.Checked = true;
                checkWriteProgram.Enabled = true;    
                checkWriteData.Checked = false;
                checkWriteData.Enabled = true;

                buttonRead.Enabled = true;
                checkReadProgram.Checked = true;
                checkReadProgram.Enabled = true;
                checkReadData.Checked = true;
                checkReadData.Enabled = true;


                checkSoftReset.Enabled = true;
                checkSoftReset.Checked = false;

                checkHardReset.Enabled = true;
                checkHardReset.Checked = true;

                checkPowerReset.Checked = false;              
                checkPowerReset.Enabled = false;

                checkRun.Checked = false;
                checkRun.Enabled = false;
                
            }
        }

        private void read_olimex()
        {
            Device temp = Devices.ElementAt(DeviceSelectedIndex);
            int addr_prog = Convert.ToInt32(temp.program_start, 16);
            int addr_data = Convert.ToInt32(temp.data_start, 16);
            string cmd2 = "\"" + path.Replace("\\", "/") + "/Olimex/mspprog-cli-v2.exe\" /p=" + jtag_port + " /d=" + temp.name;

            if (checkReadProgram.Checked) 
            {
                cmd2 = cmd2 + " /rc";
            }
            if (checkReadData.Checked)
            {
                cmd2 = cmd2 + " /ri";
            }
            cmd2 = cmd2 + " /f=\"" + path.Replace("\\", "/") + "/memory.txt\"";
            
            if (checkSoftReset.Checked || checkHardReset.Checked || checkPowerReset.Checked || checkRun.Checked)
            {
                cmd2 = cmd2 + " /post=";
                if (checkSoftReset.Checked)
                    cmd2 = cmd2 + "s";
                if (checkHardReset.Checked)
                    cmd2 = cmd2 + "h";
                if (checkPowerReset.Checked)
                    cmd2 = cmd2 + "v";
                if (checkRun.Checked)
                    cmd2 = cmd2 + "r";
            }

            if (checkReadProgram.Checked || checkReadData.Checked)
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 0;
                    label11.Text = "Status: Reading memory...";
                    label11.Refresh();
                });
                Console.WriteLine(cmd2);
                ExecuteCommandSync(cmd2);
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 100;
                });
                if (label11.Text.Substring(8, 5) != "ERROR")
                {
                    disp_mem_content_olimex(addr_prog, addr_data);
                    this.Invoke((MethodInvoker)delegate()
                    {
                        pictureBox2.Visible = true;
                        label11.Text = "Status: Reading completed!";
                        label11.Refresh();
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 100;
                    label11.Text = "Status: No operation performed.";
                    label11.Refresh();
                });
            }
            
        }

        private void write_olimex()
        {
            if (firmwarePath == "")
            {
                MessageBox.Show("Please select a file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            this.Invoke((MethodInvoker)delegate()
            {
                progressBar1.Value = 0;
                label11.Text = "Status: Writing file in memory...";
                label11.Refresh();
            });
            Device temp = Devices.ElementAt(DeviceSelectedIndex);
            string cmd2 = "\"" + path.Replace("\\", "/") + "/Olimex/mspprog-cli-v2.exe\" /p=" + jtag_port + " /d=" + temp.name + " /w /f=\"" + firmwarePath + "\"";

            if (!checkWriteData.Checked)
                cmd2 = cmd2 + " /cal=0x" + temp.data_start + ":0x" + temp.data_end;
            if (!checkWriteProgram.Checked)
                cmd2 = cmd2 + " /cal=0x" + temp.program_start + ":0x" + temp.program_end;

            if (checkSoftReset.Checked || checkHardReset.Checked || checkPowerReset.Checked || checkRun.Checked)
            {
                cmd2 = cmd2 + " /post=";
                if (checkSoftReset.Checked)
                    cmd2 = cmd2 + "s";
                if (checkHardReset.Checked)
                    cmd2 = cmd2 + "h";
                if (checkPowerReset.Checked)
                    cmd2 = cmd2 + "v";
                if (checkRun.Checked)
                    cmd2 = cmd2 + "r";
            }

            if (checkWriteProgram.Checked || checkWriteData.Checked)
            {
                ExecuteCommandSync(cmd2);
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 100;
                });
                if (label11.Text.Substring(8, 5) != "ERROR")
                {
                    this.Invoke((MethodInvoker)delegate()
                    {
                        pictureBox2.Visible = true;
                        label11.Text = "Status: Write completed!";
                        label11.Refresh();
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 100;
                    label11.Text = "Status: No operation performed.";
                    label11.Refresh();
                });
            }
        }

        private void verify_olimex()
        {
            if (firmwarePath == "")
            {
                MessageBox.Show("Please select a file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Device temp = Devices.ElementAt(DeviceSelectedIndex);
            string cmd2 = "\"" + path.Replace("\\", "/") + "/Olimex/mspprog-cli-v2.exe\" /p=" + jtag_port + " /d=" + temp.name + " /v /f=\"" + firmwarePath + "\"";
            if (checkSoftReset.Checked || checkHardReset.Checked || checkPowerReset.Checked || checkRun.Checked)
            {
                cmd2 = cmd2 + " /post=";
                if (checkSoftReset.Checked)
                    cmd2 = cmd2 + "s";
                if (checkHardReset.Checked)
                    cmd2 = cmd2 + "h";
                if (checkPowerReset.Checked)
                    cmd2 = cmd2 + "v";
                if (checkRun.Checked)
                    cmd2 = cmd2 + "r";
            }

            if (checkVerifyProgram.Checked || checkVerifyData.Checked)
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 0;
                    label11.Text = "Status: Verifying...";
                    label11.Refresh();
                });
                ExecuteCommandSync(cmd2);
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 100;
                });
                if (label11.Text.Substring(8, 5) != "ERROR")
                {
                    this.Invoke((MethodInvoker)delegate()
                    {
                        pictureBox2.Visible = true;
                        label11.Text = "Status: Verify completed!";
                        label11.Refresh();
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 0;
                    label11.Text = "Status: No operation performed.";
                    label11.Refresh();
                });
            }
        }

        private void erase_olimex()
        {
            Device temp = Devices.ElementAt(DeviceSelectedIndex);
            string cmd2 = "\"" + path.Replace("\\", "/") + "/Olimex/mspprog-cli-v2.exe\" /p=" + jtag_port + " /d=" + temp.name;

            if (checkEraseProgram.Checked && !checkEraseAll.Checked)
                cmd2 = cmd2 + " /ec";
            if (checkEraseAll.Checked)
                cmd2 = cmd2 + " /eall";

            if (checkSoftReset.Checked || checkHardReset.Checked || checkPowerReset.Checked || checkRun.Checked)
            {
                cmd2 = cmd2 + " /post=";
                if (checkSoftReset.Checked)
                    cmd2 = cmd2 + "s";
                if (checkHardReset.Checked)
                    cmd2 = cmd2 + "h";
                if (checkPowerReset.Checked)
                    cmd2 = cmd2 + "v";
                if (checkRun.Checked)
                    cmd2 = cmd2 + "r";
            }
            if (checkEraseProgram.Checked || checkEraseAll.Checked)
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 0;
                    label11.Text = "Status: Erasing memory...";
                    label11.Refresh();
                });
                ExecuteCommandSync(cmd2);
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 100;
                });
                if (label11.Text.Substring(8, 5) != "ERROR")
                {
                    this.Invoke((MethodInvoker)delegate()
                    {
                        pictureBox2.Visible = true;
                        label11.Text = "Status: Erase completed!";
                        label11.Refresh();
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 0;
                    label11.Text = "Status: No operation performed.";
                    label11.Refresh();
                });
            }
        }

        private Device C1_CheckNodeBootloader (Device dev)
        {
            switch (jtag_type)
            {
                case TypeProgrammer.STLINK:
                    {
                        int current_boot_version = 2;
                        if (System.IO.File.Exists(path + "\\memory.bin"))
                        {
                            try
                            {
                                System.IO.File.Delete(path + "\\memory.bin");
                            }
                            catch (System.IO.IOException e)
                            {
                                MessageBox.Show(e.Message);
                            }
                        }

                        string cmd0 = "\"" + path.Replace("\\", "/") + "/ST-LINK Utility/ST-LINK_CLI.exe\" -c SN=" + jtag_serial + " -Dump 0x5FF0 0x10 \"" + path.Replace("\\", "/") + "/memory.bin\" -Q";

                        ExecuteCommandSync(cmd0);

                        if (label11.Text.Substring(8, 5) != "ERROR")
                        {
                            System.IO.FileStream myFile;
                            try
                            {
                                myFile = new System.IO.FileStream(path + "\\memory.bin", FileMode.Open);
                            }
                            catch (System.IO.FileNotFoundException)
                            {
                                return dev;
                            }

                            try
                            {
                                byte[] buffer = new byte[4];
                                if (myFile.Read(buffer, 0, 4) != 0)
                                {
                                    current_boot_version = BitConverter.ToInt32(buffer, 0);
                                }

                                Device temp = Devices.Find(delegate (Device item)
                                {
                                    return dev.name == item.name && Convert.ToInt32(item.boot_version) == current_boot_version;
                                });
                                if (temp != null)
                                {
                                    dev = temp;
                                }
                            }
                            catch (Exception)
                            {

                            }
                            myFile.Close();

                        }

                        if (System.IO.File.Exists(path + "\\memory.bin"))
                        {
                            try
                            {
                                System.IO.File.Delete(path + "\\memory.bin");
                            }
                            catch (System.IO.IOException e)
                            {
                                MessageBox.Show(e.Message);
                            }
                        }
                    }
                    break;

                case TypeProgrammer.JLINK:
                    {
                        int current_boot_version = 2;
                        string cmd0 = "\"" + path.Replace("\\", "/") + "/J-Link/JLink.exe\" -USB " + jtag_serial + " -If SWD -Speed 4000 -Device " + dev.type + " -autoconnect 1 -CommandFile \"";

                        if (File.Exists(path + "\\J-Link\\CommandFile.jlink"))
                        {
                            File.Delete(path + "\\J-Link\\CommandFile.jlink");
                        }

                        System.IO.StreamWriter CommandFile;

                        CommandFile = new System.IO.StreamWriter(path + "\\J-Link\\CommandFile.jlink");
                        CommandFile.WriteLine("SaveBin " + "\"" + path.Replace("\\", "/") + "/J-Link/memory.bin\"" + " 0x5FF0 0x10");
                        CommandFile.WriteLine("RSetType 2\n" +
                                              "Reset");
                        CommandFile.WriteLine("Go");
                        CommandFile.WriteLine("exit");
                        CommandFile.Close();

                        cmd0 = cmd0 + path.Replace("\\", "/") + "/J-Link/CommandFile.jlink" + "\"";

                        ExecuteCommandSync(cmd0);

                        if (label11.Text.Substring(8, 5) != "ERROR")
                        {
                            System.IO.FileStream myFile;
                            try
                            {
                                myFile = new System.IO.FileStream(path + "\\J-Link\\memory.bin", FileMode.Open);
                            }
                            catch (System.IO.FileNotFoundException)
                            {
                                return dev;
                            }

                            try
                            {
                                byte[] buffer = new byte[4];
                                if (myFile.Read(buffer, 0, 4) != 0)
                                {
                                    current_boot_version = BitConverter.ToInt32(buffer, 0);
                                }

                                Device temp = Devices.Find(delegate (Device item)
                                {
                                    return dev.name == item.name && Convert.ToInt32(item.boot_version) == current_boot_version;
                                });
                                if (temp != null)
                                {
                                    dev = temp;
                                }
                            }
                            catch (Exception)
                            {

                            }
                            myFile.Close();

                        }

                        if (System.IO.File.Exists(path + "\\J-Link\\memory.bin"))
                        {
                            try
                            {
                                System.IO.File.Delete(path + "\\J-Link\\memory.bin");
                            }
                            catch (System.IO.IOException e)
                            {
                                MessageBox.Show(e.Message);
                            }
                        }
                    }
                    break;

                default:
                    break;

            }
            return dev;
        }


        private Device C1_CheckNodeDataStart(Device dev, ref int delete_config, ref int data_start)
        {
            switch (jtag_type)
            {
                case TypeProgrammer.STLINK:
                    {
                        int current_boot_version = 2;
                        if (System.IO.File.Exists(path + "\\memory.bin"))
                        {
                            try
                            {
                                System.IO.File.Delete(path + "\\memory.bin");
                            }
                            catch (System.IO.IOException e)
                            {
                                MessageBox.Show(e.Message);
                            }
                        }
                        int prog_start = Convert.ToInt32(dev.program_start, 16);
                        int prog_end = Convert.ToInt32(dev.program_end, 16);
                        int data_end = Convert.ToInt32(dev.data_end, 16);
                        string cmd0 = "\"" + path.Replace("\\", "/") + "/ST-LINK Utility/ST-LINK_CLI.exe\" -c SN=" + jtag_serial + " -Dump 0x" + prog_start.ToString("X") + " 0x" + (data_end - prog_start + 1).ToString("X") + " \"" + path.Replace("\\", "/") + "/memory.bin\" -Q";

                        ExecuteCommandSync(cmd0);

                        if (label11.Text.Substring(8, 5) != "ERROR")
                        {
                            System.IO.FileStream myFile;
                            try
                            {
                                myFile = new System.IO.FileStream(path + "\\memory.bin", FileMode.Open);
                            }
                            catch (System.IO.FileNotFoundException)
                            {
                                return dev;
                            }

                            try
                            {
                                byte[] buffer = new byte[4];
                                myFile.Seek(0x5FF0, SeekOrigin.Begin);
                                if (myFile.Read(buffer, 0, 4) != 0)
                                {
                                    current_boot_version = BitConverter.ToInt32(buffer, 0);
                                }

                                Device temp = Devices.Find(delegate (Device item)
                                {
                                    return dev.name == item.name && Convert.ToInt32(item.boot_version) == current_boot_version;
                                });
                                if (temp != null)
                                {
                                    Device node_dev = temp;
                                    int node_prog_start = Convert.ToInt32(node_dev.program_start, 16);
                                    int node_prog_end = Convert.ToInt32(node_dev.program_end, 16);
                                    int node_data_start = Convert.ToInt32(node_dev.data_start, 16);
                                    int node_data_end = Convert.ToInt32(node_dev.data_end, 16);
                                    if (node_data_start != data_start)
                                    {
                                        if (data_start > node_data_start)
                                        {
                                            byte[] bufferDif = new byte[16];
                                            myFile.Seek(node_data_start - node_prog_start, SeekOrigin.Begin);
                                            int start_index = node_data_start;
                                            while (start_index < data_start && delete_config == 0)
                                            {
                                                int bytes = ((data_start - start_index) >= 16) ? 16 : (data_start - start_index);
                                                int readead = myFile.Read(bufferDif, 0, bytes);
                                                for (int i = 0; i < readead; i++)
                                                {
                                                    if (bufferDif[i] != 0xFF)
                                                    {
                                                        delete_config = 1;
                                                        break;
                                                    }
                                                }
                                                start_index += 16;
                                            }
                                        }
                                        else
                                        {
                                            data_start = node_data_start;
                                        }
                                    }
                                }
                            }
                            catch (Exception)
                            {

                            }
                            myFile.Close();

                        }

                        if (System.IO.File.Exists(path + "\\memory.bin"))
                        {
                            try
                            {
                                System.IO.File.Delete(path + "\\memory.bin");
                            }
                            catch (System.IO.IOException e)
                            {
                                MessageBox.Show(e.Message);
                            }
                        }
                    }
                    break;

                case TypeProgrammer.JLINK:
                    {
                        int current_boot_version = 2;

                        int prog_start = Convert.ToInt32(dev.program_start, 16);
                        int prog_end = Convert.ToInt32(dev.program_end, 16);
                        int data_end = Convert.ToInt32(dev.data_end, 16);

                        string cmd0 = "\"" + path.Replace("\\", "/") + "/J-Link/JLink.exe\" -USB " + jtag_serial + " -If SWD -Speed 4000 -Device " + dev.type + " -autoconnect 1 -CommandFile \"";

                        System.IO.StreamWriter CommandFile;

                        CommandFile = new System.IO.StreamWriter(path + "\\J-Link\\CommandFile.jlink");
                        CommandFile.WriteLine("SaveBin " + "\"" + path.Replace("\\", "/") + "/J-Link/memory.bin\"" + " 0x" + prog_start.ToString("X") + " 0x" + (data_end - prog_start + 1).ToString("X"));
                        CommandFile.WriteLine("RSetType 2\n" +
                                              "Reset");
                        CommandFile.WriteLine("Go");
                        CommandFile.WriteLine("exit");
                        CommandFile.Close();

                        cmd0 = cmd0 + path.Replace("\\", "/") + "/J-Link/CommandFile.jlink" + "\"";

                        ExecuteCommandSync(cmd0);

                        if (label11.Text.Substring(8, 5) != "ERROR")
                        {
                            System.IO.FileStream myFile;
                            try
                            {
                                myFile = new System.IO.FileStream(path + "\\J-Link\\memory.bin", FileMode.Open);
                            }
                            catch (System.IO.FileNotFoundException)
                            {
                                return dev;
                            }

                            try
                            {
                                byte[] buffer = new byte[4];
                                myFile.Seek(0x5FF0, SeekOrigin.Begin);
                                if (myFile.Read(buffer, 0, 4) != 0)
                                {
                                    current_boot_version = BitConverter.ToInt32(buffer, 0);
                                }

                                Device temp = Devices.Find(delegate (Device item)
                                {
                                    return dev.name == item.name && Convert.ToInt32(item.boot_version) == current_boot_version;
                                });
                                if (temp != null)
                                {
                                    Device node_dev = temp;
                                    int node_prog_start = Convert.ToInt32(node_dev.program_start, 16);
                                    int node_prog_end = Convert.ToInt32(node_dev.program_end, 16);
                                    int node_data_start = Convert.ToInt32(node_dev.data_start, 16);
                                    int node_data_end = Convert.ToInt32(node_dev.data_end, 16);
                                    if (node_data_start != data_start)
                                    {
                                        if (data_start > node_data_start)
                                        {
                                            byte[] bufferDif = new byte[16];
                                            myFile.Seek(node_data_start - node_prog_start, SeekOrigin.Begin);
                                            int start_index = node_data_start;
                                            while (start_index < data_start && delete_config == 0)
                                            {
                                                int bytes = ((data_start - start_index) >= 16) ? 16 : (data_start - start_index);
                                                int readead = myFile.Read(bufferDif, 0, bytes);
                                                for (int i = 0; i < readead; i++)
                                                {
                                                    if (bufferDif[i] != 0xFF)
                                                    {
                                                        delete_config = 1;
                                                        break;
                                                    }
                                                }
                                                start_index += 16;
                                            }
                                        }
                                        else
                                        {
                                            data_start = node_data_start;
                                        }
                                    }
                                }
                            }
                            catch (Exception)
                            {

                            }
                            myFile.Close();

                        }

                        if (System.IO.File.Exists(path + "\\J-Link\\memory.bin"))
                        {
                            try
                            {
                                System.IO.File.Delete(path + "\\J-Link\\memory.bin");
                            }
                            catch (System.IO.IOException e)
                            {
                                MessageBox.Show(e.Message);
                            }
                        }
                    }
                    break;

                default:
                    break;

            }
            return dev;
        }

        
        private void erase_ST()
        {
            Device dev = Devices.ElementAt(DeviceSelectedIndex);

            if (dev.name == "BU-C1")
            {
                dev = C1_CheckNodeBootloader(dev);
            }

            int addr_prog = Convert.ToInt32(dev.program_start, 16);
            int addr_data = Convert.ToInt32(dev.data_start, 16);

            int sectors = (addr_data - addr_prog) / 2048;
            string cmd2 = "\"" + path.Replace("\\", "/") + "/ST-LINK Utility/ST-LINK_CLI.exe\" -c SN=" + jtag_serial + "";

            if (checkEraseProgram.Checked && !checkEraseAll.Checked)
                cmd2 = cmd2 + " -SE 0 " +(sectors - 1) + " -Q";
            if (checkEraseAll.Checked)
                cmd2 = cmd2 + " -ME";

            if (checkSoftReset.Checked || checkHardReset.Checked || checkPowerReset.Checked || checkRun.Checked)
            {
                if (checkSoftReset.Checked)
                    cmd2 = cmd2 + " -Rst";
                if (checkHardReset.Checked)
                    cmd2 = cmd2 + " -HardRst";
            }
            if (checkEraseProgram.Checked || checkEraseAll.Checked)
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 0;
                    label11.Text = "Status: Erasing memory...";
                    label11.Refresh();
                });
                ExecuteCommandSync(cmd2);
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 100;
                });
                if (label11.Text.Substring(8, 5) != "ERROR")
                {
                    this.Invoke((MethodInvoker)delegate()
                    {
                        pictureBox2.Visible = true;
                        label11.Text = "Status: Erase completed!";
                        label11.Refresh();
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 0;
                    label11.Text = "Status: No operation performed.";
                    label11.Refresh();
                });
            }
        }


        private void verify_ST()
        {
            if (firmwarePath == "")
            {
                MessageBox.Show("Please select a file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Device dev = Devices.ElementAt(DeviceSelectedIndex);

            if (dev.name == "BU-C1")
            {
                dev = C1_CheckFileBootloader(dev);
            }

            int addr_prog = Convert.ToInt32(dev.program_start, 16);
            int addr_data = Convert.ToInt32(dev.data_start, 16);

            string cmd2 = "\"" + path.Replace("\\", "/") + "/ST-LINK Utility/ST-LINK_CLI.exe\" -c SN=" + jtag_serial + " -CmpFile \"";

            if (checkVerifyProgram.Checked)
                cmd2 = cmd2 + firmwarePath + "\" 0x" + addr_prog.ToString("X");
            if (checkVerifyData.Checked && !checkVerifyProgram.Checked)
                cmd2 = cmd2 + firmwarePath + "\" 0x" + addr_data.ToString("X");

            if (checkSoftReset.Checked || checkHardReset.Checked || checkPowerReset.Checked || checkRun.Checked)
            {
                if (checkSoftReset.Checked)
                    cmd2 = cmd2 + " -Rst";
                if (checkHardReset.Checked)
                    cmd2 = cmd2 + " -HardRst";
            }

            cmd2 = cmd2 + " -Q";

            if (checkVerifyProgram.Checked || checkVerifyData.Checked)
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 0;
                    label11.Text = "Status: Verifying...";
                    label11.Refresh();
                });
                ExecuteCommandSync(cmd2);
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 100;
                });
                if (label11.Text.Substring(8, 5) != "ERROR")
                {
                    this.Invoke((MethodInvoker)delegate()
                    {
                        pictureBox2.Visible = true;
                        label11.Text = "Status: Verify completed!";
                        label11.Refresh();
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 0;
                    label11.Text = "Status: No operation performed.";
                    label11.Refresh();
                });
            }
        }


        private void write_ST()
        {
            if (firmwarePath == "")
            {
                MessageBox.Show("Please select a file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            this.Invoke((MethodInvoker)delegate()
            {
                progressBar1.Value = 0;
                label11.Text = "Status: Writing file in memory...";
                label11.Refresh();
            });
            Device dev = Devices.ElementAt(DeviceSelectedIndex);

            if (dev.name == "BU-C1")
            {
                dev = C1_CheckFileBootloader(dev);
            }

            int addr_prog = Convert.ToInt32(dev.program_start, 16);
            int addr_data = Convert.ToInt32(dev.data_start, 16);

            string cmd2 = "\"" + path.Replace("\\", "/") + "/ST-LINK Utility/ST-LINK_CLI.exe\" -c SN=" + jtag_serial + " -P \"" + firmwarePath + "\"";

            if (checkWriteProgram.Checked)
                cmd2 = cmd2 + " 0x" + addr_prog.ToString("X") + " -V -Q";
            if (checkWriteData.Checked && !checkWriteProgram.Checked)
                cmd2 = cmd2 + " 0x" + addr_data.ToString("X") + " -V -Q";

            if (checkSoftReset.Checked || checkHardReset.Checked || checkPowerReset.Checked || checkRun.Checked)
            {
                if (checkSoftReset.Checked)
                    cmd2 = cmd2 + " -Rst";
                if (checkHardReset.Checked)
                    cmd2 = cmd2 + " -HardRst";
            }

            if (checkWriteProgram.Checked )
            {
                ExecuteCommandSync(cmd2);
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 100;
                });
                if (label11.Text.Substring(8, 5) != "ERROR")
                {
                    this.Invoke((MethodInvoker)delegate()
                    {
                        pictureBox2.Visible = true;
                        label11.Text = "Status: Write completed!";
                        label11.Refresh();
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 100;
                    label11.Text = "Status: No operation performed.";
                    label11.Refresh();
                });
            }
        }


        private void read_ST()
        {
            Device dev = Devices.ElementAt(DeviceSelectedIndex);

            if (dev.name == "BU-C1")
            {
                dev = C1_CheckNodeBootloader(dev);
            }

            int prog_start = Convert.ToInt32(dev.program_start, 16);
            int data_start = Convert.ToInt32(dev.data_start, 16);
            int data_end = Convert.ToInt32(dev.data_end, 16);
            int prog_end = Convert.ToInt32(dev.program_end, 16);
            int addr_init = prog_start, addr_end = data_end;

            string cmd2 = "\"" + path.Replace("\\", "/") + "/ST-LINK Utility/ST-LINK_CLI.exe\" -c SN=" + jtag_serial + " -Dump ";
            
            if (checkReadProgram.Checked && checkReadData.Checked)
            {
                cmd2 = cmd2 + "0x" + prog_start.ToString("X") + " 0x" + (data_end - prog_start + 1).ToString("X") + " \"" + path.Replace("\\", "/") + "/memory.bin\" -Q";
                addr_init = prog_start;
                addr_end = data_end;
            }
            else
            {
                if (checkReadProgram.Checked)
                {
                    cmd2 = cmd2 + "0x" + prog_start.ToString("X") + " 0x" + (prog_end - prog_start + 1).ToString("X") + " \"" + path.Replace("\\", "/") + "/memory.bin\" -Q";
                    addr_init = prog_start;
                    addr_end = prog_end;
                }
                if (checkReadData.Checked)
                {
                    cmd2 = cmd2 + "0x" + data_start.ToString("X") + " 0x" + (data_end - data_start + 1).ToString("X") + " \"" + path.Replace("\\", "/") + "/memory.bin\" -Q";
                    addr_init = data_start;
                    addr_end = data_end;
                }
            }

            if (checkSoftReset.Checked || checkHardReset.Checked || checkPowerReset.Checked || checkRun.Checked)
            {
                if (checkSoftReset.Checked)
                    cmd2 = cmd2 + " -Rst";
                if (checkHardReset.Checked)
                    cmd2 = cmd2 + " -HardRst";
            }

            if (checkReadProgram.Checked || checkReadData.Checked)
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 0;
                    label11.Text = "Status: Reading memory...";
                    label11.Refresh();
                });
                Console.WriteLine(cmd2);
                ExecuteCommandSync(cmd2);
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 100;
                });
                if (label11.Text.Substring(8, 5) != "ERROR")
                {
                    disp_mem_content_st(addr_init, addr_end);
                    this.Invoke((MethodInvoker)delegate()
                    {
                        pictureBox2.Visible = true;
                        label11.Text = "Status: Reading completed!";
                        label11.Refresh();
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 100;
                    label11.Text = "Status: No operation performed.";
                    label11.Refresh();
                });
            }

        }

        private bool Is_old_firmware()
        {
            bool res = false;
            System.Text.StringBuilder sb_firm = new System.Text.StringBuilder();
            string line;

            System.IO.StreamReader mainFile = new System.IO.StreamReader(firmwarePath);
            while ((line = mainFile.ReadLine()) != null)
            {
                if (line.StartsWith("@44400") || line.StartsWith("@445B0"))
                {
                    res = true;
                    break;
                }
                else
                {
                    if (line.StartsWith("q"))
                        break;
                }
            }
            mainFile.Close();

            if (res)
            {
                Debug.WriteLine("The Firmware contains old boot init. Range 0x42000-0x45BFF will erased.");
                log += "\nThe Firmware contains old boot init. Range 0x42000-0x45BFF will erased.\n";
            }
            return res;
        }

        private void easy_olimex(bool previus_old_firmware = false)
        {
            if (firmwarePath == "")
            {
                MessageBox.Show("Please select a file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Device temp = Devices.ElementAt(DeviceSelectedIndex);
            int addr_prog = Convert.ToInt32(temp.program_start, 16);
            int addr_data = Convert.ToInt32(temp.data_start, 16);
            string cmd1, cmd2;

            bool old_firmware = true; 

            if (device == TypeDevice.TE)
            {
                cmd1 = "\"" + path.Replace("\\", "/") + "/Olimex/mspprog-cli-v2.exe\" /p=" + jtag_port + " /d=\"" + temp.name + "\" /cal=0x" + temp.data_start + ":0x" + temp.data_end + " /ec /w /v /f=\"" + firmwarePath + "\" /post=hr";
            }
            else if (device == TypeDevice.Wpan)
            {
                old_firmware = Is_old_firmware();
                if (old_firmware || previus_old_firmware == true)
                {
                    cmd1 = "\"" + path.Replace("\\", "/") + "/Olimex/mspprog-cli-v2.exe\" /p=" + jtag_port + " /d=" + temp.name + " /ec /w /v /f=\"" + firmwarePath + "\" /post=hr";
                }
                else
                {
                    cmd1 = "\"" + path.Replace("\\", "/") + "/Olimex/mspprog-cli-v2.exe\" /p=" + jtag_port + " /d=" + temp.name + " /cal=0x42000:0x45BFF /ec /w /v /f=\"" + firmwarePath + "\" /post=hr";
                }
            }
            else if (device == TypeDevice.WTDrop)
            {
                cmd1 = "\"" + path.Replace("\\", "/") + "/Olimex/mspprog-cli-v2.exe\" /p=" + jtag_port + " /d=" + temp.name + " /cal=0x22000:0x25BFF /ec /w /v /f=\"" + firmwarePath + "\" /post=hr";
            }
            else
            {
                cmd1 = "\"" + path.Replace("\\", "/") + "/Olimex/mspprog-cli-v2.exe\" /p=" + jtag_port + " /d=" + temp.name + " /eall /w /v /f=\"" + firmwarePath + "\" /post=hr";
            }

            cmd2 = "\"" + path.Replace("\\", "/") + "/Olimex/mspprog-cli-v2.exe\" /p=" + jtag_port + " /d=\"" + temp.name + "\" /rc /ri /f=\"" + path.Replace("\\", "/") + "/memory.txt\" /post=hr";

            this.Invoke((MethodInvoker)delegate()
            {
                progressBar1.Value = 0;
            });

            if (previus_old_firmware == false)
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    label11.Text = "Status: Erasing, Writing, Verifying and Running...";
                    label11.Refresh();
                });
            }
            ExecuteCommandSync(cmd1);
            if (label11.Text.Substring(8, 5) != "ERROR")
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 50;
                    pictureBox2.Visible = false;
                    pictureBox4.Visible = false;
                });
                ExecuteCommandSync(cmd2);
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 100;
                });
                if (label11.Text.Substring(8, 5) != "ERROR")
                {
                    bool new_firmware_WPAN = true;

                    if (previus_old_firmware || old_firmware)
                        new_firmware_WPAN = false;

                    bool flag = ((device == TypeDevice.Wpan) ? true : false) && new_firmware_WPAN;

                    disp_mem_content_olimex(addr_prog, addr_data, flag);

                    this.Invoke((MethodInvoker)delegate()
                    {
                        pictureBox2.Visible = true;
                        label11.Text = "Status: Completed!";
                        label11.Refresh();
                    });
                }
            }
            else 
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 100;
                });
            }
        }

        private void easy_ST()
        {
            if (firmwarePath == "")
            {
                MessageBox.Show("Please select a file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Device dev = Devices.ElementAt(DeviceSelectedIndex);

            if (dev.name == "BU-C1")
            {
                dev = C1_CheckFileBootloader(dev);
            }

            int delete_config = 0;

            int prog_start = Convert.ToInt32(dev.program_start, 16);
            int prog_end = Convert.ToInt32(dev.program_end, 16);
            int data_start = Convert.ToInt32(dev.data_start, 16);
            int data_end = Convert.ToInt32(dev.data_end, 16);
            string cmd0, cmd1, cmd2, cmd3, cmd4;

            this.Invoke((MethodInvoker)delegate ()
            {
                progressBar1.Value = 0;
            });


            this.Invoke((MethodInvoker)delegate ()
            {
                label11.Text = "Status: Erasing, Writing, Verifying and Running...";
                label11.Refresh();
            });

            if (dev.name == "BU-C1")
            {
                C1_CheckNodeDataStart(dev, ref delete_config, ref data_start);
            }


            cmd0 = "\"" + path.Replace("\\", "/") + "/ST-LINK Utility/ST-LINK_CLI.exe\" -c SN=" + jtag_serial + " -Dump 0x" + data_start.ToString("X") + " 0x" + (data_end - data_start + 1).ToString("X") + " \"" + path.Replace("\\", "/") + "/memory.bin\" -Q";

            cmd1 = "\"" + path.Replace("\\", "/") + "/ST-LINK Utility/ST-LINK_CLI.exe\" -c SN=" + jtag_serial + " -ME -Q";

            cmd2 = "\"" + path.Replace("\\", "/") + "/ST-LINK Utility/ST-LINK_CLI.exe\" -c SN=" + jtag_serial + " -P \"" + firmwarePath + "\" 0x" + prog_start.ToString("X") + " -V -Q";

            cmd3 = "\"" + path.Replace("\\", "/") + "/ST-LINK Utility/ST-LINK_CLI.exe\" -c SN=" + jtag_serial + " -P \"" + path.Replace("\\", "/") + "/memory.bin\" 0x" + data_start.ToString("X") + " -V -Q";

            cmd4 = "\"" + path.Replace("\\", "/") + "/ST-LINK Utility/ST-LINK_CLI.exe\" -c SN=" + jtag_serial + " -Dump 0x" + prog_start.ToString("X") + " 0x" + (data_end - prog_start + 1).ToString("X") + " \"" + path.Replace("\\", "/") + "/memory.bin\" -Q -HardRst -Run";



            if (System.IO.File.Exists(path + "\\memory.bin"))
            {
                try
                {
                    System.IO.File.Delete(path + "\\memory.bin");
                }
                catch (System.IO.IOException e)
                {
                    MessageBox.Show(e.Message);
                }
            }

            if (label11.Text.Substring(8, 5) != "ERROR")
            {
                if (data_end > data_start)
                {
                    ExecuteCommandSync(cmd0);
                }
                if (label11.Text.Substring(8, 5) != "ERROR")
                {
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        progressBar1.Value = 20;
                        pictureBox2.Visible = false;
                        pictureBox4.Visible = false;
                    });
                    ExecuteCommandSync(cmd1);
                    if (label11.Text.Substring(8, 5) != "ERROR")
                    {
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            progressBar1.Value = 40;
                        });
                        ExecuteCommandSync(cmd2);
                        if (label11.Text.Substring(8, 5) != "ERROR")
                        {
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                progressBar1.Value = 60;
                            });
                            if ((data_end > data_start) && delete_config == 0)
                            {
                                ExecuteCommandSync(cmd3);
                            }
                            
                            if (label11.Text.Substring(8, 5) != "ERROR")
                            {
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    progressBar1.Value = 80;
                                });
                                if (data_end > data_start)
                                {
                                    if (System.IO.File.Exists(path + "\\memory.bin"))
                                    {
                                        try
                                        {
                                            System.IO.File.Delete(path + "\\memory.bin");
                                        }
                                        catch (System.IO.IOException e)
                                        {
                                            MessageBox.Show(e.Message);
                                        }
                                    }
                                }

                                ExecuteCommandSync(cmd4);
                                if (label11.Text.Substring(8, 5) != "ERROR")
                                {
                                    disp_mem_content_st(prog_start, data_end);
                                }
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    progressBar1.Value = 100;
                                });

                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    pictureBox2.Visible = true;
                                    label11.Text = "Status: Completed!";
                                    label11.Refresh();
                                });

                                if (delete_config == 1)
                                {
                                    MessageBox.Show("The configuration on the node is not compatible with the new firmware flashed.\nThe configuration was deleted and the node must reconfigured.\nAny custom XBee configurations also was deleted.\nThis may cause the node not to synchronize with other nodes if they have different parameters.", "Configuration Deleted", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                }
                            }
                            else
                            {
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    progressBar1.Value = 100;
                                });
                            }
                        }
                        else
                        {
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                progressBar1.Value = 100;
                            });
                        }
                    }
                    else
                    {
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            progressBar1.Value = 100;
                        });
                    }
                }
                else
                {
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        progressBar1.Value = 100;
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 100;
                });
            }
        }

        private void read_TI()
        {
            Device temp = Devices.ElementAt(DeviceSelectedIndex);
            int addr_prog = Convert.ToInt32(temp.program_start, 16);
            int addr_data = Convert.ToInt32(temp.data_start, 16);
            bool read_prog_memory = false;
            bool read_data_memory = false;

            string cmd0 = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -s";
            string resultado = ExecuteCommandSync(cmd0,2200);

            if (label11.Text.Substring(8, 5) != "ERROR")
            {
                int c = resultado.IndexOf(": MSP430F");
                string name_detected = "";
                if (c > 0)
                {
                    int enter = resultado.IndexOf("\n", c);
                    name_detected = resultado.Substring(c + 2, enter - c - 2);
                }
                string name = "";

                bool OK = false;

                if (device == TypeDevice.TE)
                {
                    name = "MSP430F235";
                    if (name_detected == "MSP430F235" || name_detected == "MSP430F247")
                    {                       
                        OK = true;
                    }
                    if (name_detected == "MSP430F247")
                    {
                        name = name_detected;
                        addr_prog = 0x8000;
                    }
                }
                else if (device == TypeDevice.Wpan)
                {
                    name = "MSP430F5438A";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                    else if (name_detected == "MSP430F5438")
                    {
                        switch (MessageBox.Show("Device detected is MSP430F5438. Are you sure continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
                        {
                            case DialogResult.Yes:
                                name = name_detected;
                                OK = true;
                                break;
                            default:
                                break;
                        }
                    }
                }
                else if (device == TypeDevice.WTDrop)
                {
                    name = "MSP430F5419";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                    else if (name_detected == "MSP430F5419A")
                    {
                        switch (MessageBox.Show("Device detected is MSP430F5419A. Are you sure continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
                        {
                            case DialogResult.Yes:
                                OK = true;
                                name = name_detected;
                                break;
                            default:
                                break;
                        }
                    }
                }
                else
                {
                    name = "MSP430F5419";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                }

                if(OK)
                {
                    string cmd = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -n " + name + " -s";

                    

                    string cmd1 = cmd + " -r [memoryTI_MAIN.txt,MAIN]";
                    string cmd2 = cmd + " -r [memoryTI_INFO.txt,INFO]";

                    if (checkHardReset.Checked || checkRun.Checked)
                    {
                        cmd2 = cmd2 + " -z ";
                        if (checkHardReset.Checked && checkRun.Checked)
                            cmd2 = cmd2 + "[RESET,VCC]";
                        if (checkRun.Checked && !checkHardReset.Checked)
                            cmd2 = cmd2 + "[VCC]";
                        if (!checkRun.Checked && checkHardReset.Checked)
                            cmd2 = cmd2 + "[RESET]";
                    }

                    if (checkReadProgram.Checked)
                    {
                        read_prog_memory = true;
                    }

                    if (checkReadData.Checked)
                    {
                        read_data_memory = true;
                    }

                    if (read_prog_memory)
                    {
                        this.Invoke((MethodInvoker)delegate()
                        {
                            progressBar1.Value = 0;
                            label11.Text = "Status: Reading program memory...";
                            label11.Refresh();
                        });
                        ExecuteCommandSync(cmd1);
                        if (label11.Text.Substring(8, 5) != "ERROR")
                        {
                            if (read_data_memory)
                            {
                                this.Invoke((MethodInvoker)delegate()
                                {
                                    progressBar1.Value = 50;
                                    pictureBox2.Visible = false;
                                    pictureBox4.Visible = true;
                                    label11.Text = "Status: Reading data memory...";
                                    label11.Refresh();
                                });
                                ExecuteCommandSync(cmd2);

                                this.Invoke((MethodInvoker)delegate()
                                {
                                    progressBar1.Value = 100;
                                });

                                if (label11.Text.Substring(8, 5) != "ERROR")
                                {
                                    disp_mem_content_TI(read_prog_memory, read_data_memory, addr_prog, addr_data);
                                    this.Invoke((MethodInvoker)delegate ()
                                    {
                                        pictureBox2.Visible = true;
                                        pictureBox3.Visible = false;
                                        pictureBox4.Visible = false;
                                        label11.Text = "Status: Reading completed!";
                                        label11.Refresh();
                                    });
                                }
                                else 
                                {
                                    this.Invoke((MethodInvoker)delegate ()
                                    {
                                        progressBar1.Value = 100;
                                        pictureBox3.Visible = true;
                                        pictureBox2.Visible = false;
                                        pictureBox4.Visible = false;
                                    });
                                }
                            }
                            else
                            {
                                disp_mem_content_TI(read_prog_memory, read_data_memory, addr_prog, addr_data);                               
                                this.Invoke((MethodInvoker)delegate()
                                {
                                    progressBar1.Value = 100;
                                    pictureBox2.Visible = true;
                                    pictureBox3.Visible = false;
                                    pictureBox4.Visible = false;
                                    label11.Text = "Status: Reading completed!";
                                    label11.Refresh();
                                });
                            }
                        }
                        else 
                        {
                            this.Invoke((MethodInvoker)delegate()
                            {
                                progressBar1.Value = 100;
                                pictureBox3.Visible = true;
                                pictureBox2.Visible = false;
                                pictureBox4.Visible = false;
                            });
                        }
                    }
                    else
                    {
                        if (read_data_memory)
                        {
                            this.Invoke((MethodInvoker)delegate()
                            {
                                progressBar1.Value = 0;
                                label11.Text = "Status: Reading data memory...";
                                label11.Refresh();
                            });
                            ExecuteCommandSync(cmd2);
                            this.Invoke((MethodInvoker)delegate()
                            {
                                progressBar1.Value = 100;
                            });
                            if (label11.Text.Substring(8, 5) != "ERROR")
                            {
                                disp_mem_content_TI(read_prog_memory, read_data_memory, addr_prog, addr_data);
                                this.Invoke((MethodInvoker)delegate()
                                {
                                    pictureBox2.Visible = true;
                                    pictureBox3.Visible = false;
                                    pictureBox4.Visible = false;
                                    label11.Text = "Status: Reading completed!";
                                    label11.Refresh();
                                });
                            }
                            else
                            {
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    progressBar1.Value = 100;
                                    pictureBox3.Visible = true;
                                    pictureBox2.Visible = false;
                                    pictureBox4.Visible = false;
                                });
                            }
                        }
                    }
                }
                else
                {
                    this.Invoke((MethodInvoker)delegate()
                    {
                        MessageBox.Show("Found device does not match", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        progressBar1.Value = 100;
                        label11.Text = "Status: ERROR";
                        pictureBox3.Visible = true;
                        pictureBox2.Visible = false;
                        pictureBox4.Visible = false;
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 100;
                });
            }
        }


        private void write_TI()
        {
            if (firmwarePath == "")
            {
                MessageBox.Show("Please select a file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Device temp = Devices.ElementAt(DeviceSelectedIndex);


            string cmd0 = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -s";
            string resultado = ExecuteCommandSync(cmd0, 2200);

            if (label11.Text.Substring(8, 5) != "ERROR")
            {
                int c = resultado.IndexOf(": MSP430F");
                string name_detected = "";
                if (c > 0)
                {
                    int enter = resultado.IndexOf("\n", c);
                    name_detected = resultado.Substring(c + 2, enter - c - 2);
                }
                string name = "";

                bool OK = false;

                if (device == TypeDevice.TE)
                {
                    name = "MSP430F235";
                    if (name_detected == "MSP430F235" || name_detected == "MSP430F247")
                    {
                        name = name_detected;
                        OK = true;
                    }
                }
                else if (device == TypeDevice.Wpan)
                {
                    name = "MSP430F5438A";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                    else if (name_detected == "MSP430F5438")
                    {
                        switch (MessageBox.Show("Device detected is MSP430F5438. Are you sure continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
                        {
                            case DialogResult.Yes:
                                name = name_detected;
                                OK = true;
                                break;
                            default:
                                break;
                        }
                    }
                }
                else if (device == TypeDevice.WTDrop)
                {
                    name = "MSP430F5419";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                    else if (name_detected == "MSP430F5419A")
                    {
                        switch (MessageBox.Show("Device detected is MSP430F5419A. Are you sure continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
                        {
                            case DialogResult.Yes:
                                OK = true;
                                name = name_detected;
                                break;
                            default:
                                break;
                        }
                    }
                }
                else
                {
                    name = "MSP430F5419";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                }

                if (OK)
                {
                    string cmd = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -n " + name + " -w \"" + firmwarePath + "\"" + " -v -e ERASE_MAIN" + " -s";

                    if (checkHardReset.Checked || checkRun.Checked)
                    {
                        cmd = cmd + " -z ";
                        if (checkHardReset.Checked && checkRun.Checked)
                            cmd = cmd + "[RESET,VCC]";
                        if (checkRun.Checked && !checkHardReset.Checked)
                            cmd = cmd + "[VCC]";
                        if (!checkRun.Checked && checkHardReset.Checked)
                            cmd = cmd + "[RESET]";
                    }

                    if (checkWriteData.Checked && !checkWriteProgram.Checked)
                    {
                        cmd = cmd + " -u";
                    }
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        progressBar1.Value = 0;
                        label11.Text = "Status: Writing file in memory...";
                        label11.Refresh();
                    });
                    ExecuteCommandSync(cmd);
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        progressBar1.Value = 100;
                    });
                    if (label11.Text.Substring(8, 5) != "ERROR")
                    {
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            pictureBox2.Visible = true;
                            pictureBox3.Visible = false;
                            pictureBox4.Visible = false;
                            label11.Text = "Status: Write completed!";
                            label11.Refresh();
                        });
                    }
                }
                else
                {
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        MessageBox.Show("Found device does not match", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        progressBar1.Value = 100;
                        label11.Text = "Status: ERROR";
                        pictureBox3.Visible = true;
                        pictureBox2.Visible = false;
                        pictureBox4.Visible = false;
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 100;
                    pictureBox2.Visible = false;
                    pictureBox3.Visible = true;
                    pictureBox4.Visible = false;
                });
            }
        }

        private void erase_TI()
        {
            if (!checkEraseProgram.Checked && !checkEraseAll.Checked)
                return;

            Device temp = Devices.ElementAt(DeviceSelectedIndex);


            string cmd0 = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -s";
            string resultado = ExecuteCommandSync(cmd0, 2200);

            if (label11.Text.Substring(8, 5) != "ERROR")
            {
                int c = resultado.IndexOf(": MSP430F");
                string name_detected = "";
                if (c > 0)
                {
                    int enter = resultado.IndexOf("\n", c);
                    name_detected = resultado.Substring(c + 2, enter - c - 2);
                }
                string name = "";

                bool OK = false;

                if (device == TypeDevice.TE)
                {
                    name = "MSP430F235";
                    if (name_detected == "MSP430F235" || name_detected == "MSP430F247")
                    {
                        name = name_detected;
                        OK = true;
                    }
                }
                else if (device == TypeDevice.Wpan)
                {
                    name = "MSP430F5438A";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                    else if (name_detected == "MSP430F5438")
                    {
                        switch (MessageBox.Show("Device detected is MSP430F5438. Are you sure continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
                        {
                            case DialogResult.Yes:
                                name = name_detected;
                                OK = true;
                                break;
                            default:
                                break;
                        }
                    }
                }
                else if (device == TypeDevice.WTDrop)
                {
                    name = "MSP430F5419";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                    else if (name_detected == "MSP430F5419A")
                    {
                        switch (MessageBox.Show("Device detected is MSP430F5419A. Are you sure continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
                        {
                            case DialogResult.Yes:
                                OK = true;
                                name = name_detected;
                                break;
                            default:
                                break;
                        }
                    }
                }
                else
                {
                    name = "MSP430F5419";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                }

                if (OK)
                {
                    string cmd = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -n " + name;

                    if (checkEraseProgram.Checked && !checkEraseAll.Checked)
                        cmd = cmd + " -e ERASE_MAIN -s";
                    if (checkEraseAll.Checked)
                        cmd = cmd + " -e ERASE_ALL -s -u";



                    if (checkHardReset.Checked || checkRun.Checked)
                    {
                        cmd = cmd + " -z ";
                        if (checkHardReset.Checked && checkRun.Checked)
                            cmd = cmd + "[RESET,VCC]";
                        if (checkRun.Checked && !checkHardReset.Checked)
                            cmd = cmd + "[VCC]";
                        if (!checkRun.Checked && checkHardReset.Checked)
                            cmd = cmd + "[RESET]";
                    }

                    this.Invoke((MethodInvoker)delegate ()
                    {
                        progressBar1.Value = 0;
                        label11.Text = "Status: Writing file in memory...";
                        label11.Refresh();
                    });
                    ExecuteCommandSync(cmd);
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        progressBar1.Value = 100;
                    });
                    if (label11.Text.Substring(8, 5) != "ERROR")
                    {
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            pictureBox2.Visible = true;
                            pictureBox3.Visible = false;
                            pictureBox4.Visible = false;
                            label11.Text = "Status: Write completed!";
                            label11.Refresh();
                        });
                    }
                }
                else
                {
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        MessageBox.Show("Found device does not match", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        progressBar1.Value = 100;
                        label11.Text = "Status: ERROR";
                        pictureBox3.Visible = true;
                        pictureBox2.Visible = false;
                        pictureBox4.Visible = false;
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 100;
                    pictureBox2.Visible = false;
                    pictureBox3.Visible = true;
                    pictureBox4.Visible = false;
                });
            }
        }

        private void verify_TI()
        {
            if (firmwarePath == "")
            {
                MessageBox.Show("Please select a file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Device temp = Devices.ElementAt(DeviceSelectedIndex);

            string cmd0 = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -s";
            string resultado = ExecuteCommandSync(cmd0, 2200);

            if (label11.Text.Substring(8, 5) != "ERROR")
            {
                int c = resultado.IndexOf(": MSP430F");
                string name_detected = "";
                if (c > 0)
                {
                    int enter = resultado.IndexOf("\n", c);
                    name_detected = resultado.Substring(c + 2, enter - c - 2);
                }
                string name = "";

                bool OK = false;

                if (device == TypeDevice.TE)
                {
                    name = "MSP430F235";
                    if (name_detected == "MSP430F235" || name_detected == "MSP430F247")
                    {
                        name = name_detected;
                        OK = true;
                    }
                }
                else if (device == TypeDevice.Wpan)
                {
                    name = "MSP430F5438A";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                    else if (name_detected == "MSP430F5438")
                    {
                        switch (MessageBox.Show("Device detected is MSP430F5438. Are you sure continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
                        {
                            case DialogResult.Yes:
                                name = name_detected;
                                OK = true;
                                break;
                            default:
                                break;
                        }
                    }
                }
                else if (device == TypeDevice.WTDrop)
                {
                    name = "MSP430F5419";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                    else if (name_detected == "MSP430F5419A")
                    {
                        switch (MessageBox.Show("Device detected is MSP430F5419A. Are you sure continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
                        {
                            case DialogResult.Yes:
                                OK = true;
                                name = name_detected;
                                break;
                            default:
                                break;
                        }
                    }
                }
                else
                {
                    name = "MSP430F5419";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                }

                if (OK)
                {
                    string cmd = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -n " + name + " -v \"" + firmwarePath + "\"" + " -s";
                    if (checkHardReset.Checked || checkRun.Checked)
                    {
                        cmd = cmd + " -z ";
                        if (checkHardReset.Checked && checkRun.Checked)
                            cmd = cmd + "[RESET,VCC]";
                        if (checkRun.Checked && !checkHardReset.Checked)
                            cmd = cmd + "[VCC]";
                        if (!checkRun.Checked && checkHardReset.Checked)
                            cmd = cmd + "[RESET]";
                    }
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        progressBar1.Value = 0;
                        label11.Text = "Status: Verifying...";
                        label11.Refresh();
                    });
                    ExecuteCommandSync(cmd);
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        progressBar1.Value = 100;
                    });
                    if (label11.Text.Substring(8, 5) != "ERROR")
                    {
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            pictureBox2.Visible = true;
                            pictureBox3.Visible = false;
                            pictureBox4.Visible = false;
                            label11.Text = "Status: Verify completed!";
                            label11.Refresh();
                        });
                    }
                }
                else
                {
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        MessageBox.Show("Found device does not match", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        progressBar1.Value = 100;
                        label11.Text = "Status: ERROR";
                        pictureBox3.Visible = true;
                        pictureBox2.Visible = false;
                        pictureBox4.Visible = false;
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 100;
                    pictureBox2.Visible = false;
                    pictureBox3.Visible = true;
                    pictureBox4.Visible = false;
                });
            }
        }

        private void easy_TI(bool previus_old_firmware = false)
        {
            if (firmwarePath == "")
            {
                MessageBox.Show("Please select a file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Device temp = Devices.ElementAt(DeviceSelectedIndex);
            int addr_prog = Convert.ToInt32(temp.program_start, 16);
            int addr_data = Convert.ToInt32(temp.data_start, 16);
            string cmd0, cmd1, cmd2, cmd3, cmd4 = "", cmd5 = "";

            cmd0 = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -s";
            string resultado = ExecuteCommandSync(cmd0,2200);

            if (label11.Text.Substring(8, 5) != "ERROR")
            {
                int c = resultado.IndexOf(": MSP430F");
                string name_detected = "";
                if (c > 0)
                {
                    int enter = resultado.IndexOf("\n", c);
                    name_detected = resultado.Substring(c + 2, enter - c - 2);
                }
                string name = "";

                bool old_firmware = true;
                bool OK = false;
                if (device == TypeDevice.TE)
                {
                    name = "MSP430F235";
                    if (name_detected == "MSP430F235" || name_detected == "MSP430F247")
                    {
                        OK = true;
                    }
                    if (name_detected == "MSP430F247")
                    {
                        name = name_detected;
                        addr_prog = 0x8000;
                    }
                    cmd1 = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -n " + name + " -j fast -w " + "\"" + firmwarePath + "\"" + " -v -s -e ERASE_MAIN -z [RESET,VCC]";
                }
                else if (device == TypeDevice.Wpan)
                {
                    name = "MSP430F5438A";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                    else if (name_detected == "MSP430F5438")
                    {
                        switch (MessageBox.Show("Device detected is MSP430F5438. Are you sure continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
                        {
                            case DialogResult.Yes:
                                name = name_detected;
                                OK = true;
                                break;
                            default:
                                break;
                        }
                    }

                    old_firmware = Is_old_firmware();
                    if (old_firmware || previus_old_firmware == true)
                    {
                        cmd1 = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -n " + name + " -j fast -u -w " + "\"" + firmwarePath + "\"" + " -v -s -e ERASE_ALL";
                    }
                    else
                    {
                        cmd1 = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -n " + name + " -j fast -u -w " + "\"" + firmwarePath + "\"" + " -v -s -e ERASE_MAIN";
                    }
                    cmd4 = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -n " + name + " -j fast -s -r [memoryTI_CONF.txt,0x42000-0x45BFF]";
                    cmd5 = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -n " + name + " -j fast -u -w " + "\"memoryTI_CONF.txt\"" + " -v -s -e ERASE_SEGMENT";
                }
                else if (device == TypeDevice.WTDrop)
                {
                    name = "MSP430F5419";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                    else if (name_detected == "MSP430F5419A")
                    {
                        switch (MessageBox.Show("Device detected is MSP430F5419A. Are you sure continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
                        {
                            case DialogResult.Yes:
                                OK = true;
                                name = name_detected;
                                break;
                            default:
                                break;
                        }
                    }
                    cmd1 = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -n " + name + " -j fast -u -w " + "\"" + firmwarePath + "\"" + " -v -s -e ERASE_MAIN";
                    cmd4 = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -n " + name + " -j fast -s -r [memoryTI_CONF.txt,0x22000-0x25BFF]";
                    cmd5 = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -n " + name + " -j fast -u -w " + "\"memoryTI_CONF.txt\"" + " -v -s -e ERASE_SEGMENT";
                }
                else
                {
                    name = "MSP430F5419";
                    if (name_detected == name)
                    {
                        OK = true;
                    }
                    else
                    {

                    }
                    cmd1 = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -n " + name + " -j fast -u -w " + "\"" + firmwarePath + "\"" + " -v -s -e ERASE_ALL";
                }

                cmd2 = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -n " + name + " -j fast -s -r [memoryTI_MAIN.txt,MAIN]";
                cmd3 = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -n " + name + " -j fast -s -r [memoryTI_INFO.txt,INFO] -z [RESET,VCC]";

                if (OK)
                {
                    this.Invoke((MethodInvoker)delegate()
                    {
                        progressBar1.Value = 0;
                        label11.Text = "Status: Erasing, Writing, Verifying and Running...";
                        label11.Refresh();
                    });

                    if ((device == TypeDevice.Wpan || device == TypeDevice.WTDrop) && cmd4 != "")
                    {
                        ExecuteCommandSync(cmd4);
                    }

                    if (label11.Text.Substring(8, 5) != "ERROR")
                    {
                        ExecuteCommandSync(cmd1);

                        if (label11.Text.Substring(8, 5) != "ERROR")
                        {
                            this.Invoke((MethodInvoker)delegate()
                            {
                                progressBar1.Value = 40;
                                pictureBox2.Visible = false;
                                pictureBox4.Visible = false;
                            });

                            if ((device == TypeDevice.Wpan || device == TypeDevice.WTDrop) && cmd5 != "")
                            {
                                ExecuteCommandSync(cmd5);
                                if (System.IO.File.Exists(path + "\\memoryTI_CONF.txt"))
                                {
                                    try
                                    {
                                        System.IO.File.Delete(path + "\\memoryTI_CONF.txt");
                                    }
                                    catch (System.IO.IOException e)
                                    {
                                        MessageBox.Show(e.Message);
                                    }
                                }
                            }

                            if (label11.Text.Substring(8, 5) != "ERROR")
                            {
                                ExecuteCommandSync(cmd2);
                                if (label11.Text.Substring(8, 5) != "ERROR")
                                {
                                    this.Invoke((MethodInvoker)delegate()
                                    {
                                        progressBar1.Value = 80;
                                        pictureBox2.Visible = false;
                                        pictureBox4.Visible = false;
                                    });
                                    ExecuteCommandSync(cmd3);
                                    this.Invoke((MethodInvoker)delegate()
                                    {
                                        progressBar1.Value = 100;
                                    });
                                    if (label11.Text.Substring(8, 5) != "ERROR")
                                    {
                                        bool new_firmware_WPAN = true;

                                        if (previus_old_firmware || old_firmware)
                                            new_firmware_WPAN = false;

                                        bool flag = ((device == TypeDevice.Wpan) ? true : false) && new_firmware_WPAN;

                                        disp_mem_content_TI(true, true, addr_prog, addr_data, false);  //flag -> false
                                        this.Invoke((MethodInvoker)delegate()
                                        {
                                            label11.Text = "Status: Completed!";
                                            label11.Refresh();
                                            pictureBox2.Visible = true;
                                            pictureBox3.Visible = false;
                                            pictureBox4.Visible = false;
                                        });

                                    }
                                }
                                else
                                {
                                    this.Invoke((MethodInvoker)delegate()
                                    {
                                        progressBar1.Value = 100;
                                        pictureBox2.Visible = false;
                                        pictureBox3.Visible = true;
                                        pictureBox4.Visible = false;
                                    });
                                }
                            }
                            else
                            {
                                this.Invoke((MethodInvoker)delegate()
                                {
                                    progressBar1.Value = 100;
                                    pictureBox2.Visible = false;
                                    pictureBox3.Visible = true;
                                    pictureBox4.Visible = false;
                                });
                            }
                        }
                        else
                        {
                            this.Invoke((MethodInvoker)delegate()
                            {
                                progressBar1.Value = 100;
                                pictureBox2.Visible = false;
                                pictureBox3.Visible = true;
                                pictureBox4.Visible = false;
                            });
                        }
                    }
                    else
                    {
                        this.Invoke((MethodInvoker)delegate()
                        {
                            progressBar1.Value = 100;
                            pictureBox2.Visible = false;
                            pictureBox3.Visible = true;
                            pictureBox4.Visible = false;
                        });
                    }
                }
                else 
                {
                    this.Invoke((MethodInvoker)delegate()
                    {
                        MessageBox.Show("Found device does not match", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        progressBar1.Value = 100;
                        label11.Text = "Status: ERROR";
                        pictureBox3.Visible = true;
                        pictureBox2.Visible = false;
                        pictureBox4.Visible = false;
                    });
                }
            }
            else 
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 100;
                    pictureBox2.Visible = false;
                    pictureBox3.Visible = true;
                    pictureBox4.Visible = false;
                });
            }
        }

        private Device C1_CheckFileBootloader(Device dev)
        {
            int boot_version = 2;
            System.IO.FileStream myFile;
            try
            {
                myFile = new System.IO.FileStream(firmwarePath, FileMode.Open);
            }
            catch (System.IO.FileNotFoundException)
            {
                return dev;
            }

            try
            {
                byte[] buffer = new byte[4];
                myFile.Seek(0x5FF0, SeekOrigin.Begin);
                if (myFile.Read(buffer, 0, 4) != 0)
                {
                    boot_version = BitConverter.ToInt32(buffer, 0);
                }

                Device temp = Devices.Find(delegate (Device item)
                {
                    return dev.name == item.name && Convert.ToInt32(item.boot_version) == boot_version;
                });
                if (temp != null)
                {
                    dev = temp;
                    DeviceSelectedIndex = Devices.IndexOf(temp);
                }
            }
            catch (Exception)
            {

            }
            myFile.Close();
            return dev;
        }

        private int V1_CheckFileFLFS(Device dev)
        {
            int FLFS_version = 0;
            System.IO.FileStream myFile;
            try
            {
                myFile = new System.IO.FileStream(firmwarePath, FileMode.Open);
            }
            catch (System.IO.FileNotFoundException)
            {
                return FLFS_version;
            }

            try
            {
                byte[] buffer = new byte[4];

                int offset = 0;
                if (dev.name == "BU-V1")
                {
                    offset = 0x4104;
                }
                if (dev.name == "C1-LORA")
                {
                    offset = 0x10104;
                }

                myFile.Seek(offset, SeekOrigin.Begin);
                if (myFile.Read(buffer, 0, 4) != 0)
                {
                    FLFS_version = BitConverter.ToInt32(buffer, 0);
                }
            }
            catch (Exception)
            {

            }
            myFile.Close();
            return FLFS_version;
        }

        private int V1_CheckNodeFLFS(Device dev, ref int delete_config)
        {
            int current_FLFS_Version = 0;
            string Manufacturer = "";

            int prog_start = Convert.ToInt32(dev.program_start, 16);
            int prog_end = Convert.ToInt32(dev.program_end, 16);
            int data_end = Convert.ToInt32(dev.data_end, 16);

            string cmd0 = "\"" + path.Replace("\\", "/") + "/J-Link/JLink.exe\" -USB " + jtag_serial + " -If SWD -Speed 4000 -Device " + dev.type + " -autoconnect 1 -CommandFile \"";

            System.IO.StreamWriter CommandFile;

            CommandFile = new System.IO.StreamWriter(path + "\\J-Link\\CommandFile.jlink");
            CommandFile.WriteLine("SaveBin " + "\"" + path.Replace("\\", "/") + "/J-Link/memory.bin\"" + " 0x" + prog_start.ToString("X") + " 0x" + (data_end - prog_start + 1).ToString("X"));
            CommandFile.WriteLine("RSetType 2\n" +
                                    "Reset");
            CommandFile.WriteLine("Go");
            CommandFile.WriteLine("exit");
            CommandFile.Close();

            cmd0 = cmd0 + path.Replace("\\", "/") + "/J-Link/CommandFile.jlink" + "\"";

            ExecuteCommandSync(cmd0);

            if (label11.Text.Substring(8, 5) != "ERROR")
            {
                System.IO.FileStream myFile;
                try
                {
                    myFile = new System.IO.FileStream(path + "\\J-Link\\memory.bin", FileMode.Open);
                }
                catch (System.IO.FileNotFoundException)
                {
                    return current_FLFS_Version;
                }

                try
                {
                    byte[] buffer = new byte[16];
                    
                    int offset_Manufacturer = 0;
                    if (dev.name == "BU-V1")
                    {
                        offset_Manufacturer = 0x3FD0;
                    }
                    if (dev.name == "C1-LORA")
                    {
                        offset_Manufacturer = 0xFFD0;
                    }
                    myFile.Seek(offset_Manufacturer, SeekOrigin.Begin);

                    if (myFile.Read(buffer, 0, 16) != 0)
                    {
                        try
                        {
                            Manufacturer = Encoding.UTF8.GetString(buffer, 0, 8);
                        }
                        catch (Exception) { }
                    }

                    int offset_FHFS = 0;
                    if (dev.name == "BU-V1")
                    {
                        offset_FHFS = 0x4104;
                    }
                    if (dev.name == "C1-LORA")
                    {
                        offset_FHFS = 0x10104;
                    }
                    myFile.Seek(offset_FHFS, SeekOrigin.Begin);
                    if (myFile.Read(buffer, 0, 4) != 0)
                    {
                        current_FLFS_Version = BitConverter.ToInt32(buffer, 0);
                    }


                }
                catch (Exception)
                {

                }
                myFile.Close();

            }

            if (System.IO.File.Exists(path + "\\J-Link\\memory.bin"))
            {
                try
                {
                    System.IO.File.Delete(path + "\\J-Link\\memory.bin");
                }
                catch (System.IO.IOException e)
                {
                    MessageBox.Show(e.Message);
                }
            }

            return (Manufacturer.Equals("Wiseconn")) ? current_FLFS_Version : -2;
        }


        private void easy_JL()
        {
            if (firmwarePath == "")
            {
                MessageBox.Show("Please select a file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }


            Device dev = Devices.ElementAt(DeviceSelectedIndex);
            int new_FLFS_Version = 0, current_FLFS_Version = 0;
           
            if (dev.name == "BU-C1")
            {
                dev = C1_CheckFileBootloader(dev);
            }

            if (dev.name == "BU-V1" || dev.name == "C1-LORA")
            {
                new_FLFS_Version = V1_CheckFileFLFS(dev);
            }

            int delete_config = 0;

            int prog_start = Convert.ToInt32(dev.program_start, 16);
            int prog_end = Convert.ToInt32(dev.program_end, 16);
            int data_start = Convert.ToInt32(dev.data_start, 16);
            int data_end = Convert.ToInt32(dev.data_end, 16);
            string cmd1, cmd2, cmd4;

            this.Invoke((MethodInvoker)delegate ()
            {
                progressBar1.Value = 0;
            });
            this.Invoke((MethodInvoker)delegate ()
            {
                label11.Text = "Status: Erasing, Writing, Verifying and Running...";
                label11.Refresh();
            });

            if (System.IO.File.Exists(path + "\\J-Link\\memory.bin"))
            {
                try
                {
                    System.IO.File.Delete(path + "\\J-Link\\memory.bin");
                }
                catch (System.IO.IOException e)
                {
                    MessageBox.Show(e.Message);
                }
            }
            if (label11.Text.Substring(8, 5) != "ERROR")
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 20;
                    pictureBox2.Visible = false;
                    pictureBox4.Visible = false;
                });

                if (File.Exists(path + "\\J-Link\\CommandFile.jlink"))
                {
                    File.Delete(path + "\\J-Link\\CommandFile.jlink");
                }

                System.IO.StreamWriter CommandFile;

                if (dev.name == "BU-C1")
                {
                    C1_CheckNodeDataStart(dev, ref delete_config, ref data_start);
                }

                if (dev.name == "BU-V1" || dev.name == "C1-LORA")
                {
                    current_FLFS_Version = V1_CheckNodeFLFS(dev, ref delete_config);
                    log = log + "\n" + "FLFS version - current: " + current_FLFS_Version + "   new: " + new_FLFS_Version + "\n";
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        richTextBox1.Text = log;
                    });
                    if (new_FLFS_Version > current_FLFS_Version)
                    {
                        delete_config = 1;
                    }
                }

                if (label11.Text.Substring(8, 5) != "ERROR")
                {
                    cmd1 = "\"" + path.Replace("\\", "/") + "/J-Link/JLink.exe\" -USB "+ jtag_serial+ " -If SWD -Speed 4000 -Device " + dev.type + " -autoconnect 1 -CommandFile \"";

                    CommandFile = new System.IO.StreamWriter(path + "\\J-Link\\CommandFile.jlink");
                    CommandFile.WriteLine("Reset");
                    CommandFile.WriteLine("Halt");
                    if (delete_config == 1)
                    {
                        CommandFile.WriteLine("Erase " + "0x" + prog_start.ToString("X") + " 0x" + data_end.ToString("X"));
                        if (dev.type == "AMA3B1KK-KBR")
                        {
                            CommandFile.WriteLine("Erase " + " 0xFE000 0xFFFFF");
                        }
                    }
                    else
                    {
                        CommandFile.WriteLine("Erase " + "0x" + prog_start.ToString("X") + " 0x" + prog_end.ToString("X"));
                        if (dev.type == "AMA3B1KK-KBR")
                        {
                            CommandFile.WriteLine("Erase " + " 0xFE000 0xFFFFF");
                        }
                    }
                    CommandFile.WriteLine("exit");
                    CommandFile.Close();

                    cmd1 = cmd1 + path.Replace("\\", "/") + "/J-Link/CommandFile.jlink" + "\"";

                    ExecuteCommandSync(cmd1); // Erase Program Memory

                    if (label11.Text.Substring(8, 5) != "ERROR")
                    {
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            progressBar1.Value = 50;
                        });

                        if (File.Exists(path + "\\J-Link\\CommandFile.jlink"))
                        {
                            File.Delete(path + "\\J-Link\\CommandFile.jlink");
                        }

                        cmd2 = "\"" + path.Replace("\\", "/") + "/J-Link/JLink.exe\" -USB "+ jtag_serial+ " -If SWD -Speed 4000 -Device " + dev.type + " -autoconnect 1 -CommandFile \"";

                        CommandFile = new System.IO.StreamWriter(path + "\\J-Link\\CommandFile.jlink");
                        CommandFile.WriteLine("RSetType 2\n" + 
                                              "Reset\n" +
                                              "Halt\n" +
                                              "Erase " + "0x" + prog_start.ToString("X") + " 0x" + prog_end.ToString("X") + "\n" + // noreset
                                              "LoadBin " + "\"" + firmwarePath.Replace("\\", "/") + "\"" + " 0x" + prog_start.ToString("X"));
                        CommandFile.WriteLine("exit");
                        CommandFile.Close();

                        cmd2 = cmd2 + path.Replace("\\", "/") + "/J-Link/CommandFile.jlink" + "\"";

                        ExecuteCommandSync(cmd2); // Write firmware on program memory
                        if (label11.Text.Substring(8, 5) != "ERROR")
                        {
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                progressBar1.Value = 80;
                            });
                            if (label11.Text.Substring(8, 5) != "ERROR")
                            {
                                cmd4 = "\"" + path.Replace("\\", "/") + "/J-Link/JLink.exe\" -USB "+ jtag_serial+ " -If SWD -Speed 4000 -Device " + dev.type + " -autoconnect 1 -CommandFile \"";

                                CommandFile = new System.IO.StreamWriter(path + "\\J-Link\\CommandFile.jlink");
                                CommandFile.WriteLine("SaveBin " + "\"" + path.Replace("\\", "/") + "/J-Link/memory.bin\"" + " 0x" + prog_start.ToString("X") + " 0x" + (data_end - prog_start + 1).ToString("X"));
                                CommandFile.WriteLine("RSetType 2\n" +
                                                      "Reset");
                                CommandFile.WriteLine("Go");
                                CommandFile.WriteLine("exit");
                                CommandFile.Close();

                                cmd4 = cmd4 + path.Replace("\\", "/") + "/J-Link/CommandFile.jlink" + "\"";

                                ExecuteCommandSync(cmd4);
                                if (label11.Text.Substring(8, 5) != "ERROR")
                                {
                                    disp_mem_content_JL(prog_start, data_end);
                                }
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    progressBar1.Value = 100;
                                });


                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    pictureBox2.Visible = true;
                                    label11.Text = "Status: Completed!";
                                    label11.Refresh();
                                });

                                if (delete_config == 1)
                                {
                                    if (dev.type == "AMA3B1KK-KBR" && current_FLFS_Version >= 0)
                                    {
                                        MessageBox.Show("The version of Flash File System on the node is not compatible with the new firmware flashed.\nThe Flash File System sector was deleted.\nUpcoming irrigations and the current status of the actuators, components, and pump system will be lost.", "Flash File System Deleted", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    }
                                    if (dev.name == "BU-C1")
                                    {
                                        MessageBox.Show("The configuration on the node is not compatible with the new firmware flashed.\nThe configuration was deleted and the node must reconfigured.\nAny custom XBee configurations also was deleted.\nThis may cause the node not to synchronize with other nodes if they have different parameters.", "Configuration Deleted", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    }

                                }
                            }
                            else
                            {
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    progressBar1.Value = 100;
                                });
                            }
                        }
                        else
                        {
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                progressBar1.Value = 100;
                            });
                        }
                    }
                    else
                    {
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            progressBar1.Value = 100;
                        });
                    }
                }
                else
                {
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        progressBar1.Value = 100;
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 100;
                });
            }
        }


        private void erase_JL()
        {

            Device dev = Devices.ElementAt(DeviceSelectedIndex);

            if (dev.name == "BU-C1")
            {
                dev = C1_CheckNodeBootloader(dev);
            }

            int prog_start = Convert.ToInt32(dev.program_start, 16);
            int prog_end = Convert.ToInt32(dev.program_end, 16);
            int data_end = Convert.ToInt32(dev.data_end, 16);

            // Delete the file if it exists.
            if (File.Exists(path + "\\J-Link\\CommandFile.jlink"))
            {
                File.Delete(path + "\\J-Link\\CommandFile.jlink");
            }

            System.IO.StreamWriter CommandFile = new System.IO.StreamWriter(path + "\\J-Link\\CommandFile.jlink");


            string cmd2 = "\"" + path.Replace("\\", "/") + "/J-Link/JLink.exe\" -USB "+ jtag_serial+ " -If SWD -Speed 4000 -Device " + dev.type + " -autoconnect 1 -CommandFile \"";

            CommandFile.WriteLine("Reset");
            CommandFile.WriteLine("Halt");
            if (checkEraseProgram.Checked && !checkEraseAll.Checked)
            {
                CommandFile.WriteLine("Erase " + " 0x" + prog_start.ToString("X") + " 0x" + prog_end.ToString("X") + " noreset");
                if (dev.type == "AMA3B1KK-KBR")
                {
                    CommandFile.WriteLine("Erase " + " 0xFE000 0xFFFFF noreset");
                }
            }
            if (checkEraseAll.Checked)
            {
                if (dev.type == "AMA3B1KK-KBR")
                {
                    CommandFile.WriteLine("Erase " + " 0x" + prog_start.ToString("X") + " 0x" + data_end.ToString("X") + " noreset");
                    CommandFile.WriteLine("Erase " + " 0xFE000 0xFFFFF noreset");
                }
                else 
                {
                    CommandFile.WriteLine("Erase");
                }
            }

            if (checkSoftReset.Checked || checkHardReset.Checked || checkRun.Checked)
            {
                if (checkSoftReset.Checked)
                    CommandFile.WriteLine("RSetType 0\n" +
                                          "Reset");
                if (checkHardReset.Checked)
                    CommandFile.WriteLine("RSetType 2\n" +
                                          "Reset");
                if (checkRun.Checked)
                {
                    if (!checkSoftReset.Checked && !(checkHardReset.Checked))
                        CommandFile.WriteLine("Reset");
                    CommandFile.WriteLine("Go");
                }
            }
            CommandFile.WriteLine("exit");
            CommandFile.Close();
            cmd2 = cmd2 + path.Replace("\\", "/") + "/J-Link/CommandFile.jlink" + "\"";


            if (checkEraseProgram.Checked || checkEraseAll.Checked)
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 0;
                    label11.Text = "Status: Erasing memory...";
                    label11.Refresh();
                });
                ExecuteCommandSync(cmd2);
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 100;
                });
                if (label11.Text.Substring(8, 5) != "ERROR")
                {
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        pictureBox2.Visible = true;
                        label11.Text = "Status: Erase completed!";
                        label11.Refresh();
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 0;
                    label11.Text = "Status: No operation performed.";
                    label11.Refresh();
                });
            }
        }


        private void verify_JL()
        {

            if (firmwarePath == "")
            {
                MessageBox.Show("Please select a file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Device dev = Devices.ElementAt(DeviceSelectedIndex);

            if (dev.name == "BU-C1")
            {
                dev = C1_CheckFileBootloader(dev);
            }

            int addr_prog = Convert.ToInt32(dev.program_start, 16);
            int addr_data = Convert.ToInt32(dev.data_start, 16);



            // Delete the file if it exists.
            if (File.Exists(path + "\\J-Link\\CommandFile.jlink"))
            {
                File.Delete(path + "\\J-Link\\CommandFile.jlink");
            }

            System.IO.StreamWriter CommandFile = new System.IO.StreamWriter(path + "\\J-Link\\CommandFile.jlink");


            string cmd2 = "\"" + path.Replace("\\", "/") + "/J-Link/JLink.exe\" -USB "+ jtag_serial+ " -If SWD -Speed 4000 -Device " + dev.type + " -autoconnect 1 -CommandFile \"";


            if (checkVerifyProgram.Checked)
                CommandFile.WriteLine("VerifyBin " + "\"" + firmwarePath + "\"" + " 0x" + addr_prog.ToString("X"));
            if (checkVerifyData.Checked && !checkVerifyProgram.Checked)
                CommandFile.WriteLine("VerifyBin " + "\"" + firmwarePath + "\"" + " 0x" + addr_data.ToString("X"));



            if (checkSoftReset.Checked || checkHardReset.Checked || checkRun.Checked)
            {
                if (checkSoftReset.Checked)
                    CommandFile.WriteLine("RSetType 0\n" +
                                          "Reset");
                if (checkHardReset.Checked)
                    CommandFile.WriteLine("RSetType 2\n" +
                                          "Reset");
                if (checkRun.Checked)
                {
                    if (!checkSoftReset.Checked && !(checkHardReset.Checked))
                        CommandFile.WriteLine("Reset");
                    CommandFile.WriteLine("Go");
                }
            }
            CommandFile.WriteLine("exit");
            CommandFile.Close();
            cmd2 = cmd2 + path.Replace("\\", "/") + "/J-Link/CommandFile.jlink" + "\"";
            if (checkVerifyProgram.Checked || checkVerifyData.Checked)
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 0;
                    label11.Text = "Status: Verifying...";
                    label11.Refresh();
                });
                ExecuteCommandSync(cmd2);
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 100;
                });
                if (label11.Text.Substring(8, 5) != "ERROR")
                {
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        pictureBox2.Visible = true;
                        label11.Text = "Status: Verify completed!";
                        label11.Refresh();
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 0;
                    label11.Text = "Status: No operation performed.";
                    label11.Refresh();
                });
            }


        }


        private void write_JL()
        {

            if (firmwarePath == "")
            {
                MessageBox.Show("Please select a file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            this.Invoke((MethodInvoker)delegate ()
            {
                progressBar1.Value = 0;
                label11.Text = "Status: Writing file in memory...";
                label11.Refresh();
            });

            Device dev = Devices.ElementAt(DeviceSelectedIndex);
            
            if (dev.name == "BU-C1")
            {
                dev = C1_CheckFileBootloader(dev);
            }

            int prog_start = Convert.ToInt32(dev.program_start, 16);
            int prog_end = Convert.ToInt32(dev.program_end, 16);
            int data_start = Convert.ToInt32(dev.data_start, 16);
            int data_end = Convert.ToInt32(dev.data_end, 16);

            // Delete the file if it exists.
            if (File.Exists(path + "\\J-Link\\CommandFile.jlink"))
            {
                File.Delete(path + "\\J-Link\\CommandFile.jlink");
            }

            System.IO.StreamWriter CommandFile = new System.IO.StreamWriter(path + "\\J-Link\\CommandFile.jlink");


            string cmd2 = "\"" + path.Replace("\\", "/") + "/J-Link/JLink.exe\" -USB "+ jtag_serial+ " -If SWD -Speed 4000 -Device " + dev.type + " -autoconnect 1 -CommandFile \"";

            if (checkWriteProgram.Checked)
                CommandFile.WriteLine("Reset\n" +
                                      "Halt\n" +
                                      "Erase" + " 0x" + prog_start.ToString("X") + " 0x" + prog_end.ToString("X") + "\n" +
                                      "LoadBin " + "\"" + firmwarePath.Replace("\\", "/") + "\"" + " 0x" + prog_start.ToString("X"));
            if (checkWriteData.Checked && !checkWriteProgram.Checked)
                CommandFile.WriteLine("Reset\n" +
                                      "Halt\n" +
                                      "Erase" + " 0x" + data_start.ToString("X") + " 0x" + data_end.ToString("X") + "\n" +
                                      "LoadBin " + "\"" + firmwarePath.Replace("\\", "/") + "\"" + " 0x" + data_start.ToString("X"));

            if (checkSoftReset.Checked || checkHardReset.Checked || checkRun.Checked)
            {
                if (checkSoftReset.Checked)
                    CommandFile.WriteLine("RSetType 0\n" +
                                          "Reset");
                if (checkHardReset.Checked)
                    CommandFile.WriteLine("RSetType 2\n" +
                                          "Reset");
                if (checkRun.Checked)
                {
                    if (!checkSoftReset.Checked && !(checkHardReset.Checked))
                        CommandFile.WriteLine("Reset");
                    CommandFile.WriteLine("Go");
                }
            }
            CommandFile.WriteLine("exit");
            CommandFile.Close();
            cmd2 = cmd2 + path.Replace("\\", "/") + "/J-Link/CommandFile.jlink" + "\"";

            if (checkWriteProgram.Checked || checkWriteData.Checked)
            {
                ExecuteCommandSync(cmd2);
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 100;
                });
                if (label11.Text.Substring(8, 5) != "ERROR")
                {
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        pictureBox2.Visible = true;
                        label11.Text = "Status: Write completed!";
                        label11.Refresh();
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 100;
                    label11.Text = "Status: No operation performed.";
                    label11.Refresh();
                });
            }
        }


        private void read_JL()
        {

            Device dev = Devices.ElementAt(DeviceSelectedIndex);

            if (dev.name == "BU-C1")
            {
                dev = C1_CheckNodeBootloader(dev);
            }

            int prog_start = Convert.ToInt32(dev.program_start, 16);
            int prog_end = Convert.ToInt32(dev.program_end, 16);
            int data_start = Convert.ToInt32(dev.data_start, 16);
            int data_end = Convert.ToInt32(dev.data_end, 16);

            int addr_init = prog_start;
            int addr_end = data_end;

            // Delete the file if it exists.
            if (File.Exists(path + "\\J-Link\\CommandFile.jlink"))
            {
                File.Delete(path + "\\J-Link\\CommandFile.jlink");
            }

            System.IO.StreamWriter CommandFile = new System.IO.StreamWriter(path + "\\J-Link\\CommandFile.jlink");

            string cmd2 = "\"" + path.Replace("\\", "/") + "/J-Link/JLink.exe\" -USB "+ jtag_serial+ " -If SWD -Speed 4000 -Device " + dev.type + " -autoconnect 1 -CommandFile \"";

            if (checkReadProgram.Checked && checkReadData.Checked)
            {
                CommandFile.WriteLine("SaveBin " + "\"" + path.Replace("\\", "/") + "/J-Link/memory.bin\"" + " 0x" + prog_start.ToString("X") + " 0x" + (data_end - prog_start + 1).ToString("X")); //SaveBin<FilePath> < Addr > < NumBytes >
                addr_init = prog_start;
                addr_end = data_end;
            }
            else
            {
                if (checkReadProgram.Checked)
                {
                    CommandFile.WriteLine("SaveBin " + "\"" + path.Replace("\\", "/") + "/J-Link/memory.bin\"" + " 0x" + prog_start.ToString("X") + " 0x" + (prog_end - prog_start + 1).ToString("X")); //SaveBin<FilePath> < Addr > < NumBytes >
                    addr_init = prog_start;
                    addr_end = prog_end;
                }
                if (checkReadData.Checked)
                {
                    CommandFile.WriteLine("SaveBin " + "\"" + path.Replace("\\", "/") + "/J-Link/memory.bin\"" + " 0x" + data_start.ToString("X") + " 0x" + (data_end - data_start + 1).ToString("X")); //SaveBin<FilePath> < Addr > < NumBytes >
                    addr_init = data_start;
                    addr_end = data_end;
                }
            }

            if (checkSoftReset.Checked || checkHardReset.Checked || checkRun.Checked)
            {
                if (checkSoftReset.Checked)
                    CommandFile.WriteLine("RSetType 0\n" +
                                          "Reset");
                if (checkHardReset.Checked)
                    CommandFile.WriteLine("RSetType 2\n" +
                                          "Reset");
                if (checkRun.Checked)
                {
                    if (!checkSoftReset.Checked && !(checkHardReset.Checked))
                        CommandFile.WriteLine("Reset");
                    CommandFile.WriteLine("Go");
                }
            }
            CommandFile.WriteLine("exit");
            CommandFile.Close();
            cmd2 = cmd2 + path.Replace("\\", "/") + "/J-Link/CommandFile.jlink" + "\"";
            if (checkReadProgram.Checked || checkReadData.Checked)
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 0;
                    label11.Text = "Status: Reading memory...";
                    label11.Refresh();
                });
                Console.WriteLine(cmd2);
                ExecuteCommandSync(cmd2);
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 100;
                });
                if (label11.Text.Substring(8, 5) != "ERROR")
                {
                    disp_mem_content_JL(addr_init, addr_end);
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        pictureBox2.Visible = true;
                        label11.Text = "Status: Reading completed!";
                        label11.Refresh();
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    progressBar1.Value = 100;
                    label11.Text = "Status: No operation performed.";
                    label11.Refresh();
                });
            }
        }


 
 

        private void Write_XBEE_content_TI(bool read_prog_mem, bool read_data_mem, int addr_prog, int addr_data, bool new_firmware = false)
        {
            System.Text.StringBuilder sb_code = new System.Text.StringBuilder();
            System.Text.StringBuilder sb_data = new System.Text.StringBuilder();
            int dir;
            int aux_addr;

            int Channel = 0;
            int MeshNetworkRetries = 0;
            string ChannelMask = "";
            int NetworkHops = 0;
            int NetworkDelaySlots = 0;
            int UnicastMacRetries = 0;
            int SleepTime = 0;
            int WakeTime = 0;
            int SleepOptions = 0;
            int SleepMode = 0;
            int PowerLevel = 0;
            int SoloGW = 0;
            int PreambleID = 0;
            int SecurityEnable = 0;
            string line;
            string message = "";

            // Read main memory and display in program memory text box
            if (read_prog_mem)
            {
                string filePath = System.IO.Path.Combine(path, "memoryTI_INFO.txt"); ;
                List<string> lines = File.ReadAllLines(filePath).ToList();
                int addressStart = 0x1800;
                int addressStruct = 0x1900;
                int structOffset = (addressStruct - addressStart) / 16;
                List<byte> memoryBytes = new List<byte>();
                for (int i = 1; i < lines.Count - 1; i++) // Ignorar la primera línea y la última ('q')
                {
                    string hexLine = lines[i].Trim(); // Eliminar espacios al inicio y final
                    if (!string.IsNullOrEmpty(hexLine))
                    {
                        // Eliminar espacios internos entre los pares de caracteres
                        hexLine = hexLine.Replace(" ", "");

                        if (hexLine.All(c => char.IsDigit(c) || "0123456789ABCDEF".Contains(c))) // Verificar que contiene solo caracteres válidos
                        {
                            for (int j = 0; j < hexLine.Length; j += 2)
                            {
                                try
                                {
                                    memoryBytes.Add(Convert.ToByte(hexLine.Substring(j, 2), 16));
                                }
                                catch (FormatException)
                                {
                                    Console.WriteLine($"Error al procesar la línea: {hexLine}. Carácter inválido encontrado.");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Línea no válida ignorada: {hexLine}");
                        }
                    }
                }


                XbeeInfoConfig config = ParseXbeeInfoConfig(memoryBytes.Skip(structOffset * 16).Take(64).ToArray());

                config.code = 0xFFFFFFFF;
                config.gprs = Program.varglobal.gprs;
                config.router = Program.varglobal.router;
                config.lone_coord = Program.varglobal.lone_coord;
                config.identifier = Program.varglobal.identifier;
                config.mod_Config = Program.varglobal.mod_Config;
                config.Channel = Program.varglobal.Channel;
                config.MeshNetworkRetries = Program.varglobal.MeshNetworkRetries;
                config.ChannelMask = Program.varglobal.ChannelMask;
                config.NetworkHops = Program.varglobal.NetworkHops;
                config.NetworkDelaySlots = Program.varglobal.NetworkDelaySlots;
                config.UnicastMacRetries = Program.varglobal.UnicastMacRetries;
                config.SleepTime = Program.varglobal.SleepTime;
                config.WakeTime = Program.varglobal.WakeTime;
                config.SleepOptions = Program.varglobal.SleepOptions;
                config.SleepMode = Program.varglobal.SleepMode;
                config.PowerLevel = Program.varglobal.PowerLevel;
                config.coordinator = Program.varglobal.coordinator;
                config.SoloGW = Program.varglobal.SoloGW;
                config.PreambleID= Program.varglobal.PreambleID;
                config.SecurityEnable = Program.varglobal.SecurityEnable;

                byte[] updatedStruct = SerializeXbeeInfoConfig(config);

                byte[] memoryArray = memoryBytes.ToArray();
                updatedStruct.CopyTo(memoryArray, structOffset * 16);
                // Convertir el array de nuevo en una lista
                memoryBytes = memoryArray.ToList();

                List<string> updatedLines = new List<string> { "@1800" };
                for (int i = 0; i < memoryBytes.Count; i += 16)
                {
                    var bytesToConvert = memoryBytes.Skip(i).Take(16);
                    var hexString = string.Join(" ", bytesToConvert.Select(b => b.ToString("X2")));
                    updatedLines.Add(hexString);
                }
                updatedLines.Add("q");

                // Escribir las líneas actualizadas en el archivo
                File.WriteAllLines(filePath, updatedLines);
                
                if (device == TypeDevice.Wpan)
                {
                   string name = "MSP430F5438A";

                   string cmd = "\"" + path.Replace("\\", "/") + "/TI/MSP430Flasher.exe\" -i " + jtag_port + " -n " + name + " -w \"memoryTI_INFO.txt\"" + " -v -e ERASE_SEGMENT" + " -s  -z [RESET,VCC] -u";

                   ExecuteCommandSync(cmd, 2200);
                }
                

                System.IO.StreamReader mainFile;
                try
                {
                    mainFile = new System.IO.StreamReader(path + "\\memoryTI_MAIN.txt");
                }
                catch (System.IO.FileNotFoundException)
                {
                    return;
                }

                while ((line = mainFile.ReadLine()) != null)
                {
                    if (line.StartsWith("@"))
                    {
                        dir = Convert.ToInt32(line.Substring(1), 16);
                        if (dir != addr_prog)
                        {
                            MessageBox.Show("Initial address for program memory section inconsistent. Check if the selected device is correct", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            break;
                        }
                    }
                    else
                    {
                        if (line.StartsWith("q"))
                            break;
                        sb_code.AppendLine(addr_prog.ToString("X") + " " + line);
                        addr_prog += 16;
                    }
                }
                this.Invoke((MethodInvoker)delegate ()
                {
                    richTextBox3.Text = sb_code.ToString();
                });
                mainFile.Close();
            }

            // Read data memory and display in data memory text box
            if (read_data_mem)
            {
                System.IO.StreamReader infoFile;
                try
                {
                    infoFile = new System.IO.StreamReader(path + "\\memoryTI_INFO.txt");
                }
                catch (System.IO.FileNotFoundException)
                {
                    return;
                }
                while ((line = infoFile.ReadLine()) != null)
                {
                    if (line.StartsWith("@"))
                    {
                        dir = Convert.ToInt32(line.Substring(1), 16);
                        if (dir != addr_data)
                        {
                            MessageBox.Show("Initial address for data memory section inconsistent. Check if the selected device is correct", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            break;
                        }
                    }
                    else
                    {
                        if (line.StartsWith("q"))
                            break;

                        aux_addr = addr_data + 8;
                        sb_data.AppendLine(addr_data.ToString("X") + " " + line.Substring(0, 8 * 3 - 1) + "\n" + aux_addr.ToString("X") + " " + line.Substring(8 * 3));

                        if (device == TypeDevice.Wpan)
                        {
                            if (aux_addr.ToString("X") == "1900")
                            {

                            }

                            if (aux_addr.ToString("X") == "1908")
                            {
                                string temp = line.Substring(8 * 3, 5);
                                temp = temp.Replace(" ", "");
                                string temp_reverse = reverse(temp);
                                int config = Int32.Parse(temp_reverse, System.Globalization.NumberStyles.HexNumber);


                                if ((config & 1) == 0)
                                {
                                    string tempCh = line.Substring(8 * 3 + 6, 2);
                                    Channel = Int32.Parse(tempCh, System.Globalization.NumberStyles.HexNumber);
                                    message += "\n- Channel: %01";
                                }
                                if ((config & 2) == 0)
                                {
                                    string tempNR = line.Substring(8 * 3 + 9, 2);
                                    MeshNetworkRetries = Int32.Parse(tempNR, System.Globalization.NumberStyles.HexNumber);
                                    message += "\n- MeshNetworkRetries: %02";
                                }
                                if ((config & 4) == 0)
                                {
                                    ChannelMask = line.Substring(8 * 3 + 12, 11).Replace(" ", "");
                                    message += "\n- ChannelMask: 0x%03  %15";
                                }
                                if ((config & 8) == 0)
                                {
                                    message += "\n- NetworkHops: %04";
                                }
                                if ((config & 16) == 0)
                                {
                                    message += "\n- NetworkDelaySlots: %05";
                                }
                                if ((config & 32) == 0)
                                {
                                    message += "\n- UnicastMacRetries: %06";
                                }
                                if ((config & 64) == 0)
                                {
                                    message += "\n- SleepTime: %07";
                                }
                                if ((config & 128) == 0)
                                {
                                    message += "\n- WakeTime: %08";
                                }
                                if ((config & 256) == 0)
                                {
                                    message += "\n- SleepOptions: %09";
                                }
                                if ((config & 512) == 0)
                                {
                                    message += "\n- SleepMode: %10  %16";
                                }
                                if ((config & 1024) == 0)
                                {
                                    message += "\n- PowerLevel: %11";
                                }
                                if ((config & 2048) == 0)
                                {
                                    message += "\n- Lone Coordinator (SoloGW): %12";
                                }
                                if ((config & 4096) == 0)
                                {
                                    message += "\n- Preamble ID: %13";
                                }
                                if ((config & 8192) == 0)
                                {
                                    message += "\n- Security Enable: %14  %17";
                                }

                            }
                            if (addr_data.ToString("X") == "1920")
                            {

                                ChannelMask += line.Substring(0, 11).Replace(" ", "");
                                string tempNH = line.Substring(12, 2);
                                NetworkHops = Byte.Parse(tempNH, System.Globalization.NumberStyles.HexNumber);

                                string tempND = line.Substring(15, 2);
                                NetworkDelaySlots = Byte.Parse(tempND, System.Globalization.NumberStyles.HexNumber);

                                string tempUR = line.Substring(18, 2);
                                UnicastMacRetries = Byte.Parse(tempUR, System.Globalization.NumberStyles.HexNumber);

                                string tempST = line.Substring(21, 11).Replace(" ", "");
                                tempST = reverse(tempST);
                                SleepTime = (int)UInt32.Parse(tempST, System.Globalization.NumberStyles.HexNumber);

                                string tempWT = line.Substring(33, 11).Replace(" ", "");
                                tempWT = reverse(tempWT);
                                WakeTime = (int)UInt32.Parse(tempWT, System.Globalization.NumberStyles.HexNumber);

                                string tempSO = line.Substring(45, 2);
                                SleepOptions = Byte.Parse(tempSO, System.Globalization.NumberStyles.HexNumber);
                            }
                            if (addr_data.ToString("X") == "1920")
                            {
                                string tempSM = line.Substring(0, 2);
                                SleepMode = Byte.Parse(tempSM, System.Globalization.NumberStyles.HexNumber);

                                string tempPL = line.Substring(3, 2);
                                PowerLevel = Byte.Parse(tempPL, System.Globalization.NumberStyles.HexNumber);

                                string tempSG = line.Substring(9, 2);
                                SoloGW = Byte.Parse(tempSG, System.Globalization.NumberStyles.HexNumber);

                                string tempHP = line.Substring(12, 2);
                                PreambleID = Byte.Parse(tempHP, System.Globalization.NumberStyles.HexNumber);

                                string tempEE = line.Substring(15, 2);
                                SecurityEnable = Byte.Parse(tempEE, System.Globalization.NumberStyles.HexNumber);
                            }


                        }
                        addr_data += 16;
                    }
                }
                this.Invoke((MethodInvoker)delegate ()
                {
                    richTextBox4.Text = sb_data.ToString();
                });
                infoFile.Close();
            }

            if (System.IO.File.Exists(path + "\\memoryTI_INFO.txt"))
            {
                try
                {
                    System.IO.File.Delete(path + "\\memoryTI_INFO.txt");
                }
                catch (System.IO.IOException e)
                {
                    MessageBox.Show(e.Message);
                    return;
                }
            }
            if (System.IO.File.Exists(path + "\\memoryTI_MAIN.txt"))
            {
                try
                {
                    System.IO.File.Delete(path + "\\memoryTI_MAIN.txt");
                }
                catch (System.IO.IOException e)
                {
                    MessageBox.Show(e.Message);
                    return;
                }
            }

            if (true)
            {
                message = message.Replace("%01", Channel.ToString());
                message = message.Replace("%02", MeshNetworkRetries.ToString());

                string ChannelMask_String = reverse(ChannelMask);
                ulong mask = UInt64.Parse(ChannelMask_String, System.Globalization.NumberStyles.HexNumber);
                string freq = "(902-912 Mhz)";
                if ((mask & 0x01FFFFFFFFFFFFFF) == 0x01FFFFFF00000000)
                    freq = "(915-925 Mhz)";
                else if ((mask & 0x0000000003F7FFFF) == 0x0000000003F7FFFF)
                    freq = "(902-912 Mhz)";
                else if (mask == 0xFFFFFFFE00000000)
                    freq = "(915-928 Mhz AUSTRALIA)";
                else
                    freq = "(Custom)";


                string sleep = "";
                switch (SleepMode)
                {
                    case 0:
                        sleep = "(Normal Mode)";
                        break;
                    case 7:
                        sleep = "(Sleep Support Mode)";
                        break;
                    case 8:
                        sleep = "(Sleep Mode)";
                        break;

                }

                // Mostrar un mensaje adicional con todos los parámetros
                string allParametersMessage = "Detalles de Configuración del Dispositivo XBee:\n\n" +
                                              $"- Channel: {Channel}\n" +
                                              $"- Mesh Network Retries: {MeshNetworkRetries}\n" +
                                              $"- Channel Mask: {ChannelMask_String}\n" +
                                              $"- Frequency: {freq}\n" +
                                              $"- Network Hops: {NetworkHops.ToString()}\n" +
                                              $"- Network Delay Slots: {NetworkDelaySlots}\n" +
                                              $"- Unicast MAC Retries: {UnicastMacRetries}\n" +
                                              $"- Sleep Time: {SleepTime} ms\n" +
                                              $"- Wake Time: {WakeTime} ms\n" +
                                              $"- Sleep Options: {SleepOptions}\n" +
                                              $"- Sleep Mode: {sleep}\n" +
                                              $"- Power Level: {PowerLevel}\n" +
                                              $"- Solo Gateway: {SoloGW}\n" +
                                              $"- Preamble ID: {PreambleID}\n" +
                                              $"- Security Enable: {SecurityEnable}\n" +
                                              $"- Security: {(SecurityEnable == 1 ? "XBee Encrypted" : "XBee not encrypted")}";

                MessageBox.Show(allParametersMessage, "Configuración Completa XBee", MessageBoxButtons.OK, MessageBoxIcon.Information);



            }
        }
        private void Lecture_XBEE_content_TI(bool read_prog_mem, bool read_data_mem, int addr_prog, int addr_data, bool new_firmware = false)
        {
            System.Text.StringBuilder sb_code = new System.Text.StringBuilder();
            System.Text.StringBuilder sb_data = new System.Text.StringBuilder();
            int dir;
            int aux_addr;

            int Channel = 0;
            int MeshNetworkRetries = 0;
            string ChannelMask = "";
            int NetworkHops = 0;
            int NetworkDelaySlots = 0;
            int UnicastMacRetries = 0;
            int SleepTime = 0;
            int WakeTime = 0;
            int SleepOptions = 0;
            int SleepMode = 0;
            int PowerLevel = 0;
            int SoloGW = 0;
            int PreambleID = 0;
            int SecurityEnable = 0;
            string line;
            string message = "";

            // Read main memory and display in program memory text box
            if (read_prog_mem)
            {
                System.IO.StreamReader mainFile;
                try
                {
                    mainFile = new System.IO.StreamReader(path + "\\memoryTI_MAIN.txt");
                }
                catch (System.IO.FileNotFoundException)
                {
                    return;
                }

                string filePath = @"C:\Users\cmuls\OneDrive\Escritorio\memoryTI_INFO.txt";
                List<string> lines = File.ReadAllLines(filePath).ToList();
                int addressStart = 0x1800;
                int addressStruct = 0x1900;
                int structOffset = (addressStruct - addressStart) / 16;
                List<byte> memoryBytes = new List<byte>();
                for (int i = 1; i < lines.Count - 1; i++) // Ignorar la primera línea y la última ('q')
                {
                    string hexLine = lines[i].Trim(); // Eliminar espacios al inicio y final
                    if (!string.IsNullOrEmpty(hexLine))
                    {
                        // Eliminar espacios internos entre los pares de caracteres
                        hexLine = hexLine.Replace(" ", "");

                        if (hexLine.All(c => char.IsDigit(c) || "0123456789ABCDEF".Contains(c))) // Verificar que contiene solo caracteres válidos
                        {
                            for (int j = 0; j < hexLine.Length; j += 2)
                            {
                                try
                                {
                                    memoryBytes.Add(Convert.ToByte(hexLine.Substring(j, 2), 16));
                                }
                                catch (FormatException)
                                {
                                    Console.WriteLine($"Error al procesar la línea: {hexLine}. Carácter inválido encontrado.");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Línea no válida ignorada: {hexLine}");
                        }
                    }
                }


                XbeeInfoConfig config = ParseXbeeInfoConfig(memoryBytes.Skip(structOffset * 16).Take(64).ToArray());

                config.gprs = 03;
                config.router = 03;
                config.Channel = 04;
                config.MeshNetworkRetries = 3;
                config.SleepTime = 6000;
                config.WakeTime = 200;


                byte[] updatedStruct = SerializeXbeeInfoConfig(config);

                byte[] memoryArray = memoryBytes.ToArray();
                updatedStruct.CopyTo(memoryArray, structOffset * 16);
                // Convertir el array de nuevo en una lista
                memoryBytes = memoryArray.ToList();

                List<string> updatedLines = new List<string> { "@1800" };
                for (int i = 0; i < memoryBytes.Count; i += 16)
                {
                    var bytesToConvert = memoryBytes.Skip(i).Take(16);
                    var hexString = string.Join(" ", bytesToConvert.Select(b => b.ToString("X2")));
                    updatedLines.Add(hexString);
                }
                updatedLines.Add("q");

                // Escribir las líneas actualizadas en el archivo
                File.WriteAllLines(filePath, updatedLines);

                updatedLines.Add("q");

                File.WriteAllLines(filePath, updatedLines);



                while ((line = mainFile.ReadLine()) != null)
                {
                    if (line.StartsWith("@"))
                    {
                        dir = Convert.ToInt32(line.Substring(1), 16);
                        if (dir != addr_prog)
                        {
                            MessageBox.Show("Initial address for program memory section inconsistent. Check if the selected device is correct", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            break;
                        }
                    }
                    else
                    {
                        if (line.StartsWith("q"))
                            break;
                        sb_code.AppendLine(addr_prog.ToString("X") + " " + line);
                        addr_prog += 16;
                    }
                }
                this.Invoke((MethodInvoker)delegate ()
                {
                    richTextBox3.Text = sb_code.ToString();
                });
                mainFile.Close();
            }

            // Read data memory and display in data memory text box
            if (read_data_mem)
            {
                System.IO.StreamReader infoFile;
                try
                {
                    infoFile = new System.IO.StreamReader(path + "\\memoryTI_INFO.txt");
                }
                catch (System.IO.FileNotFoundException)
                {
                    return;
                }
                while ((line = infoFile.ReadLine()) != null)
                {
                    if (line.StartsWith("@"))
                    {
                        dir = Convert.ToInt32(line.Substring(1), 16);
                        if (dir != addr_data)
                        {
                            MessageBox.Show("Initial address for data memory section inconsistent. Check if the selected device is correct", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            break;
                        }
                    }
                    else
                    {
                        if (line.StartsWith("q"))
                            break;

                        aux_addr = addr_data + 8;
                        sb_data.AppendLine(addr_data.ToString("X") + " " + line.Substring(0, 8 * 3 - 1) + "\n" + aux_addr.ToString("X") + " " + line.Substring(8 * 3));

                        if (device == TypeDevice.Wpan)
                        {
                            if (aux_addr.ToString("X") == "1900")
                            {
                              
                            }

                            if (aux_addr.ToString("X") == "1908")
                            {
                                string temp = line.Substring(8 * 3, 5);
                                    temp = temp.Replace(" ", "");
                                    string temp_reverse = reverse(temp);
                                    int config = Int32.Parse(temp_reverse, System.Globalization.NumberStyles.HexNumber);
                                   

                                    if ((config & 1) == 0)
                                    {
                                        string tempCh = line.Substring(8 * 3 + 6, 2);
                                        Channel = Int32.Parse(tempCh, System.Globalization.NumberStyles.HexNumber);
                                        message += "\n- Channel: %01";
                                    }
                                    if ((config & 2) == 0)
                                    {
                                        string tempNR = line.Substring(8 * 3 + 9, 2);
                                        MeshNetworkRetries = Int32.Parse(tempNR, System.Globalization.NumberStyles.HexNumber);
                                        message += "\n- MeshNetworkRetries: %02";
                                    }
                                    if ((config & 4) == 0)
                                    {
                                        ChannelMask = line.Substring(8 * 3 + 12, 11).Replace(" ", "");
                                        message += "\n- ChannelMask: 0x%03  %15";
                                    }
                                    if ((config & 8) == 0)
                                    {
                                        message += "\n- NetworkHops: %04";
                                    }
                                    if ((config & 16) == 0)
                                    {
                                        message += "\n- NetworkDelaySlots: %05";
                                    }
                                    if ((config & 32) == 0)
                                    {
                                        message += "\n- UnicastMacRetries: %06";
                                    }
                                    if ((config & 64) == 0)
                                    {
                                        message += "\n- SleepTime: %07";
                                    }
                                    if ((config & 128) == 0)
                                    {
                                        message += "\n- WakeTime: %08";
                                    }
                                    if ((config & 256) == 0)
                                    {
                                        message += "\n- SleepOptions: %09";
                                    }
                                    if ((config & 512) == 0)
                                    {
                                        message += "\n- SleepMode: %10  %16";
                                    }
                                    if ((config & 1024) == 0)
                                    {
                                        message += "\n- PowerLevel: %11";
                                    }
                                    if ((config & 2048) == 0)
                                    {
                                        message += "\n- Lone Coordinator (SoloGW): %12";
                                    }
                                    if ((config & 4096) == 0)
                                    {
                                        message += "\n- Preamble ID: %13";
                                    }
                                    if ((config & 8192) == 0)
                                    {
                                        message += "\n- Security Enable: %14  %17";
                                    }
                                
                            }
                            if (addr_data.ToString("X") == "1920")
                            {

                                ChannelMask += line.Substring(0, 11).Replace(" ", "");
                                string tempNH = line.Substring(12, 2);
                                NetworkHops = Byte.Parse(tempNH, System.Globalization.NumberStyles.HexNumber);

                                string tempND = line.Substring(15, 2);
                                NetworkDelaySlots = Byte.Parse(tempND, System.Globalization.NumberStyles.HexNumber);

                                string tempUR = line.Substring(18, 2);
                                UnicastMacRetries = Byte.Parse(tempUR, System.Globalization.NumberStyles.HexNumber);

                                string tempST = line.Substring(21, 11).Replace(" ", "");
                                tempST = reverse(tempST);
                                SleepTime = (int)UInt32.Parse(tempST, System.Globalization.NumberStyles.HexNumber);

                                string tempWT = line.Substring(33, 11).Replace(" ", "");
                                tempWT = reverse(tempWT);
                                WakeTime = (int)UInt32.Parse(tempWT, System.Globalization.NumberStyles.HexNumber);

                                string tempSO = line.Substring(45, 2);
                                SleepOptions = Byte.Parse(tempSO, System.Globalization.NumberStyles.HexNumber);
                            }
                            if (addr_data.ToString("X") == "1920")
                            {
                                string tempSM = line.Substring(0, 2);
                                SleepMode = Byte.Parse(tempSM, System.Globalization.NumberStyles.HexNumber);

                                string tempPL = line.Substring(3, 2);
                                PowerLevel = Byte.Parse(tempPL, System.Globalization.NumberStyles.HexNumber);

                                string tempSG = line.Substring(9, 2);
                                SoloGW =Byte.Parse(tempSG, System.Globalization.NumberStyles.HexNumber);

                                string tempHP = line.Substring(12, 2);
                                PreambleID = Byte.Parse(tempHP, System.Globalization.NumberStyles.HexNumber);

                                string tempEE = line.Substring(15, 2);
                                SecurityEnable = Byte.Parse(tempEE, System.Globalization.NumberStyles.HexNumber);
                            }


                        }
                        addr_data += 16;
                    }
                }
                this.Invoke((MethodInvoker)delegate ()
                {
                    richTextBox4.Text = sb_data.ToString();
                });
                infoFile.Close();
            }

            if (System.IO.File.Exists(path + "\\memoryTI_INFO.txt"))
            {
                try
                {
                    System.IO.File.Delete(path + "\\memoryTI_INFO.txt");
                }
                catch (System.IO.IOException e)
                {
                    MessageBox.Show(e.Message);
                    return;
                }
            }
            if (System.IO.File.Exists(path + "\\memoryTI_MAIN.txt"))
            {
                try
                {
                    System.IO.File.Delete(path + "\\memoryTI_MAIN.txt");
                }
                catch (System.IO.IOException e)
                {
                    MessageBox.Show(e.Message);
                    return;
                }
            }
 
            if(true)
            {
                    message = message.Replace("%01", Channel.ToString());
                    message = message.Replace("%02", MeshNetworkRetries.ToString());

                    string ChannelMask_String = reverse(ChannelMask);
                    ulong mask = UInt64.Parse(ChannelMask_String, System.Globalization.NumberStyles.HexNumber);
                    string freq = "(902-912 Mhz)";
                    if ((mask & 0x01FFFFFFFFFFFFFF) == 0x01FFFFFF00000000)
                        freq = "(915-925 Mhz)";
                    else if ((mask & 0x0000000003F7FFFF) == 0x0000000003F7FFFF)
                        freq = "(902-912 Mhz)";
                    else if (mask == 0xFFFFFFFE00000000)
                        freq = "(915-928 Mhz AUSTRALIA)";
                    else
                        freq = "(Custom)";
                 

                    string sleep = "";
                    switch (SleepMode)
                    {
                        case 0:
                            sleep = "(Normal Mode)";
                            break;
                        case 7:
                            sleep = "(Sleep Support Mode)";
                            break;
                        case 8:
                            sleep = "(Sleep Mode)";
                            break;

                    }
           
                    // Mostrar un mensaje adicional con todos los parámetros
                    string allParametersMessage = "Detalles de Configuración del Dispositivo XBee:\n\n" +
                                                  $"- Channel: {Channel}\n" +
                                                  $"- Mesh Network Retries: {MeshNetworkRetries}\n" +
                                                  $"- Channel Mask: {ChannelMask_String}\n" +
                                                  $"- Frequency: {freq}\n" +
                                                  $"- Network Hops: {NetworkHops.ToString()}\n" +
                                                  $"- Network Delay Slots: {NetworkDelaySlots}\n" +
                                                  $"- Unicast MAC Retries: {UnicastMacRetries}\n" +
                                                  $"- Sleep Time: {SleepTime} ms\n" +
                                                  $"- Wake Time: {WakeTime} ms\n" +
                                                  $"- Sleep Options: {SleepOptions}\n" +
                                                  $"- Sleep Mode: {sleep}\n" +
                                                  $"- Power Level: {PowerLevel}\n" +
                                                  $"- Solo Gateway: {SoloGW}\n" +
                                                  $"- Preamble ID: {PreambleID}\n" +
                                                  $"- Security Enable: {SecurityEnable}\n" +
                                                  $"- Security: {(SecurityEnable == 1 ? "XBee Encrypted" : "XBee not encrypted")}";

                    MessageBox.Show(allParametersMessage, "Configuración Completa XBee", MessageBoxButtons.OK, MessageBoxIcon.Information);


                
            }
        }


        private void disp_mem_content_TI(bool read_prog_mem, bool read_data_mem, int addr_prog, int addr_data, bool new_firmware = false)
        {
            bool old_firmware_boot = false;
            System.Text.StringBuilder sb_code = new System.Text.StringBuilder();
            System.Text.StringBuilder sb_data = new System.Text.StringBuilder();
            int dir;
            int aux_addr;
            
            int Channel = 0;
            int MeshNetworkRetries = 0;
            string ChannelMask = "";
            int NetworkHops = 0;
            int NetworkDelaySlots = 0;
            int UnicastMacRetries = 0;
            int SleepTime = 0;
            int WakeTime = 0;
            int SleepOptions = 0;
            int SleepMode = 0;
            int PowerLevel = 0;
            int SoloGW = 0;
            int PreambleID = 0;
            int SecurityEnable = 0;
            bool warning = false;
            string line;
            string message = "";

            // Read main memory and display in program memory text box
            if (read_prog_mem)
            {
                System.IO.StreamReader mainFile;
                try
                {
                    mainFile = new System.IO.StreamReader(path + "\\memoryTI_MAIN.txt");
                }
                catch (System.IO.FileNotFoundException)
                {
                    return;
                }
                while ((line = mainFile.ReadLine()) != null)
                {
                    if (line.StartsWith("@"))
                    {
                        dir = Convert.ToInt32(line.Substring(1), 16);
                        if (dir != addr_prog)
                        {
                            MessageBox.Show("Initial address for program memory section inconsistent. Check if the selected device is correct", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            break;
                        }
                    }
                    else
                    {
                        if (line.StartsWith("q"))
                            break;

                        if (addr_prog.ToString("X") == "44400")
                        {
                            if (line.Substring(3).StartsWith("82 32 C2 03 43"))
                            {
                                old_firmware_boot = true;
                                Debug.WriteLine("Old boot init detected!");
                            }
                        }
                        sb_code.AppendLine(addr_prog.ToString("X") + " " + line);
                        addr_prog += 16;
                    }
                }
                this.Invoke((MethodInvoker)delegate()
                {
                    richTextBox3.Text = sb_code.ToString();
                });
                mainFile.Close();
            }

            // Read data memory and display in data memory text box
            if (read_data_mem)
            {
                System.IO.StreamReader infoFile;
                try
                {
                    infoFile = new System.IO.StreamReader(path + "\\memoryTI_INFO.txt");
                }
                catch (System.IO.FileNotFoundException)
                {
                    return;
                }
                while ((line = infoFile.ReadLine()) != null)
                {
                    if (line.StartsWith("@"))
                    {
                        dir = Convert.ToInt32(line.Substring(1), 16);
                        if (dir != addr_data)
                        {
                            MessageBox.Show("Initial address for data memory section inconsistent. Check if the selected device is correct", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            break;
                        }
                    }
                    else
                    {
                        if (line.StartsWith("q"))
                            break;

                        aux_addr = addr_data + 8;
                        sb_data.AppendLine(addr_data.ToString("X") + " " + line.Substring(0, 8 * 3 - 1) + "\n" + aux_addr.ToString("X") + " " + line.Substring(8 * 3));

                        if (device == TypeDevice.Wpan)

                        {
                            if(aux_addr.ToString("X") == "1900")
                            {
                                string tempCh = line.Substring(8 * 3 + 6, 2);
                            }
                            if (aux_addr.ToString("X") == "1908")
                            {
                                string temp = line.Substring(8 * 3, 5);
                                if (!temp.StartsWith("FF FF"))  
                                {
                                    warning = true;
                                    temp = temp.Replace(" ", "");
                                    string temp_reverse = reverse(temp);
                                    int config = Int32.Parse(temp_reverse, System.Globalization.NumberStyles.HexNumber);
                                    Debug.WriteLine("Custom XBee configurations detected!");
                                    message = "Custom XBee configurations detected!.\nA custom configuration may cause the node not to synchronize with other nodes if they have different parameters.\n";

                                    if ((config & 1) == 0)
                                    {
                                        string tempCh = line.Substring(8 * 3 + 6, 2);
                                        Channel = Int32.Parse(tempCh, System.Globalization.NumberStyles.HexNumber);
                                        message += "\n- Channel: %01";
                                    }
                                    if ((config & 2) == 0)
                                    {
                                        string tempNR = line.Substring(8 * 3 + 9, 2);
                                        MeshNetworkRetries = Int32.Parse(tempNR, System.Globalization.NumberStyles.HexNumber);
                                        message += "\n- MeshNetworkRetries: %02";
                                    }
                                    if ((config & 4) == 0)
                                    {
                                        ChannelMask = line.Substring(8 * 3 + 12, 11).Replace(" ", "");
                                        message += "\n- ChannelMask: 0x%03  %15";
                                    }
                                    if ((config & 8) == 0)
                                    {
                                        message += "\n- NetworkHops: %04";
                                    }
                                    if ((config & 16) == 0)
                                    {
                                        message += "\n- NetworkDelaySlots: %05";
                                    }
                                    if ((config & 32) == 0)
                                    {
                                        message += "\n- UnicastMacRetries: %06";
                                    }
                                    if ((config & 64) == 0)
                                    {
                                        message += "\n- SleepTime: %07";
                                    }
                                    if ((config & 128) == 0)
                                    {
                                        message += "\n- WakeTime: %08";
                                    }
                                    if ((config & 256) == 0)
                                    {
                                        message += "\n- SleepOptions: %09";
                                    }
                                    if ((config & 512) == 0)
                                    {
                                        message += "\n- SleepMode: %10  %16";
                                    }
                                    if ((config & 1024) == 0)
                                    {
                                        message += "\n- PowerLevel: %11";
                                    }
                                    if ((config & 2048) == 0)
                                    {
                                        message += "\n- Lone Coordinator (SoloGW): %12";
                                    }
                                    if ((config & 4096) == 0)
                                    {
                                        message += "\n- Preamble ID: %13";
                                    }
                                    if ((config & 8192) == 0)
                                    {
                                        message += "\n- Security Enable: %14  %17";
                                    }
                                }
                            }
                            if (warning && addr_data.ToString("X") == "1910")
                            {
                                ChannelMask += line.Substring(0, 11).Replace(" ", "");
                                string tempNH = line.Substring(12, 2);
                                NetworkHops = Int32.Parse(tempNH, System.Globalization.NumberStyles.HexNumber);

                                string tempND = line.Substring(15, 2);
                                NetworkDelaySlots = Int32.Parse(tempND, System.Globalization.NumberStyles.HexNumber);

                                string tempUR = line.Substring(18, 2);
                                UnicastMacRetries = Int32.Parse(tempUR, System.Globalization.NumberStyles.HexNumber);

                                string tempST = line.Substring(21, 11).Replace(" ", "");
                                tempST = reverse(tempST);
                                SleepTime = Int32.Parse(tempST, System.Globalization.NumberStyles.HexNumber);

                                string tempWT = line.Substring(33, 11).Replace(" ", "");
                                tempWT = reverse(tempWT);
                                WakeTime = Int32.Parse(tempWT, System.Globalization.NumberStyles.HexNumber);

                                string tempSO = line.Substring(45, 2);
                                SleepOptions = Int32.Parse(tempSO, System.Globalization.NumberStyles.HexNumber);
                            }
                            if (warning && addr_data.ToString("X") == "1920")
                            {
                                string tempSM = line.Substring(0, 2);
                                SleepMode = Int32.Parse(tempSM, System.Globalization.NumberStyles.HexNumber);

                                string tempPL = line.Substring(3, 2);
                                PowerLevel = Int32.Parse(tempPL, System.Globalization.NumberStyles.HexNumber);

                                string tempSG = line.Substring(9, 2);
                                SoloGW = Int32.Parse(tempSG, System.Globalization.NumberStyles.HexNumber);

                                string tempHP = line.Substring(12, 2);
                                PreambleID = Int32.Parse(tempHP, System.Globalization.NumberStyles.HexNumber);

                                string tempEE = line.Substring(15, 2);
                                SecurityEnable = Int32.Parse(tempEE, System.Globalization.NumberStyles.HexNumber);
                            }

                        }
                        addr_data += 16;
                    }
                }
                this.Invoke((MethodInvoker)delegate()
                {
                    richTextBox4.Text = sb_data.ToString();
                });
                infoFile.Close();
            }

            if (System.IO.File.Exists(path + "\\memoryTI_INFO.txt"))
            {
                try
                {
                    System.IO.File.Delete(path + "\\memoryTI_INFO.txt");
                }
                catch (System.IO.IOException e)
                {
                    MessageBox.Show(e.Message);
                    return;
                }
            }
            if (System.IO.File.Exists(path + "\\memoryTI_MAIN.txt"))
            {
                try
                {
                    System.IO.File.Delete(path + "\\memoryTI_MAIN.txt");
                }
                catch (System.IO.IOException e)
                {
                    MessageBox.Show(e.Message);
                    return;
                }
            }

            if (new_firmware && old_firmware_boot)
            {
                Debug.WriteLine("Writing again. Range 0x42000-0x45BFF will erased.");
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 0;
                    label11.Text = "Status: Section of old firmware detected, writing again...";
                    label11.Refresh();
                });
                easy_TI(old_firmware_boot);
            }
            else
            {
                if (warning)
                {
                    message = message.Replace("%01", Channel.ToString());
                    message = message.Replace("%02", MeshNetworkRetries.ToString());

                    string ChannelMask_String = reverse(ChannelMask);
                    ulong mask = UInt64.Parse(ChannelMask_String, System.Globalization.NumberStyles.HexNumber);
                    string freq = "(902-912 Mhz)";
                    if ((mask & 0x01FFFFFFFFFFFFFF) == 0x01FFFFFF00000000)
                        freq = "(915-925 Mhz)";
                    else if ((mask & 0x0000000003F7FFFF) == 0x0000000003F7FFFF)
                        freq = "(902-912 Mhz)";
                    else if (mask == 0xFFFFFFFE00000000)
                        freq = "(915-928 Mhz AUSTRALIA)";
                    else
                        freq = "(Custom)";
                    message = message.Replace("%03", ChannelMask_String);
                    message = message.Replace("%15", freq);

                    message = message.Replace("%04", NetworkHops.ToString());
                    message = message.Replace("%05", NetworkDelaySlots.ToString());
                    message = message.Replace("%06", UnicastMacRetries.ToString());
                    message = message.Replace("%07", SleepTime.ToString());
                    message = message.Replace("%08", WakeTime.ToString());
                    message = message.Replace("%09", SleepOptions.ToString());
                    message = message.Replace("%10", SleepMode.ToString());

                    string sleep = "";
                    switch (SleepMode)
                    {
                        case 0:
                            sleep = "(Normal Mode)";
                            break;
                        case 7:
                            sleep = "(Sleep Support Mode)";
                            break;
                        case 8:
                            sleep = "(Sleep Mode)";
                            break;

                    }
                    message = message.Replace("%16", sleep);
                    message = message.Replace("%11", PowerLevel.ToString());
                    message = message.Replace("%12", SoloGW.ToString());
                    message = message.Replace("%13", PreambleID.ToString());
                    message = message.Replace("%14", SecurityEnable.ToString());
                    message = message.Replace("%17", (SecurityEnable == 1) ? "(XBee Encrypted)" : "(XBee not encripted)");
                    MessageBox.Show(message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    MessageBox.Show(message, WakeTime.ToString(), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void disp_mem_content_olimex(int addr_prog, int addr_data, bool new_firmware = false)
        {
            bool old_firmware_boot = false;
            short state = 0;
            int dir;
            int aux_addr;
            System.Text.StringBuilder sb_code = new System.Text.StringBuilder();
            System.Text.StringBuilder sb_data = new System.Text.StringBuilder();

            System.IO.StreamReader myFile;
            try
            {
                myFile = new System.IO.StreamReader(path + "\\memory.txt");
            }
            catch (System.IO.FileNotFoundException)
            {
                return;
            }

            int Channel = 0;
            int MeshNetworkRetries = 0;
            string ChannelMask = "";
            int NetworkHops = 0;
            int NetworkDelaySlots = 0;
            int UnicastMacRetries = 0;
            int SleepTime = 0;
            int WakeTime = 0;
            int SleepOptions = 0;
            int SleepMode = 0;
            int PowerLevel = 0;
            int SoloGW = 0;
            int PreambleID = 0;
            int SecurityEnable = 0;
            bool warning = false;
            string line;
            string message = "";
            while ((line = myFile.ReadLine()) != null)
            {
                if (line.StartsWith("@"))
                {
                    state = 1;
                }
                else if (line.StartsWith("q"))
                {
                    break;
                }

                switch (state)
                {
                    case 1:
                        dir = Convert.ToInt32(line.Substring(1), 16);
                        if (dir == addr_prog)
                            state = 2;
                        else if (dir == addr_data)
                            state = 3;
                        break;
                    case 2:
                        if (addr_prog.ToString("X") == "44400")
                        {
                            if (line.Substring(3).StartsWith("82 32 C2 03 43"))
                            {
                                old_firmware_boot = true;
                                Debug.WriteLine("Old boot init detected!");
                                log += "\nOld boot init detected!\n";
                            }
                        }
                        sb_code.AppendLine(addr_prog.ToString("X") + " " + line);
                        addr_prog += 16;
                        break;
                    case 3:
                        aux_addr = addr_data + 8;
                        sb_data.AppendLine(addr_data.ToString("X") + " " + line.Substring(0, 8 * 3 - 1) + "\n" + aux_addr.ToString("X") + " " + line.Substring(8 * 3));
                        if (device == TypeDevice.Wpan)
                        {
                            if (aux_addr.ToString("X") == "1908")
                            {
                                string temp = line.Substring(8 * 3, 5);
                                if (!temp.StartsWith("FF FF"))
                                {
                                    warning = true;
                                    temp = temp.Replace(" ", "");
                                    string temp_reverse = reverse(temp);
                                    int config = Int32.Parse(temp_reverse, System.Globalization.NumberStyles.HexNumber);
                                    Debug.WriteLine("Custom XBee configurations detected!");
                                    log += "\nCustom XBee configurations detected!\n";
                                    message = "Custom XBee configurations detected!.\nA custom configuration may cause the node not to synchronize with other nodes if they have different parameters.\n";

                                    if ((config & 1) == 0)
                                    {
                                        string tempCh = line.Substring(8 * 3 + 6, 2);
                                        Channel = Int32.Parse(tempCh, System.Globalization.NumberStyles.HexNumber);
                                        message += "\n- Channel: %01";
                                    }
                                    if ((config & 2) == 0)
                                    {
                                        string tempNR = line.Substring(8 * 3 + 9, 2);
                                        MeshNetworkRetries = Int32.Parse(tempNR, System.Globalization.NumberStyles.HexNumber);
                                        message += "\n- MeshNetworkRetries: %02";
                                    }
                                    if ((config & 4) == 0)
                                    {
                                        ChannelMask = line.Substring(8 * 3 + 12, 11).Replace(" ", "");
                                        message += "\n- ChannelMask: 0x%03  %15";
                                    }
                                    if ((config & 8) == 0)
                                    {
                                        message += "\n- NetworkHops: %04";
                                    }
                                    if ((config & 16) == 0)
                                    {
                                        message += "\n- NetworkDelaySlots: %05";
                                    }
                                    if ((config & 32) == 0)
                                    {
                                        message += "\n- UnicastMacRetries: %06";
                                    }
                                    if ((config & 64) == 0)
                                    {
                                        message += "\n- SleepTime: %07";
                                    }
                                    if ((config & 128) == 0)
                                    {
                                        message += "\n- WakeTime: %08";
                                    }
                                    if ((config & 256) == 0)
                                    {
                                        message += "\n- SleepOptions: %09";
                                    }
                                    if ((config & 512) == 0)
                                    {
                                        message += "\n- SleepMode: %10  %16";
                                    }
                                    if ((config & 1024) == 0)
                                    {
                                        message += "\n- PowerLevel: %11";
                                    }
                                    if ((config & 2048) == 0)
                                    {
                                        message += "\n- Lone Coordinator (SoloGW): %12";
                                    }
                                    if ((config & 4096) == 0)
                                    {
                                        message += "\n- Preamble ID: %13";
                                    }
                                    if ((config & 8192) == 0)
                                    {
                                        message += "\n- Security Enable: %14  %17";
                                    }
                                }
                            }
                            if (warning && addr_data.ToString("X") == "1910")
                            {
                                ChannelMask += line.Substring(0, 11).Replace(" ", "");
                                string tempNH = line.Substring(12, 2);
                                NetworkHops = Int32.Parse(tempNH, System.Globalization.NumberStyles.HexNumber);

                                string tempND = line.Substring(15, 2);
                                NetworkDelaySlots = Int32.Parse(tempND, System.Globalization.NumberStyles.HexNumber);

                                string tempUR = line.Substring(18, 2);
                                UnicastMacRetries = Int32.Parse(tempUR, System.Globalization.NumberStyles.HexNumber);

                                string tempST = line.Substring(21, 11).Replace(" ", "");
                                tempST = reverse(tempST);
                                SleepTime = Int32.Parse(tempST, System.Globalization.NumberStyles.HexNumber);

                                string tempWT = line.Substring(33, 11).Replace(" ", "");
                                tempWT = reverse(tempWT);
                                WakeTime = Int32.Parse(tempWT, System.Globalization.NumberStyles.HexNumber);

                                string tempSO = line.Substring(45, 2);
                                SleepOptions = Int32.Parse(tempSO, System.Globalization.NumberStyles.HexNumber);
                            }
                            if (warning && addr_data.ToString("X") == "1920")
                            {
                                string tempSM = line.Substring(0, 2);
                                SleepMode = Int32.Parse(tempSM, System.Globalization.NumberStyles.HexNumber);

                                string tempPL = line.Substring(3, 2);
                                PowerLevel = Int32.Parse(tempPL, System.Globalization.NumberStyles.HexNumber);

                                string tempSG = line.Substring(9, 2);
                                SoloGW = Int32.Parse(tempSG, System.Globalization.NumberStyles.HexNumber);

                                string tempHP = line.Substring(12, 2);
                                PreambleID = Int32.Parse(tempHP, System.Globalization.NumberStyles.HexNumber);

                                string tempEE = line.Substring(15, 2);
                                SecurityEnable = Int32.Parse(tempEE, System.Globalization.NumberStyles.HexNumber);
                            }
                        }
                        addr_data += 16;
                        break;
                }
            }
            this.Invoke((MethodInvoker)delegate()
            {
                richTextBox3.Text = sb_code.ToString();
                richTextBox4.Text = sb_data.ToString();
            });
            myFile.Close();

            if (System.IO.File.Exists(path + "\\memory.txt"))
            {
                try
                {
                    System.IO.File.Delete(path + "\\memory.txt");
                }
                catch (System.IO.IOException e)
                {
                    MessageBox.Show(e.Message);
                }
            }


            if (new_firmware && old_firmware_boot)
            {
                Debug.WriteLine("Writing again. Range 0x42000-0x45BFF will erased.");
                log += "\nWriting again. Range 0x42000-0x45BFF will erased.\n";
                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar1.Value = 0;
                    label11.Text = "Status: Section of old firmware detected, writing again...";
                    label11.Refresh();
                });
                easy_olimex(old_firmware_boot);
            }
            else
            {
                if (warning)
                {
                    message = message.Replace("%01", Channel.ToString());
                    message = message.Replace("%02", MeshNetworkRetries.ToString());

                    string ChannelMask_String = reverse(ChannelMask);
                    ulong mask = UInt64.Parse(ChannelMask_String, System.Globalization.NumberStyles.HexNumber);
                    string freq = "(902-912 Mhz)";
                    if ((mask & 0x01FFFFFFFFFFFFFF) == 0x01FFFFFF00000000)
                        freq = "(915-925 Mhz)";
                    else if ((mask & 0x0000000003F7FFFF) == 0x0000000003F7FFFF)
                        freq = "(902-912 Mhz)";
                    else if (mask == 0xFFFFFFFE00000000)
                        freq = "(915-928 Mhz AUSTRALIA)";
                    else
                        freq = "(Custom)";
                    message = message.Replace("%03", ChannelMask_String);
                    message = message.Replace("%15", freq);

                    message = message.Replace("%04", NetworkHops.ToString());
                    message = message.Replace("%05", NetworkDelaySlots.ToString());
                    message = message.Replace("%06", UnicastMacRetries.ToString());
                    message = message.Replace("%07", SleepTime.ToString());
                    message = message.Replace("%08", WakeTime.ToString());
                    message = message.Replace("%09", SleepOptions.ToString());
                    message = message.Replace("%10", SleepMode.ToString());

                    string sleep = "";
                    switch (SleepMode)
                    {
                        case 0:
                            sleep = "(Normal Mode)";
                            break;
                        case 7:
                            sleep = "(Sleep Support Mode)";
                            break;
                        case 8:
                            sleep = "(Sleep Mode)";
                            break;

                    }
                    message = message.Replace("%16", sleep);
                    message = message.Replace("%11", PowerLevel.ToString());
                    message = message.Replace("%12", SoloGW.ToString());
                    message = message.Replace("%13", PreambleID.ToString());
                    message = message.Replace("%14", SecurityEnable.ToString());
                    message = message.Replace("%17", (SecurityEnable == 1) ? "(XBee Encrypted)" : "(XBee not encripted)");
                    MessageBox.Show(message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void disp_mem_content_st(int addr_init, int addr_end, bool new_firmware = false)
        {
            Device temp = Devices.ElementAt(DeviceSelectedIndex);
            int addr_prog = Convert.ToInt32(temp.program_start, 16);
            int addr_data = Convert.ToInt32(temp.data_start, 16);
            int addr_data_end = Convert.ToInt32(temp.data_end, 16);

            System.Text.StringBuilder sb_code = new System.Text.StringBuilder();
            System.Text.StringBuilder sb_data = new System.Text.StringBuilder();

            System.IO.FileStream myFile;
            try
            {
                myFile = new System.IO.FileStream(path + "\\memory.bin",FileMode.Open);
            }
            catch (System.IO.FileNotFoundException)
            {
                return;
            }

            int index = addr_init;

            string line;

            byte[] buffer = new byte[16] ;
            byte[] buffer2 = new byte[8];
            while (index < addr_data && myFile.Read(buffer, 0, 16) != 0)
            {
                line = ByteArrayToString(buffer);
                sb_code.AppendLine(index.ToString("X") + " " + line);
                index += 16;
            }

            int state = 0;
            int Channel = 0;
            int MeshNetworkRetries = 0;
            string ChannelMask = "";
            int NetworkHops = 0;
            int NetworkDelaySlots = 0;
            int UnicastMacRetries = 0;
            int SleepTime = 0;
            int WakeTime = 0;
            int SleepOptions = 0;
            int SleepMode = 0;
            int PowerLevel = 0;
            int SoloGW = 0;
            int PreambleID = 0;
            int SecurityEnable = 0;
            bool warning = false;
            string message = "";

            while (index >= addr_data && myFile.Read(buffer2, 0, 8) != 0)
            {
                line = ByteArrayToString(buffer2);
                sb_data.AppendLine(index.ToString("X") + " " + line);

                if (device == TypeDevice.C1 || device == TypeDevice.M1)
                {
                    switch (state)
                    {
                        case 0:
                            if (index % 2048 == 0)
                            {
                                string type = line.Substring(0, 5);
                                if (type.StartsWith("2C 00"))
                                {
                                    state = 1;
                                }
                            }
                            break;

                        case 1:
                            state = 2;
                            break;

                        case 2:
                            string code = line.Substring(3, 5);
                            if (!code.StartsWith("00 00"))
                            {
                                warning = true;
                                code = code.Replace(" ", "");
                                string temp_reverse = reverse(code);
                                int config = Int32.Parse(temp_reverse, System.Globalization.NumberStyles.HexNumber);
                                Debug.WriteLine("Custom XBee configurations detected!");
                                log += "\nCustom XBee configurations detected!\n";
                                message = "Custom XBee configurations detected!.\nA custom configuration may cause the node not to synchronize with other nodes if they have different parameters.\n";

                                if ((config & 1) != 0)
                                {
                                    string tempCh = line.Substring(9, 2);
                                    Channel = Int32.Parse(tempCh, System.Globalization.NumberStyles.HexNumber);
                                    message += "\n- Channel: %01";
                                }
                                if ((config & 2) != 0)
                                {
                                    string tempNR = line.Substring(12, 2);
                                    MeshNetworkRetries = Int32.Parse(tempNR, System.Globalization.NumberStyles.HexNumber);
                                    message += "\n- MeshNetworkRetries: %02";
                                }
                                if ((config & 4) != 0)
                                {
                                    ChannelMask = line.Substring(15, 8).Replace(" ", "");
                                    message += "\n- ChannelMask: 0x%03  %15";
                                }
                                if ((config & 8) != 0)
                                {
                                    message += "\n- NetworkHops: %04";
                                }
                                if ((config & 16) != 0)
                                {
                                    message += "\n- NetworkDelaySlots: %05";
                                }
                                if ((config & 32) != 0)
                                {
                                    message += "\n- UnicastMacRetries: %06";
                                }
                                if ((config & 64) != 0)
                                {
                                    message += "\n- SleepTime: %07";
                                }
                                if ((config & 128) != 0)
                                {
                                    message += "\n- WakeTime: %08";
                                }
                                if ((config & 256) != 0)
                                {
                                    message += "\n- SleepOptions: %09";
                                }
                                if ((config & 512) != 0)
                                {
                                    message += "\n- SleepMode: %10  %16";
                                }
                                if ((config & 1024) != 0)
                                {
                                    message += "\n- PowerLevel: %11";
                                }
                                if ((config & 2048) != 0)
                                {
                                    message += "\n- Lone Coordinator (SoloGW): %12";
                                }
                                if ((config & 4096) != 0)
                                {
                                    message += "\n- Preamble ID: %13";
                                }
                                if ((config & 8192) != 0)
                                {
                                    message += "\n- Security Enable: %14  %17";
                                }
                                state = 3;
                            }
                            else
                            {
                                state = 100;
                            }
                            break;

                        case 3:
                            ChannelMask += line.Substring(0, 14).Replace(" ", "");

                            string tempNH = line.Substring(15, 2);
                            NetworkHops = Int32.Parse(tempNH, System.Globalization.NumberStyles.HexNumber);

                            string tempND = line.Substring(18, 2);
                            NetworkDelaySlots = Int32.Parse(tempND, System.Globalization.NumberStyles.HexNumber);

                            string tempUR = line.Substring(21, 2);
                            UnicastMacRetries = Int32.Parse(tempUR, System.Globalization.NumberStyles.HexNumber);

                            state = 4;
                            break;

                        case 4:
                            string tempST = line.Substring(0, 11).Replace(" ", "");
                            tempST = reverse(tempST);
                            SleepTime = Int32.Parse(tempST, System.Globalization.NumberStyles.HexNumber);

                            string tempWT = line.Substring(12, 11).Replace(" ", "");
                            tempWT = reverse(tempWT);
                            WakeTime = Int32.Parse(tempWT, System.Globalization.NumberStyles.HexNumber);

                            state = 5;
                            break;

                        case 5:
                            string tempSO = line.Substring(0, 2);
                            SleepOptions = Int32.Parse(tempSO, System.Globalization.NumberStyles.HexNumber);

                            string tempSM = line.Substring(3, 2);
                            SleepMode = Int32.Parse(tempSM, System.Globalization.NumberStyles.HexNumber);

                            string tempPL = line.Substring(6, 2);
                            PowerLevel = Int32.Parse(tempPL, System.Globalization.NumberStyles.HexNumber);

                            string tempSG = line.Substring(12, 2);
                            SoloGW = Int32.Parse(tempSG, System.Globalization.NumberStyles.HexNumber);

                            string tempHP = line.Substring(15, 2);
                            PreambleID = Int32.Parse(tempHP, System.Globalization.NumberStyles.HexNumber);

                            string tempEE = line.Substring(18, 2);
                            SecurityEnable = Int32.Parse(tempEE, System.Globalization.NumberStyles.HexNumber);

                            state = 100;
                            break;
                    }
                }

                index += 8;
            }
            this.Invoke((MethodInvoker)delegate()
            {
                richTextBox3.Text = sb_code.ToString();
                richTextBox4.Text = sb_data.ToString();
            });
            myFile.Close();

            if (System.IO.File.Exists(path + "\\memory.bin"))
            {
                try
                {
                    System.IO.File.Delete(path + "\\memory.bin");
                }
                catch (System.IO.IOException e)
                {
                    MessageBox.Show(e.Message);
                }
            }

            if (warning)
            {
                message = message.Replace("%01", Channel.ToString());
                message = message.Replace("%02", MeshNetworkRetries.ToString());
                
                string ChannelMask_String = reverse(ChannelMask);
                ulong mask = UInt64.Parse(ChannelMask_String, System.Globalization.NumberStyles.HexNumber);
                string freq = "(902-912 Mhz)";
                if ((mask & 0x01FFFFFFFFFFFFFF) == 0x01FFFFFF00000000)
                    freq = "(915-925 Mhz)";
                else if ((mask & 0x0000000003F7FFFF) == 0x0000000003F7FFFF)
                    freq = "(902-912 Mhz)";
                else if (mask == 0xFFFFFFFE00000000)
                    freq = "(915-928 Mhz AUSTRALIA)";
                else
                    freq = "(Custom)";
                message = message.Replace("%03", ChannelMask_String);
                message = message.Replace("%15", freq);

                message = message.Replace("%04", NetworkHops.ToString());
                message = message.Replace("%05", NetworkDelaySlots.ToString());
                message = message.Replace("%06", UnicastMacRetries.ToString());
                message = message.Replace("%07", SleepTime.ToString());
                message = message.Replace("%08", WakeTime.ToString());
                message = message.Replace("%09", SleepOptions.ToString());
                message = message.Replace("%10", SleepMode.ToString());

                string sleep = "";
                switch (SleepMode)
                { 
                    case 0:
                        sleep = "(Normal Mode)";
                        break;
                    case 7:
                        sleep = "(Sleep Support Mode)";
                        break;
                    case 8:
                        sleep = "(Sleep Mode)";
                        break;

                }
                message = message.Replace("%16", sleep);
                message = message.Replace("%11", PowerLevel.ToString());
                message = message.Replace("%12", SoloGW.ToString());
                message = message.Replace("%13", PreambleID.ToString());
                message = message.Replace("%14", SecurityEnable.ToString());
                message = message.Replace("%17", (SecurityEnable==1)?"(XBee Encrypted)":"(XBee not encripted)" );
                MessageBox.Show(message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

        }


        private void disp_mem_content_JL(int addr_init, int addr_end, bool new_firmware = false)
        {
            Device temp = Devices.ElementAt(DeviceSelectedIndex);
            int addr_prog = Convert.ToInt32(temp.program_start, 16);
            int addr_prog_end = Convert.ToInt32(temp.program_end, 16);
            int addr_data = Convert.ToInt32(temp.data_start, 16);
            int addr_data_end = Convert.ToInt32(temp.data_end, 16);

            System.Text.StringBuilder sb_code = new System.Text.StringBuilder();
            System.Text.StringBuilder sb_data = new System.Text.StringBuilder();

            System.IO.FileStream myFile;
            try
            {
                myFile = new System.IO.FileStream(path + "\\J-Link\\memory.bin", FileMode.Open);
            }
            catch (System.IO.FileNotFoundException)
            {
                return;
            }

            int index = addr_init;

            string line;

            byte[] buffer = new byte[16];
            byte[] buffer2 = new byte[8];
            while (index < addr_prog_end && myFile.Read(buffer, 0, 16) != 0)
            {
                line = ByteArrayToString(buffer);
                sb_code.AppendLine(index.ToString("X5") + " " + line);
                index += 16;
            }

            while (index < addr_data && myFile.Read(buffer, 0, 16) != 0)
            {
                index += 16;
            }

            int state = 0;
            int Channel = 0;
            int MeshNetworkRetries = 0;
            string ChannelMask = "";
            int NetworkHops = 0;
            int NetworkDelaySlots = 0;
            int UnicastMacRetries = 0;
            int SleepTime = 0;
            int WakeTime = 0;
            int SleepOptions = 0;
            int SleepMode = 0;
            int PowerLevel = 0;
            int SoloGW = 0;
            int PreambleID = 0;
            int SecurityEnable = 0;
            bool warning = false;
            string message = "";

            while (index <= addr_data_end && myFile.Read(buffer2, 0, 8) != 0)
            {
                line = ByteArrayToString(buffer2);
                sb_data.AppendLine(index.ToString("X5") + " " + line);

                if (device == TypeDevice.C1 || device == TypeDevice.M1)
                {
                    switch (state)
                    {
                        case 0:
                            if (index % 2048 == 0)
                            {
                                string type = line.Substring(0, 5);
                                if (type.StartsWith("2C 00"))
                                {
                                    state = 1;
                                }
                            }
                            break;

                        case 1:
                            state = 2;
                            break;

                        case 2:
                            string code = line.Substring(3, 5);
                            if (!code.StartsWith("00 00"))
                            {
                                warning = true;
                                code = code.Replace(" ", "");
                                string temp_reverse = reverse(code);
                                int config = Int32.Parse(temp_reverse, System.Globalization.NumberStyles.HexNumber);
                                Debug.WriteLine("Custom XBee configurations detected!");
                                log += "\nCustom XBee configurations detected!\n";
                                message = "Custom XBee configurations detected!.\nA custom configuration may cause the node not to synchronizec with other nodes if they have different parameters.\n";

                                if ((config & 1) != 0)
                                {
                                    string tempCh = line.Substring(9, 2);
                                    Channel = Int32.Parse(tempCh, System.Globalization.NumberStyles.HexNumber);
                                    message += "\n- Channel: %01";
                                }
                                if ((config & 2) != 0)
                                {
                                    string tempNR = line.Substring(12, 2);
                                    MeshNetworkRetries = Int32.Parse(tempNR, System.Globalization.NumberStyles.HexNumber);
                                    message += "\n- MeshNetworkRetries: %02";
                                }
                                if ((config & 4) != 0)
                                {
                                    ChannelMask = line.Substring(15, 8).Replace(" ", "");
                                    message += "\n- ChannelMask: 0x%03  %15";
                                }
                                if ((config & 8) != 0)
                                {
                                    message += "\n- NetworkHops: %04";
                                }
                                if ((config & 16) != 0)
                                {
                                    message += "\n- NetworkDelaySlots: %05";
                                }
                                if ((config & 32) != 0)
                                {
                                    message += "\n- UnicastMacRetries: %06";
                                }
                                if ((config & 64) != 0)
                                {
                                    message += "\n- SleepTime: %07";
                                }
                                if ((config & 128) != 0)
                                {
                                    message += "\n- WakeTime: %08";
                                }
                                if ((config & 256) != 0)
                                {
                                    message += "\n- SleepOptions: %09";
                                }
                                if ((config & 512) != 0)
                                {
                                    message += "\n- SleepMode: %10  %16";
                                }
                                if ((config & 1024) != 0)
                                {
                                    message += "\n- PowerLevel: %11";
                                }
                                if ((config & 2048) != 0)
                                {
                                    message += "\n- Lone Coordinator (SoloGW): %12";
                                }
                                if ((config & 4096) != 0)
                                {
                                    message += "\n- Preamble ID: %13";
                                }
                                if ((config & 8192) != 0)
                                {
                                    message += "\n- Security Enable: %14  %17";
                                }
                                state = 3;
                            }
                            else
                            {
                                state = 100;
                            }
                            break;

                        case 3:
                            ChannelMask += line.Substring(0, 14).Replace(" ", "");

                            string tempNH = line.Substring(15, 2);
                            NetworkHops = Int32.Parse(tempNH, System.Globalization.NumberStyles.HexNumber);

                            string tempND = line.Substring(18, 2);
                            NetworkDelaySlots = Int32.Parse(tempND, System.Globalization.NumberStyles.HexNumber);

                            string tempUR = line.Substring(21, 2);
                            UnicastMacRetries = Int32.Parse(tempUR, System.Globalization.NumberStyles.HexNumber);

                            state = 4;
                            break;

                        case 4:
                            string tempST = line.Substring(0, 11).Replace(" ", "");
                            tempST = reverse(tempST);
                            SleepTime = Int32.Parse(tempST, System.Globalization.NumberStyles.HexNumber);

                            string tempWT = line.Substring(12, 11).Replace(" ", "");
                            tempWT = reverse(tempWT);
                            WakeTime = Int32.Parse(tempWT, System.Globalization.NumberStyles.HexNumber);

                            state = 5;
                            break;

                        case 5:
                            string tempSO = line.Substring(0, 2);
                            SleepOptions = Int32.Parse(tempSO, System.Globalization.NumberStyles.HexNumber);

                            string tempSM = line.Substring(3, 2);
                            SleepMode = Int32.Parse(tempSM, System.Globalization.NumberStyles.HexNumber);

                            string tempPL = line.Substring(6, 2);
                            PowerLevel = Int32.Parse(tempPL, System.Globalization.NumberStyles.HexNumber);

                            string tempSG = line.Substring(12, 2);
                            SoloGW = Int32.Parse(tempSG, System.Globalization.NumberStyles.HexNumber);

                            string tempHP = line.Substring(15, 2);
                            PreambleID = Int32.Parse(tempHP, System.Globalization.NumberStyles.HexNumber);

                            string tempEE = line.Substring(18, 2);
                            SecurityEnable = Int32.Parse(tempEE, System.Globalization.NumberStyles.HexNumber);

                            state = 100;
                            break;
                    }
                }

                index += 8;
            }

            this.Invoke((MethodInvoker)delegate ()
            {
                if (sb_code.Length != 0 && addr_init == addr_prog)
                    richTextBox3.Text = sb_code.ToString();
                if (sb_data.Length != 0 && (addr_init == addr_prog || addr_init == addr_data) && (addr_end == addr_data_end))
                    richTextBox4.Text = sb_data.ToString();

            });
            myFile.Close();

            if (System.IO.File.Exists(path + "\\J-Link\\memory.bin"))
            {
                try
                {
                    System.IO.File.Delete(path + "\\J-Link\\memory.bin");
                }
                catch (System.IO.IOException e)
                {
                    MessageBox.Show(e.Message);
                }
            }

            if (warning)
            {
                message = message.Replace("%01", Channel.ToString());
                message = message.Replace("%02", MeshNetworkRetries.ToString());

                string ChannelMask_String = reverse(ChannelMask);
                ulong mask = UInt64.Parse(ChannelMask_String, System.Globalization.NumberStyles.HexNumber);
                string freq = "(902-912 Mhz)";
                if ((mask & 0x01FFFFFFFFFFFFFF) == 0x01FFFFFF00000000)
                    freq = "(915-925 Mhz)";
                else if ((mask & 0x0000000003F7FFFF) == 0x0000000003F7FFFF)
                    freq = "(902-912 Mhz)";
                else if (mask == 0xFFFFFFFE00000000)
                    freq = "(915-928 Mhz AUSTRALIA)";
                else
                    freq = "(Custom)";
                message = message.Replace("%03", ChannelMask_String);
                message = message.Replace("%15", freq);

                message = message.Replace("%04", NetworkHops.ToString());
                message = message.Replace("%05", NetworkDelaySlots.ToString());
                message = message.Replace("%06", UnicastMacRetries.ToString());
                message = message.Replace("%07", SleepTime.ToString());
                message = message.Replace("%08", WakeTime.ToString());
                message = message.Replace("%09", SleepOptions.ToString());
                message = message.Replace("%10", SleepMode.ToString());

                string sleep = "";
                switch (SleepMode)
                {
                    case 0:
                        sleep = "(Normal Mode)";
                        break;
                    case 7:
                        sleep = "(Sleep Support Mode)";
                        break;
                    case 8:
                        sleep = "(Sleep Mode)";
                        break;

                }
                message = message.Replace("%16", sleep);
                message = message.Replace("%11", PowerLevel.ToString());
                message = message.Replace("%12", SoloGW.ToString());
                message = message.Replace("%13", PreambleID.ToString());
                message = message.Replace("%14", SecurityEnable.ToString());
                message = message.Replace("%17", (SecurityEnable == 1) ? "(XBee Encrypted)" : "(XBee not encripted)");
                MessageBox.Show(message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

        }


        private string reverse(string str)
        {
            string res = "";
            int type = str.Length*4;

            if (type == 64 || type == 32 || type == 16)
            {
                res = reverse(str.Substring(type / 8, type / 8)) + reverse(str.Substring(0, type / 8));
            }
            else
            {
                res = str;
            }

            return res;
        }


        private void display_empty_mem()
        {
            if (DeviceSelectedIndex != -1)
            {
                Device temp = Devices.ElementAt(DeviceSelectedIndex);
                int code_start = Convert.ToInt32(temp.program_start, 16);
                int code_end = Convert.ToInt32(temp.program_end, 16);
                int data_start = Convert.ToInt32(temp.data_start, 16);
                int data_end = Convert.ToInt32(temp.data_end, 16);
                int n_rows_prog = (code_end - code_start) / 16;
                int n_rows_data = (data_end - data_start) / 8;
                string[] array_prog = new string[n_rows_prog + 1];
                string[] array_data = new string[n_rows_data + 1];

                int dir;
                for (int i = 0; n_rows_prog>0 && i < n_rows_prog + 1; i++)
                {
                    dir = code_start + i * 16;
                    array_prog[i] = dir.ToString("X5") + " FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF";
                }
                string prog_memory = String.Join("\n", array_prog);

                for (int i = 0; n_rows_data>0 && i < n_rows_data + 1; i++)
                {
                    dir = data_start + i * 8;
                    array_data[i] = dir.ToString("X5") + " FF FF FF FF FF FF FF FF";
                }
                string data_memory = String.Join("\n", array_data);

                richTextBox3.Text = prog_memory;
                richTextBox4.Text = data_memory;
            }
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            richTextBox1.SelectionStart = richTextBox1.Text.Length;
            richTextBox1.ScrollToCaret();
        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public static string ByteArrayToString(byte[] ba)
        {
            return BitConverter.ToString(ba).Replace("-", " ");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _sparkleNet.Initialize();
            _sparkleNet.CheckForUpdate(false, false);
        }

        private void checkUpdateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CheckUpdateEnable)
            {
                _sparkleNet.CheckForUpdate(true, false);
                CheckUpdateEnable = false;
            }
        }

        private void aboutToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            MessageBox.Show("WiseFlasher \n\nVersion " + version + "\n\n" + fvi.LegalCopyright, "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }


        private void readXBEEParamsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Thread thread = new Thread(() => Read_XBEE_Parameters_Click());
            thread.Name = "Read_XBEE_Parameters_Click";
            thread.Start();
        }

        private void writeXBEEParamsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form formulario2 = new Form2(this);
            formulario2.Show();
            Thread thread = new Thread(() => Write_XBEE_Parameters_Click());
            thread.Name = "Write_XBEE_Parameters_Click";
            thread.Start();
        }
    }
}
