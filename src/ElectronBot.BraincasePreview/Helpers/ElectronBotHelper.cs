﻿using System.IO.Ports;
using System.Text.RegularExpressions;
using ElectronBot.BraincasePreview.Contracts.Services;
using ElectronBot.DotNet;
using Microsoft.Extensions.Logging;
using Verdure.ElectronBot.Core.Models;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Foundation;
using Windows.Media.Playback;
using Windows.Media.SpeechRecognition;

namespace ElectronBot.BraincasePreview.Helpers;

public class ElectronBotHelper
{
    public IElectronLowLevel? ElectronBot
    {
        get; set;
    }
    private ElectronBotHelper()
    {
    }
    private static ElectronBotHelper? _instance;
    public static ElectronBotHelper Instance => _instance ??= new ElectronBotHelper();

    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    private readonly Dictionary<string, string> _electronDic = new();

    private DeviceWatcher? deviceWatcher;

    public event EventHandler? ClockCanvasStop;

    private MediaPlayer mediaPlayer = new();

    private bool isTTS = false;

    public bool EbConnected
    {
        get; set;
    }

    public void InvokeClockCanvasStop()
    {
        ClockCanvasStop?.Invoke(this, new EventArgs());
    }

    public SerialPort SerialPort { get; set; } = new SerialPort();

    public async Task InitAsync()
    {
        // Target all Serial Devices present on the system
        var deviceSelector = SerialDevice.GetDeviceSelector();

        var myDevices = await DeviceInformation.FindAllAsync(deviceSelector);

        deviceWatcher = DeviceInformation.CreateWatcher(deviceSelector);

        deviceWatcher.Added += new TypedEventHandler<DeviceWatcher, DeviceInformation>(OnDeviceAdded);
        deviceWatcher.Removed += new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>(OnDeviceRemoved);

        deviceWatcher.Start();

        mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
    }

    public void PlayEmoticonActionFrame(EmoticonActionFrame frame)
    {
        if (EbConnected)
        {
            try
            {
                if (frame != null)
                {
                    if (ElectronBot is not null)
                    {
                        ElectronBot.SetImageSrc(frame.FrameBuffer);
                        ElectronBot.SetJointAngles(frame.J1, frame.J2, frame.J3, frame.J4, frame.J5, frame.J6, frame.Enable);
                        ElectronBot.Sync();
                    }
                }
            }
            catch (Exception)
            {
                return;
            }

        }
    }
    private Task ConnectDeviceAsync()
    {
        _context?.Post(async _ =>
        {
            ToastHelper.SendToast("ElectronBotAddTip".GetLocalized(), TimeSpan.FromSeconds(3));

            await Task.Delay(500);
        }, null);

        return Task.CompletedTask;
    }

    private Task DisconnectDeviceAsync()
    {
        _context?.Post(async _ =>
        {
            ToastHelper.SendToast("ElectronBotRemoveTip".GetLocalized(), TimeSpan.FromSeconds(3));
            await Task.Delay(500);
        }, null);

        return Task.CompletedTask;
    }

    private async void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        try
        {
            if (ElectronBot is not null)
            {
                ElectronBot.Disconnect();

                ElectronBot = null;
            }

            if (SerialPort.IsOpen)
            {
                SerialPort.Close();
            }

        }
        catch (Exception)
        {

        }

        EbConnected = false;

        await DisconnectDeviceAsync();
        //if (_electronDic.TryGetValue(args.Id, out var value))
        //{
        //    if (value != null)
        //    {
        //        _electronDic.Remove(args.Id);

        //        if (ElectronBot is not null)
        //        {
        //            ElectronBot.Disconnect();

        //            ElectronBot = null;                   
        //        }

        //        if (SerialPort.IsOpen)
        //        {
        //            SerialPort.Close();
        //        }

        //        EbConnected = false;

        //        await DisconnectDeviceAsync();
        //    }
        //};
    }

    private async void OnDeviceAdded(DeviceWatcher sender, DeviceInformation args)
    {
        if (args.Name.Contains("CP210"))
        {
            var comName = Regex.Replace(args.Name, @"(.*\()(.*)(\).*)", "$2"); //小括号()

            SerialPort.PortName = comName;

            SerialPort.BaudRate = 115200;

            try
            {
                if (ElectronBot is not null)
                {
                    ElectronBot.Disconnect();

                    ElectronBot = null;
                }

                //SerialPort.Open();


                //_electronDic.Add(args.Id, args.Name);

                ElectronBot = new ElectronLowLevel(App.GetService<ILogger<ElectronLowLevel>>());

                EbConnected = ElectronBot.Connect();

                await ConnectDeviceAsync();
            }
            catch (Exception ex)
            {
                _context?.Post(async _ =>
                {
                    ToastHelper.SendToast($"串口打开异常：{ex.Message}", TimeSpan.FromSeconds(3));
                    await Task.Delay(500);
                }, null);

                return;
            }
        }
    }

    /// <summary>
    /// 播放表情声音
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public async Task MediaPlayerPlaySoundAsync(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                var localSettingsService = App.GetService<ILocalSettingsService>();

                var audioModel = await localSettingsService
                    .ReadSettingAsync<ComboxItemModel>(Constants.DefaultAudioNameKey);

                var audioDevs = await EbHelper.FindAudioDeviceListAsync();

                if (audioModel != null)
                {
                    var audioSelect = audioDevs.FirstOrDefault(c => c.DataValue == audioModel.DataValue) ?? new ComboxItemModel();

                    var selectedDevice = (DeviceInformation)audioSelect.Tag!;

                    if (selectedDevice != null)
                    {
                        mediaPlayer.AudioDevice = selectedDevice;
                    }
                }
                mediaPlayer.SetUriSource(new Uri(path));
                mediaPlayer.Play();
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>
    /// 播放声音
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    public async Task MediaPlayerPlaySoundByTTSAsync(string content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                var localSettingsService = App.GetService<ILocalSettingsService>();

                var audioModel = await localSettingsService
                    .ReadSettingAsync<ComboxItemModel>(Constants.DefaultAudioNameKey);

                var audioDevs = await EbHelper.FindAudioDeviceListAsync();

                if (audioModel != null)
                {
                    var audioSelect = audioDevs.FirstOrDefault(c => c.DataValue == audioModel.DataValue) ?? new ComboxItemModel();

                    var selectedDevice = (DeviceInformation)audioSelect.Tag!;

                    if (selectedDevice != null)
                    {
                        mediaPlayer.AudioDevice = selectedDevice;
                    }
                }

                var speechAndTTSService = App.GetService<ISpeechAndTTSService>();

                var stream = await speechAndTTSService.TextToSpeechAsync(content);

                mediaPlayer.SetStreamSource(stream);
                mediaPlayer.Play();
                isTTS = true;
            }
            catch (Exception)
            {
            }
        }
    }

    private async void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
    {
        if (isTTS)
        {
            var speechAndTTSService = App.GetService<ISpeechAndTTSService>();
            await speechAndTTSService.InitializeRecognizerAsync(SpeechRecognizer.SystemSpeechLanguage);

            await speechAndTTSService.StartAsync();
            isTTS = false;
        }
    }
}
