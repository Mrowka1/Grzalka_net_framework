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

            if (!ctrl.IsPinOpen(GPIO))
            {
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
}
