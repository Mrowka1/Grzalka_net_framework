using System;
using System.Collections.Generic;
using System.IO;
using System.Device.Gpio;
using EasyModbus;
using Grzalka_net_framework;

namespace Grzalka
{
    public static class PowerInfo
    {
        public static double MinimalVoltage = 250.0;
        public static double MinimalPower = 1.5;

        static double powerVal;
        static public double PowerValue
        {
            get { return powerVal; }
            set
            {
                powerVal = value;
                if (power != PowerState.ForceOff)
                {
                    if (powerVal >= MinimalPower) { power = PowerState.Ok; } else power = PowerState.Low;
                }
            }
        }

        static PowerState power;
        static public PowerState Power
        {
            get => power;
            set
            {
                power = value;
                Phase.RefreshPhases();
            }
        }
        public enum PowerState
        {
            Low = 0,
            Ok = 1,
            ForceOff = 2,
            Waiting = 3
        }
    }
    public class Program
    {

        //static int[] PhasesPins = { 26, 19, 13 };


        /*    class Phase
            {
                static List<Phase> phases = new List<Phase>();
                public static Phase[] Phases => phases.ToArray();
                public enum PhasesSymbols
                {
                    A = 0,
                    B = 1,
                    C = 3
                }
                public readonly PhasesSymbols Symbol;
                public enum PhaseState
                {
                    Off = 0,
                    On = 1
                }
                PhaseState state;
                public PhaseState State
                {
                    get => state;
                    set
                    {
                        state = value;
                        UpdateRelay();
                    }
                }

                double voltage;
                public double Voltage
                {
                    get => voltage;
                    set
                    {
                        voltage = value;

                    }
                }
                public readonly int GPIO;

                public void Clear()
                {
                    phases.Clear();
                }

                public Phase(PhasesSymbols _symbol, int _gpioPin)
                {
                    state = PhaseState.Off;
                    Symbol = _symbol;
                    GPIO = _gpioPin;
                    phases.Add(this);

                    if (!ctrl.IsPinOpen(GPIO)) { 
                        ctrl.OpenPin(GPIO); ctrl.SetPinMode(GPIO, PinMode.Output);
                        ctrl.Write(GPIO, PinValue.High);
                    }
                }


                void UpdateRelay()
                {
                    if (!ctrl.IsPinOpen(GPIO)) { ctrl.OpenPin(GPIO); ctrl.SetPinMode(GPIO, PinMode.Output); }

                    if (state != PhaseState.On)
                        ctrl.Write(GPIO, PinValue.High);
                    else
                        ctrl.Write(GPIO, PinValue.Low);
                }
            }
        */
        /* static int pinPhases1_2 = 26;
         static int pinPhase3 = 19;
         static int pin_CommonPower = 13;*/





        //  static GpioController ctrl;
        static ModbusClient modbus;
        static bool Started;


        static DateTime dLogFileDate = DateTime.MinValue;
        static bool LogFileInitialized = false;
        static int interval = 1000;
        static string[] StartupArgs = { };
        static string rootPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        static DateTime dLastChange = DateTime.Now;


        static string serialPort;
        static void Main(string[] args)
        {
            try
            {

                /*  if (args.Length < 2)
                  {

                      Environment.Exit(0);
                  }*/
                string ports = "";

                foreach (string port in System.IO.Ports.SerialPort.GetPortNames())
                {
                    ports += port + ", ";
                }
                Console.WriteLine("Dostępne porty szeregowe:" + Environment.NewLine + ports);

                string configPath = System.IO.Path.GetDirectoryName( rootPath) + "/config.txt";

                Console.WriteLine("Plik konfiguracyjny: " + configPath);
                if (!System.IO.File.Exists(configPath))
                {
                    Console.WriteLine("Brak pliku konfiguracyjnego.");
                    Console.ReadLine();
                    Environment.Exit(0);
                }
                else
                {
                    string[] lines = System.IO.File.ReadAllLines(configPath);
                    serialPort = lines[0];
                    if (lines.Length >= 2) { PowerInfo.MinimalPower = double.Parse(lines[1]); }
                    if (lines.Length >= 3) { PowerInfo.MinimalVoltage = double.Parse(lines[2]); }
                    if (lines.Length >= 4) { Phase.PhasePins[0] = int.Parse(lines[3]); }
                    if (lines.Length >= 5) { Phase.PhasePins[1] = int.Parse(lines[4]); }
                    if (lines.Length >= 6) { Phase.PhasePins[2] = int.Parse(lines[5]); }

                }
                StartupArgs = args;
                Phase.ctrl = new GpioController(PinNumberingScheme.Logical);
                Console.WriteLine("Aktualny plik z danymi: " + DataLogFileName());
                Console.WriteLine("Minimalna moc: " + PowerInfo.MinimalPower + "; Minimalne napięcie: " + PowerInfo.MinimalVoltage);
                Console.WriteLine("Piny do faz: A:" + Phase.PhasePins[0] + " B: " + Phase.PhasePins[1] + " C: " + Phase.PhasePins[2]);
                Setup();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        static void Setup()
        {
            Started = false;
            try
            {
                if (Phase.GetPhase(Phase.PhasesSymbols.A) == null) new Phase(Phase.PhasesSymbols.A, Phase.PhasePins[0]);
                if (Phase.GetPhase(Phase.PhasesSymbols.B) == null) new Phase(Phase.PhasesSymbols.B, Phase.PhasePins[1]);
                if (Phase.GetPhase(Phase.PhasesSymbols.C) == null) new Phase(Phase.PhasesSymbols.C, Phase.PhasePins[2]);

                PowerInfo.Power = PowerInfo.PowerState.Waiting;

                if (modbus == null)
                {
                    modbus = new ModbusClient(serialPort);
                    modbus.ReceiveDataChanged += Modbus_ReceiveDataChanged;
                    modbus.ConnectedChanged += Modbus_ConnectedChanged;
                    modbus.DataReceivingEnd += Modbus_DataReceivingEnd;
                    modbus.Parity = System.IO.Ports.Parity.None;
                    modbus.StopBits = System.IO.Ports.StopBits.One;
                    modbus.UnitIdentifier = 1;
                    modbus.ConnectionTimeout = 1000;
                }
                else
                {
                    modbus.Disconnect();
                }
                modbus.Connect();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());

                System.Threading.Thread.Sleep(interval * 5);
                Setup();
            }
        }

        private static void Modbus_DataReceivingEnd(object sender)
        {
            Console.WriteLine("Zakończono odbieranie danych");
        }

        private static void Modbus_ReceiveDataChanged(object sender)
        {

        }

        static void Run()
        {
            if (Started) return;

            Started = true;
            try
            {

                while (true)
                {

                    int[] reg = modbus.ReadHoldingRegisters(12, 10);
                    double pwr = reg[0] / 100.0;
                    double VolA = reg[3] / 10.0;
                    double VolB = reg[5] / 10.0;
                    double VolC = reg[7] / 10.0;


                    PowerInfo.PowerValue = pwr;

                    Phase.GetPhase(Phase.PhasesSymbols.A).Voltage = VolA;
                    Phase.GetPhase(Phase.PhasesSymbols.B).Voltage = VolB;
                    Phase.GetPhase(Phase.PhasesSymbols.C).Voltage = VolC;

                    LogData(pwr, VolA, VolB, VolC);
                    // System.Threading.Thread.Sleep(1000);
                    // continue;
                    //if (pwr >= 155)

                    System.Threading.Thread.Sleep(interval);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());

                PowerInfo.Power = PowerInfo.PowerState.ForceOff;
                if (DateTime.Now.Hour >= 16 | DateTime.Now.Hour <= 7)
                {
                    System.Threading.Thread.Sleep(TimeSpan.FromMinutes(15));
                }
                else
                {
                    System.Threading.Thread.Sleep(interval * 5);
                }
                Setup();
            }
        }




        static string DataLogFileName()
        {
            if (dLogFileDate.Day != DateTime.Now.Day) dLogFileDate = DateTime.Now;
            return System.IO.Path.GetDirectoryName(rootPath) + "/" + dLogFileDate.ToString("dd.MM.yyyy").Replace(".", "_").Replace(" ", "_").Replace(":", "_") + ".csv";
        }
        static void LogData(double pwr, double vola, double volb, double volc)
        {
            string divider = ";";
            /*    if (dLogFileDate.Day != DateTime.Now.Day) dLogFileDate = DateTime.Now;
                string FileName = dLogFileDate.ToString("dd.MM.yyyy").Replace(".", "_").Replace(" ", "_").Replace(":", "_") + ".csv";*/
            string FileName = DataLogFileName();
            if (System.IO.File.Exists(FileName)) LogFileInitialized = true;
            if (!LogFileInitialized)
            {
                Console.WriteLine("Log filename: " + FileName);
                Log(FileName, "Data" + divider + "Moc" + divider + "Napięcie A" + divider + "Napięcie B" + divider + "Napiecie C" + divider + "Grzałka na fazie");
            }

            string EnabledPhases = "; PowerState: " + PowerInfo.Power.ToString() + "; Włączone fazy: ";

            foreach (Phase p in Phase.Phases) if (p.State == Phase.PhaseState.On) EnabledPhases += p.Symbol.ToString() + " ";
            int phATime = (int)(DateTime.Now - Phase.GetPhase(Phase.PhasesSymbols.A).LastRelaySwitchTime).TotalSeconds;
            int phBTime = (int)(DateTime.Now - Phase.GetPhase(Phase.PhasesSymbols.B).LastRelaySwitchTime).TotalSeconds;
            int phCTime = (int)(DateTime.Now - Phase.GetPhase(Phase.PhasesSymbols.C).LastRelaySwitchTime).TotalSeconds;
            Console.WriteLine(DateTime.Now.ToShortTimeString() + " Moc: " + (pwr).ToString().Replace(".", ",") + " A(" + phATime + "): " + vola.ToString().Replace(".", ",") + " B(" + phBTime +"): " + volb.ToString().Replace(".", ",") + " C(" + phCTime+ "): " + volc.ToString().Replace(".", ",") + EnabledPhases);

            Log(FileName, DateTime.Now.ToString() + divider + pwr.ToString().Replace(".", ",") + divider + vola.ToString().Replace(".", ",") + divider + volb.ToString().Replace(".", ",") + divider + volc.ToString().Replace(".", ",") + divider + EnabledPhases);
        }
        private static void Modbus_ConnectedChanged(object sender)
        {
            if (modbus.Connected) Run();
        }


        public static void Log(string fileName, string Text)
        {
            try
            {
                using (StreamWriter sw = File.AppendText(fileName))
                {
                    sw.WriteLine(Text);
                }
            }
            catch { }
        }
    }
}
