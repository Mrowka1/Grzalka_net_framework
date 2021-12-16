﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Device.Gpio;
using EasyModbus;
namespace Grzalka
{
    class Program
    {

        //static int[] PhasesPins = { 26, 19, 13 };
        PowerState power;
        public PowerState Power
        {
            get => power;
            set
            {
                power = value;
                foreach (Phase p in Phase.Phases)
                {

                }
            }
        }
        public enum PowerState
        {
            Low = 0,
            Ok = 1,
            ForceOff = 2
        }

        class Phase
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

        /* static int pinPhases1_2 = 26;
         static int pinPhase3 = 19;
         static int pin_CommonPower = 13;*/



        static readonly double MinimalVoltage = 250.0;

        static GpioController ctrl;
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

            string configPath = rootPath + "/config.txt";

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
            }
            StartupArgs = args;
            ctrl = new GpioController(PinNumberingScheme.Logical);
            Console.WriteLine("Aktualny plik z danymi: " + DataLogFileName());
            Setup();
        }

        static void Setup()
        {
            Started = false;
            try
            {
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
                Console.WriteLine(ex.Message);

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
                    int pwr = reg[0];
                    int VolA = reg[3];
                    int VolB = reg[5];
                    int VolC = reg[7];
                    LogData(pwr, VolA, VolB, VolC);
                    // System.Threading.Thread.Sleep(1000);
                    // continue;
                    if (pwr >= 155)

                        System.Threading.Thread.Sleep(interval);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);


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
            return rootPath + "/" + dLogFileDate.ToString("dd.MM.yyyy").Replace(".", "_").Replace(" ", "_").Replace(":", "_") + ".csv";

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
            string EnabledPhases = "";

            foreach (Phase p in Phase.Phases) if (p.State == Phase.PhaseState.On) EnabledPhases += p.Symbol.ToString() + " ";

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
