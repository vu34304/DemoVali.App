﻿using CommunityToolkit.Mvvm.Input;
using DemoCaseGui.Core.Application.Communication;
using HslCommunication;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using S7.Net;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Input;
using DateTimePoint = LiveChartsCore.Defaults.DateTimePoint;
using Timer = System.Timers.Timer;


namespace DemoCaseGui.Core.Application.ViewModels
{
    public class CaseMicroViewModel : BaseViewModel, INotifyPropertyChanged
    {
        private readonly Micro800Client _micro800Client;
        public bool IsConnected => _micro800Client.IsConnected;
        private CancellationTokenSource _cancellationTokenSource;

        public bool Status { get; set; }
        public bool IsON { get; set; }

        private readonly Timer _timer;
        private readonly Timer CheckConnected;

        private readonly List<DateTimePoint> _values = new();

        private readonly DateTimeAxis _customAxis;



        //TrafficLight

        public bool? Led2 { get; set; }
        public bool? Led3 { get; set; }
        public bool? Led4 { get; set; }
        public bool? Led5 { get; set; }
        public bool? Led6 { get; set; }
        public bool? Led7 { get; set; }

        public bool? led2_old, led3_old, led4_old, led5_old, led6_old, led7_old;

        public bool? Start_TrafficLights { get; set; }
        public bool? Stop_TrafficLights { get; set; }
        public bool? start_trafficlights_old, stop_trafficlights_old;

        public float? Edit_RedLed { get; set; }
        public float? Edit_YellowLed { get; set; }
        public float? Edit_GreenLed { get; set; }

        public ushort? edit_redled_old, edit_greenled_old, edit_yellowled_old, setpoint_old, time_display_a_old, time_display_b_old;

        //Inverter
        public bool? Start { get; set; }
        public bool? Stop { get; set; }
        public bool? start_old, stop_old, forward_old, reverse_old;

        public float? countRB3100_old, distanceUGT524_old, speed_old;

        public float? MotorSetpointWrite { get; set; }
        public float? MotorSetpoint { get; set; }
        public float? Time_Display_A { get; set; }
        public float? Time_Display_B { get; set; }
        public float? MotorSpeed { get; set; }
        public bool? MotorForward { get; set; }
        public bool? MotorReverse { get; set; }
        public bool? MotorForward1 { get; set; }
        public bool? MotorReverse1 { get; set; }

        public float? Direction { get; set; }
        public bool? ButtonStartup { get; set; }
        public bool? ButtonStop { get; set; }
        public bool? ButtonStartup1 { get; set; }
        public bool? ButtonStop1 { get; set; }
        public double MotorSpeed1 { get; set; }

        //Micro820

        public bool? i00 { get; set; }
        public bool? i01 { get; set; }
        public bool? i02 { get; set; }
        public bool? i03 { get; set; }
        public bool? i04 { get; set; }
        public bool? i05 { get; set; }
        public bool? i06 { get; set; }
        public bool? i07 { get; set; }
        public ushort? Analog_temp { get; set; }
        public double Analog { get; set; }
        public bool? i00_old, i01_old, i02_old, i03_old, i04_old, i05_old, i06_old, i07_old;
        public ushort? analog_old;
        //Loading Animation
        public bool Isbusy { get; set; }

        //Command
        public ICommand ConnectCommand { get; set; }
        public ICommand DisconnectCommand { get; set; }
        public ICommand CheckConnectCommand { get; set; }

        public ICommand MotorSetpointOKCommand { get; set; }
        public ICommand StartTrafficLightsCommand { get; set; }
        public ICommand StopTrafficLightsCommand { get; set; }
        public ICommand StartInverterCommand { get; set; }
        public ICommand StopInverterCommand { get; set; }
        public ICommand ConfirmTrafficLights_Command { get; set; }
        public ICommand ConfirmInverter_Command { get; set; }
        public ICommand ForwardCommand { get; set; }
        public ICommand ReverseCommand { get; set; }


        public CaseMicroViewModel()
        {
            _micro800Client = new Micro800Client();
            _timer = new Timer(300);
            _timer.Elapsed += _timer_Elapsed;
            _cancellationTokenSource =  new CancellationTokenSource();

            CheckConnected = new Timer(300);
            
            CheckConnected.Elapsed += CheckConnected_Elapsed;
            Status = false;
            //Button Command
            //CheckConnectCommand = new RelayCommand(CheckConnect);
            ConnectCommand = new RelayCommand(Connect);// Connect PLC
            DisconnectCommand = new RelayCommand(Disconnect);// Disonnect PLC
            //Traffic Light
            StartTrafficLightsCommand = new RelayCommand(Start_TrafficLight);
            StopTrafficLightsCommand = new RelayCommand(Stop_TrafficLight);
            ConfirmTrafficLights_Command = new RelayCommand(ConfirmTrafficLight);

            //Inverter
            MotorSetpointOKCommand = new RelayCommand(WriteMotorSetpoint);
            StartInverterCommand = new RelayCommand(Start_Inverter);
            StopInverterCommand = new RelayCommand(Stop_Inverter);
            ForwardCommand = new RelayCommand(Forward_Inverter);
            ReverseCommand = new RelayCommand(Reverse_Inverter);

            IsON = true;

            Series = new ObservableCollection<ISeries>
        {
            new LineSeries<DateTimePoint>
            {
                Values = _values,
                Fill = null,
                GeometryFill = null,
                GeometryStroke = null
            }
        };

            _customAxis = new DateTimeAxis(TimeSpan.FromSeconds(1), Formatter)
            {
                CustomSeparators = GetSeparators(),
                AnimationsSpeed = TimeSpan.FromMilliseconds(0),
                SeparatorsPaint = new SolidColorPaint(SKColors.Black.WithAlpha(100))
            };

            XAxes = new LiveChartsCore.SkiaSharpView.Axis[] { _customAxis };

            _ = ReadData();
        }

        private async void CheckConnected_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (_cancellationTokenSource.IsCancellationRequested)
            {
                _timer.Enabled = false;
                CheckConnected.Stop();
                Status = false;
                IsON = true;
                MessageBox.Show("Mất kết nối với địa chỉ IP: 192.168.1.50 & 192.168.1.20 !!! ", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                //CheckConnect
                Task CheckConnect = new(() =>
                {
                    if (!PingHost("192.168.1.20").Result && !PingHost("192.168.1.50").Result)
                    {
                        _cancellationTokenSource.Cancel();
                    }
                });
                CheckConnect.Start();
                await CheckConnect;
            }
        }

        private async void _timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            Task ReadData = new(() =>
            {
                if ((bool?)_micro800Client.GetTagValue("start_trafficlight") != start_trafficlights_old)
                {
                    Start_TrafficLights = (bool?)_micro800Client.GetTagValue("start_trafficlight");
                }
                start_trafficlights_old = (bool?)_micro800Client.GetTagValue("start_trafficlight");
                if (Start_TrafficLights is true)
                {
                    ButtonStartup1 = true;
                    ButtonStop1 = false;
                }

                if ((bool?)_micro800Client.GetTagValue("stop_trafficlight") != stop_trafficlights_old)
                {
                    Stop_TrafficLights = (bool?)_micro800Client.GetTagValue("stop_trafficlight");
                }
                stop_trafficlights_old = (bool?)_micro800Client.GetTagValue("stop_trafficlight");
                if (Stop_TrafficLights is true)
                {
                    ButtonStartup1 = false;
                    ButtonStop1 = true;
                }


                if ((bool?)_micro800Client.GetTagValue("led2") != led2_old)
                {
                    Led2 = (bool?)_micro800Client.GetTagValue("led2");
                }
                led2_old = (bool?)_micro800Client.GetTagValue("led2");

                if ((bool?)_micro800Client.GetTagValue("led3") != led3_old)
                {
                    Led3 = (bool?)_micro800Client.GetTagValue("led3");
                }
                led3_old = (bool?)_micro800Client.GetTagValue("led3");

                if ((bool?)_micro800Client.GetTagValue("led4") != led4_old)
                {
                    Led4 = (bool?)_micro800Client.GetTagValue("led4");
                }
                led4_old = (bool?)_micro800Client.GetTagValue("led4");

                if ((bool?)_micro800Client.GetTagValue("led5") != led5_old)
                {
                    Led5 = (bool?)_micro800Client.GetTagValue("led5");
                }
                led5_old = (bool?)_micro800Client.GetTagValue("led5");

                if ((bool?)_micro800Client.GetTagValue("led6") != led6_old)
                {
                    Led6 = (bool?)_micro800Client.GetTagValue("led6");
                }
                led6_old = (bool?)_micro800Client.GetTagValue("led6");

                if ((bool?)_micro800Client.GetTagValue("led7") != led7_old)
                {
                    Led7 = (bool?)_micro800Client.GetTagValue("led7");
                }
                led7_old = (bool?)_micro800Client.GetTagValue("led7");

                if ((ushort?)_micro800Client.GetTagValue("edit_redled") != edit_redled_old)
                {
                    Edit_RedLed = (ushort?)_micro800Client.GetTagValue("edit_redled");
                }
                edit_redled_old = (ushort?)_micro800Client.GetTagValue("edit_redled");

                if ((ushort?)_micro800Client.GetTagValue("edit_greenled") != edit_greenled_old)
                {
                    Edit_GreenLed = (ushort?)_micro800Client.GetTagValue("edit_greenled");
                }
                edit_greenled_old = (ushort?)_micro800Client.GetTagValue("edit_greenled");

                if ((ushort?)_micro800Client.GetTagValue("edit_yellowled") != edit_yellowled_old)
                {
                    Edit_YellowLed = (ushort?)_micro800Client.GetTagValue("edit_yellowled");
                }
                edit_yellowled_old = (ushort?)_micro800Client.GetTagValue("edit_yellowled");

                if ((ushort?)_micro800Client.GetTagValue("time_display_a") != time_display_a_old)
                {
                    Time_Display_A = (ushort?)_micro800Client.GetTagValue("time_display_a");
                }
                time_display_a_old = (ushort?)_micro800Client.GetTagValue("time_display_a");

                if ((ushort?)_micro800Client.GetTagValue("time_display_b") != time_display_b_old)
                {
                    Time_Display_B = (ushort?)_micro800Client.GetTagValue("time_display_b");
                }


                ButtonStartup = (bool?)_micro800Client.GetTagValue("inverter_active");
                ButtonStop = (bool?)_micro800Client.GetTagValue("inverter_error");
                MotorForward = (bool?)_micro800Client.GetTagValue("inverter_fwd_status");
                MotorReverse = (bool?)_micro800Client.GetTagValue("inverter_rev_status");

                if ((ushort?)_micro800Client.GetTagValue("setpoint") != setpoint_old)
                {
                    MotorSetpoint = (ushort?)_micro800Client.GetTagValue("setpoint") *60;
                }
                setpoint_old = (ushort?)_micro800Client.GetTagValue("setpoint");

                if ((float?)_micro800Client.GetTagValue("speed") != speed_old)
                {
                    MotorSpeed = (float?)_micro800Client.GetTagValue("speed") * 60;
                    //MotorSpeed1 = Math.Round((float)_micro800Client.GetTagValue("speed"), 2);
                }
                speed_old = (float?)_micro800Client.GetTagValue("speed") * 60;

                var now = DateTime.Now;




                //Micro820

                //IO
                if ((bool?)_micro800Client.GetTagValue2("i0.0") != i00_old)
                {
                    i00 = (bool?)_micro800Client.GetTagValue2("i0.0");
                }
                i00_old = (bool?)_micro800Client.GetTagValue2("i0.0");


                if ((bool?)_micro800Client.GetTagValue2("i0.1") != i01_old)
                {
                    i01 = (bool?)_micro800Client.GetTagValue2("i0.1");
                }
                i01_old = (bool?)_micro800Client.GetTagValue2("i0.1");

                if ((bool?)_micro800Client.GetTagValue2("i0.2") != i02_old)
                {
                    i02 = (bool?)_micro800Client.GetTagValue2("i0.2");
                }
                i02_old = (bool?)_micro800Client.GetTagValue2("i0.2");

                if ((bool?)_micro800Client.GetTagValue2("i0.3") != i03_old)
                {
                    i03 = (bool?)_micro800Client.GetTagValue2("i0.3");
                }
                i03_old = (bool?)_micro800Client.GetTagValue2("i0.3");

                if ((bool?)_micro800Client.GetTagValue2("i0.4") != i04_old)
                {
                    i04 = (bool?)_micro800Client.GetTagValue2("i0.4");
                }
                i04_old = (bool?)_micro800Client.GetTagValue2("i0.4");

                if ((bool?)_micro800Client.GetTagValue2("i0.5") != i05_old)
                {
                    i05 = (bool?)_micro800Client.GetTagValue2("i0.5");
                }
                i05_old = (bool?)_micro800Client.GetTagValue2("i0.5");

                if ((bool?)_micro800Client.GetTagValue2("i0.6") != i06_old)
                {
                    i06 = (bool?)_micro800Client.GetTagValue2("i0.6");
                }
                i06_old = (bool?)_micro800Client.GetTagValue2("i0.6");

                if ((bool?)_micro800Client.GetTagValue2("i0.7") != i07_old)
                {
                    i07 = (bool?)_micro800Client.GetTagValue2("i0.7");
                }
                i07_old = (bool?)_micro800Client.GetTagValue2("i0.7");

                //ANALOG DC
                if ((ushort?)_micro800Client.GetTagValue2("analog") != analog_old)
                {
#pragma warning disable CS8605 // Unboxing a possibly null value.
                    Analog = Math.Round((((ushort)_micro800Client.GetTagValue2("analog")) * 0.0025 - 0.5), 4);
#pragma warning restore CS8605 // Unboxing a possibly null value.
                }
                analog_old = (ushort?)_micro800Client.GetTagValue2("analog");
            });
            ReadData.Start();
            await ReadData;
            
        }

        public Task<bool> PingHost(string nameOrAddress)
#pragma warning restore CA1822 // Mark members as static
        {
            bool pingable = false;
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            Ping pinger = null;
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

            try
            {
                pinger = new Ping();
                PingReply reply = pinger.Send(nameOrAddress);
                pingable = reply.Status == IPStatus.Success;
            }
            catch (PingException)
            {
                // Discard PingExceptions and return false;
            }
            finally
            {
                if (pinger != null)
                {
                    pinger.Dispose();
                }
            }

            return Task.FromResult(pingable);
        }
        public ObservableCollection<ISeries> Series { get; set; }

        public LiveChartsCore.SkiaSharpView.Axis[] XAxes { get; set; }

        public object Sync { get; } = new object();

        public bool IsReading { get; set; } = true;

        private async Task ReadData()
        {
            // to keep this sample simple, we run the next infinite loop 
            // in a real application you should stop the loop/task when the view is disposed 

            while (IsReading)
            {
                await Task.Delay(100);

                // Because we are updating the chart from a different thread 
                // we need to use a lock to access the chart data. 
                // this is not necessary if your changes are made in the UI thread. 
                lock (Sync)
                {
                    _values.Add(new DateTimePoint(DateTime.Now, MotorSpeed));
                    if (_values.Count > 250) _values.RemoveAt(0);

                    // we need to update the separators every time we add a new point 
                    _customAxis.CustomSeparators = GetSeparators();
                }
            }

        }
        private double[] GetSeparators()
        {
            var now = DateTime.Now;

            return new double[]
            {
            now.AddSeconds(-25).Ticks,
            now.AddSeconds(-20).Ticks,
            now.AddSeconds(-15).Ticks,
            now.AddSeconds(-10).Ticks,
            now.AddSeconds(-5).Ticks,
            now.Ticks
            };
        }

        private static string Formatter(DateTime date)
        {
            var secsAgo = (DateTime.Now - date).TotalSeconds;

            return secsAgo < 1
                ? "now"
                : $"{secsAgo:N0}s ago";
        }

        public async void Connect()
        {
            Isbusy = true;
            await _micro800Client.Connect();
            if (IsConnected)
            {
                CheckConnected.Start();
                _timer.Enabled = true;
                Status = true;
                IsON = false;

            }
            else
            {
                _timer.Enabled = false;
                Status = false;
                IsON = true;
            }
            Isbusy = false;
        }
        public void Disconnect()
        {
            _micro800Client.Disconnect();
            _timer.Enabled = false;
            Status = false;
            IsON = true;
        }

        //TrafficLights
        public async void Start_TrafficLight()
        {
            Task write = new(() =>
            {
                _micro800Client.WritePLC(_micro800Client.GetTagAddress("start_trafficlight"), true);
                Thread.Sleep(1000);
                _micro800Client.WritePLC(_micro800Client.GetTagAddress("start_trafficlight"), false);
            });
            write.Start();
            await write;
        }

        public async void Stop_TrafficLight()
        {

            Task write = new(() =>
            {
                _micro800Client.WritePLC(_micro800Client.GetTagAddress("stop_trafficlight"), true);
                Thread.Sleep(1000);
                _micro800Client.WritePLC(_micro800Client.GetTagAddress("stop_trafficlight"), false);
            });
            write.Start();
            await write;
        }

        public async void ConfirmTrafficLight()
        {
            Task write = new(() =>
            {
#pragma warning disable CS8629 // Nullable value type may be null.
                _micro800Client.WriteNumberPLC(_micro800Client.GetTagAddress("edit_redled"), (UInt16)Edit_RedLed);
#pragma warning restore CS8629 // Nullable value type may be null.
#pragma warning disable CS8629 // Nullable value type may be null.
                _micro800Client.WriteNumberPLC(_micro800Client.GetTagAddress("edit_yellowled"), (UInt16)Edit_YellowLed);
#pragma warning restore CS8629 // Nullable value type may be null.
#pragma warning disable CS8629 // Nullable value type may be null.
                _micro800Client.WriteNumberPLC(_micro800Client.GetTagAddress("edit_greenled"), (UInt16)Edit_GreenLed);
#pragma warning restore CS8629 // Nullable value type may be null.
                _micro800Client.WritePLC(_micro800Client.GetTagAddress("confirm_trafficlight"), true);
                Thread.Sleep(500);
                _micro800Client.WritePLC(_micro800Client.GetTagAddress("confirm_trafficlight"), false);
            });
            write.Start();
            await write;
        }

        //Inverter

        public async void Start_Inverter()
        {

            Task write = new(() =>
            {
                _micro800Client.WritePLC(_micro800Client.GetTagAddress("start_inverter"), true);
                Thread.Sleep(1000);
                _micro800Client.WritePLC(_micro800Client.GetTagAddress("start_inverter"), false);
            });
            write.Start();
            await write;
        }

        public async void Stop_Inverter()
        {

            Task write = new(() =>
            {
                _micro800Client.WritePLC(_micro800Client.GetTagAddress("stop_inverter"), true);
                Thread.Sleep(1000);
                _micro800Client.WritePLC(_micro800Client.GetTagAddress("stop_inverter"), false);
            });
            write.Start();
            await write;
        }

        public async void Forward_Inverter()
        {
            Task write = new(() =>
            {
                _micro800Client.WritePLC(_micro800Client.GetTagAddress("forward"), true);
                Thread.Sleep(1000);
                _micro800Client.WritePLC(_micro800Client.GetTagAddress("forward"), false);
            });
            write.Start();
            await write;
        }

        public async void Reverse_Inverter()
        {
            Task write = new(() =>
            {
                _micro800Client.WritePLC(_micro800Client.GetTagAddress("reverse"), true);
                Thread.Sleep(1000);
                _micro800Client.WritePLC(_micro800Client.GetTagAddress("reverse"), false);
            });
            write.Start();
            await write;
        }

        public async void WriteMotorSetpoint()
        {
            //_micro800Client.WriteNumberPLC(_micro800Client.GetTagAddress("setpoint"), (float)MotorSetpointWrite);

            Task write = new(() =>
            {
                _micro800Client.WritePLC(_micro800Client.GetTagAddress("confirm_inverter"), true);
                Thread.Sleep(1000);
                _micro800Client.WritePLC(_micro800Client.GetTagAddress("confirm_inverter"), false);
            });
            write.Start();
            await write;
        }
    }
}
