using Grzalka;
using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grzalka_net_framework
{
    internal class Phase
    {
        public static int iMinSwitchTime = 30;
        public static GpioController ctrl;
        static List<Phase> phases = new List<Phase>();
        public static Phase[] Phases => phases.ToArray();
        public enum PhasesSymbols
        {
            A = 0,
            B = 1,
            C = 3
        }
        public readonly PhasesSymbols Symbol;

        public static int[] PhasePins = { 26, 19, 13 };
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
                while (VoltageHistory.Count > 10)
                {
                    VoltageHistory.RemoveAt(VoltageHistory.Count - 1);
                }
                VoltageHistory.Insert(0, voltage);
                Refresh();
            }
        }

        List<double> VoltageHistory = new List<double>();
        public double AverageVoltage
        {
            get
            {
                double sum = 0;
                int count = VoltageHistory.Count;
                foreach (double vol in VoltageHistory)
                {
                    sum += vol;
                }
                return sum / count;
            }
        }

        public readonly int GPIO;
        static public Phase GetPhase(PhasesSymbols symbol)
        {
            foreach (Phase phase in phases)
            {
                if (phase.Symbol == symbol) return phase;
            }
            return null;
        }
        public void Clear()
        {
            phases.Clear();
        }

        public DateTime LastRelaySwitchTime = DateTime.MinValue;

        public Phase(PhasesSymbols _symbol, int _gpioPin)
        {
            state = PhaseState.Off;
            Symbol = _symbol;
            GPIO = _gpioPin;
            phases.Add(this);

            if (!ctrl.IsPinOpen(GPIO))
            {
                ctrl.OpenPin(GPIO); ctrl.SetPinMode(GPIO, PinMode.Output);
                ctrl.Write(GPIO, PinValue.High);
            }
        }

        public static void RefreshPhases()
        {
            foreach (Phase phase in phases)
            {
                phase.Refresh();
            }
        }

        void Refresh()
        {
            try
            {
                if (PowerInfo.Power == PowerInfo.PowerState.ForceOff)
                {
                    state = PhaseState.Off;
                    UpdateRelay();
                    return;
                }

                PhaseState newState = state;
                if (LastRelaySwitchTime.AddSeconds(iMinSwitchTime) < DateTime.Now)
                {
                 
                    if (AverageVoltage >= PowerInfo.MinimalVoltage && PowerInfo.Power == PowerInfo.PowerState.Ok)
                    { newState = PhaseState.On; }
                    else
                    {
                        if (AverageVoltage <= PowerInfo.MinimalVoltage-3  && state == PhaseState.On)
                        {
                            newState = PhaseState.Off;
                        }
                    }                   

                }
                if (newState != state)
                {
                    state = newState;
                    LastRelaySwitchTime = DateTime.Now;
                    //  UpdateRelay();             

                }
            }
            catch { }
            finally
            {
                UpdateRelay();
            }
        }

        PinValue getPinState()
        {
            try
            {
                if (!ctrl.IsPinOpen(GPIO)) { ctrl.OpenPin(GPIO); ctrl.SetPinMode(GPIO, PinMode.Output); }
                return ctrl.Read(GPIO);
            }
            catch
            {
                return PinValue.Low;
            }
        }

        void UpdateRelay()
        {
            try
            {
                if (!ctrl.IsPinOpen(GPIO)) { ctrl.OpenPin(GPIO); ctrl.SetPinMode(GPIO, PinMode.Output); }

                if (state != PhaseState.On)
                    ctrl.Write(GPIO, PinValue.High);
                else
                    ctrl.Write(GPIO, PinValue.Low);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
             //   LastRelaySwitchTime = DateTime.Now;
            }
        }
    }
}
