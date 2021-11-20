using System;
using System.Collections.Generic;
using System.IO;
using System.Device.Gpio;
using EasyModbus;
namespace Grzalka
{
    class Program
    {

        static int[] PhasesPins = { 26, 25, 24 };

        static int pinPhases1_2 = 26;
        static int pinPhase3 = 19;
        static int pin_CommonPower = 13;

        static ePhase CurPhase = 0;

        static double MinimalVoltageDiffence = 5.0d;
        static double MinimalVoltage = 249.0;

        static GpioController ctrl;
        static ModbusClient modbus;
        static bool Started;

        static int pwr = 0;
        static int VolA;
        static int VolB;
        static int VolC;

        static DateTime dLogFileDate = DateTime.MinValue;
        static bool LogFileInitialized = false;
        static int interval = 1000;
        static string[] StartupArgs = { };

        static DateTime dLastChange = DateTime.Now;

        enum ePhase
        {
            off = 0,
            A = 1,
            B = 2,
            C = 3,
            ForceOFF = 4
        }

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

            string configPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/config.txt";

            Console.WriteLine("Plik konfiguracyjny: " + configPath);
            if (!System.IO.File.Exists(configPath))
            {
                Environment.Exit(0);
            }
            else
            {
                string[] lines = System.IO.File.ReadAllLines(configPath);
                serialPort = lines[0];
                if (lines.Length >= 2) pinPhases1_2 = int.Parse(lines[1]);
                if (lines.Length >= 3) pinPhase3 = int.Parse(lines[2]);
                if (lines.Length >= 4) pin_CommonPower = int.Parse(lines[3]);
            }
            StartupArgs = args;
            ctrl = new GpioController(PinNumberingScheme.Logical);

            Setup();
        }

        static void Setup()
        {
            Started = false;
            try
            {
                TurnHeater1Phase(ePhase.ForceOFF);

                if (modbus != null)
                {
                    if (modbus.Connected) modbus.Disconnect();
                    modbus = null;
                }
                if (serialPort.Split(':').Length > 1)
                {
                    string ip = serialPort.Split(':')[0];
                    int port = int.Parse(serialPort.Split(':')[1]);
                    Console.WriteLine("Modbus TCP " + ip + ":" + port);
                    modbus = new ModbusClient(ip, port);
                }
                else
                {
                    modbus = new ModbusClient(serialPort);
                }
                modbus.ConnectedChanged += Modbus_ConnectedChanged;
                modbus.Parity = System.IO.Ports.Parity.None;
                modbus.StopBits = System.IO.Ports.StopBits.One;
                modbus.UnitIdentifier = 1;
                modbus.ConnectionTimeout = 1000;
                modbus.Connect();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                TurnHeater1Phase(ePhase.ForceOFF);
                System.Threading.Thread.Sleep(interval * 5);
                Setup();
            }
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
                    pwr = reg[0];
                    VolA = reg[3];
                    VolB = reg[5];
                    VolC = reg[7];
                    //LogData(pwr, VolA, VolB, VolC, CurPhase);
                    // System.Threading.Thread.Sleep(1000);
                    // continue;
                    if (pwr >= 155)
                    {




                        int[] Voltages = { VolA, VolB, VolC };
                        Array.Sort(Voltages);
                        int MidValue = Voltages[1];
                        int LowValue = Voltages[0];
                        int HighValue = Voltages[2];

                        if (VolA == HighValue &&  VolA> MinimalVoltage*10)
                        {
                            TurnHeater1Phase(ePhase.A);
                        }
                        else if (VolB == HighValue && VolB > MinimalVoltage * 10)
                        {
                            TurnHeater1Phase(ePhase.B);
                        }
                        else if (VolC == HighValue && VolC > MinimalVoltage * 10)
                        {
                            TurnHeater1Phase(ePhase.C);
                        }
                        else
                        {
                            TurnHeater1Phase(ePhase.off);
                        }

                        /*    if (VolA > MinimalVoltage & VolA > LowValue + MinimalVoltageDiffence )
                            {
                                TurnHeater1Phase(ePhase.A);
                            }
                            else if (VolB > MinimalVoltage & VolB > LowValue + MinimalVoltageDiffence )
                            {
                                TurnHeater1Phase(ePhase.B);
                            }
                            else if (VolC > MinimalVoltage & VolC > LowValue + MinimalVoltageDiffence )
                            {
                                TurnHeater1Phase(ePhase.C);
                            }
                            else
                            {
                                TurnHeater1Phase(ePhase.off);
                            }*/

                        //   
                    }
                    else
                    {
                        TurnHeater1Phase(ePhase.off);
                    }
                    System.Threading.Thread.Sleep(interval);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                TurnHeater1Phase(ePhase.ForceOFF);
                System.Threading.Thread.Sleep(interval * 5);
                Setup();
            }
        }


        static void TurnHeater3Phases(bool On, byte phase = 0)
        {
            /*     foreach (int pin in PhasesPins)
                 {
                     if (!ctrl.IsPinOpen(pin))
                     {
                         ctrl.OpenPin(pin, PinMode.Output);
                         ctrl.Write(pin, PinValue.High);
                     }
                 }
                 if (On && phase != 0)
                 {
                     ctrl.Write(PhasesPins[phase - 1], PinValue.Low);
                 }
            */
        }


        static void TurnHeater1Phase(ePhase phase = ePhase.off)
        {
            try
            {
                if (!ctrl.IsPinOpen(pinPhases1_2)) { ctrl.OpenPin(pinPhases1_2); ctrl.SetPinMode(pinPhases1_2, PinMode.Output); }
                if (!ctrl.IsPinOpen(pinPhase3)) { ctrl.OpenPin(pinPhase3); ctrl.SetPinMode(pinPhase3, PinMode.Output); }
                if (!ctrl.IsPinOpen(pin_CommonPower)) { ctrl.OpenPin(pin_CommonPower); ctrl.SetPinMode(pin_CommonPower, PinMode.Output); }

                int timeLeft = (int)(dLastChange.AddSeconds(30) - DateTime.Now).TotalSeconds;
                string timeLeftText = timeLeft + "s";
                if (timeLeft <= 0) timeLeftText = "teraz";

                if (phase == ePhase.ForceOFF)
                {
                    //  Console.Beep();
                    Console.WriteLine("[" + DateTime.Now.ToString() + "][FORCE OFF] PWR: " + ((Single)pwr/100.0).ToString() + "kw, A: " + ((Single)VolA / 10.0).ToString() + "V, B: " + ((Single)VolB / 10.0).ToString() + "V, C: " + ((Single)VolC / 10.0).ToString() + "V."); 
                }
                else
                { Console.WriteLine("[" + DateTime.Now.ToString() + "]  PWR: " + ((Single)pwr / 100.0).ToString() + "kw, A: " + ((Single)VolA / 10.0).ToString() + "V, B: " + ((Single)VolB / 10.0).ToString() + "V, C: " + ((Single)VolC / 10.0).ToString() + "V. Grzałka faza: " + CurPhase + ", możliwe przelączenie: " + timeLeftText); }

                if (phase != ePhase.ForceOFF)
                {
                    if (phase == CurPhase || dLastChange.AddSeconds(30) > DateTime.Now) return;
                }

                CurPhase = phase;
                dLastChange = DateTime.Now;
                switch (phase)
                {

                    case ePhase.A:
                        ctrl.Write(pinPhases1_2, PinValue.High);
                        ctrl.Write(pinPhase3, PinValue.High);
                        ctrl.Write(pin_CommonPower, PinValue.Low);
                        break;
                    case ePhase.B:
                        ctrl.Write(pinPhases1_2, PinValue.Low);
                        ctrl.Write(pinPhase3, PinValue.High);
                        ctrl.Write(pin_CommonPower, PinValue.Low);
                        break;
                    case ePhase.C:
                        ctrl.Write(pinPhases1_2, PinValue.High);
                        ctrl.Write(pinPhase3, PinValue.Low);
                        ctrl.Write(pin_CommonPower, PinValue.Low);
                        break;
                    default:
                        ctrl.Write(pin_CommonPower, PinValue.High);
                        ctrl.Write(pinPhases1_2, PinValue.High);
                        ctrl.Write(pinPhase3, PinValue.High);
                        break;

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        static void LogData(double pwr, double vola, double volb, double volc, ePhase phase)
        {
            string divider = ";";
            if (dLogFileDate.Day != DateTime.Now.Day) dLogFileDate = DateTime.Now;
            string FileName = dLogFileDate.ToShortDateString().Replace(".", "_").Replace(" ", "_").Replace(":", "_") + ".csv";
            if (System.IO.File.Exists(FileName)) LogFileInitialized = true;
            if (!LogFileInitialized)
            {
                Log(FileName, "Data" + divider + "Moc" + divider + "Napięcie A" + divider + "Napięcie B" + divider + "Napiecie C" + divider + "Grzałka na fazie");
            }
            Log(FileName, DateTime.Now.ToString() + divider + pwr.ToString().Replace(".", ",") + divider + vola.ToString().Replace(".", ",") + divider + volb.ToString().Replace(".", ",") + divider + volc.ToString().Replace(".", ",") + divider + phase.ToString());
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
