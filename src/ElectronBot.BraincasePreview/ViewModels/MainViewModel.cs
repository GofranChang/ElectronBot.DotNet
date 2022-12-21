﻿using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElectronBot.BraincasePreview.Contracts.Services;
using ElectronBot.BraincasePreview.Contracts.ViewModels;
using ElectronBot.BraincasePreview.Helpers;
using ElectronBot.BraincasePreview.Models;
using ElectronBot.BraincasePreview.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Services;
using Verdure.ElectronBot.Core.Models;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechRecognition;
using Windows.Storage;

namespace ElectronBot.BraincasePreview.ViewModels;

public partial class MainViewModel : ObservableRecipient, INavigationAware
{
    private readonly DispatcherTimer _dispatcherTimer;

    private readonly IClockViewProviderFactory _viewProviderFactory;

    private readonly IActionExpressionProvider _actionExpressionProvider;

    private readonly IActionExpressionProviderFactory _expressionProviderFactory;

    private readonly ISpeechAndTTSService _speechAndTTSService;

    private readonly ILocalSettingsService _localSettingsService;

    private int modeNo = 0;

    private int count = 0;

    private int actionCount = 0;

    private readonly MediaPlayer _mediaPlayer;

    SoftwareBitmap? frameServerDest = null;

    CanvasImageSource? canvasImageSource = null;

    private readonly EmoticonActionFrameService _emoticonActionFrameService;

    private readonly IntPtr _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
    public MainViewModel(
        ILocalSettingsService localSettingsService,
        IClockViewProviderFactory viewProviderFactory,
        ComboxDataService comboxDataService,
        DispatcherTimer dispatcherTimer,
        ObjectPickerService objectPickerService,
        MediaPlayer mediaPlayer,
        IActionExpressionProviderFactory actionExpressionProviderFactory,
        ISpeechAndTTSService speechAndTTSService,
        EmoticonActionFrameService emoticonActionFrameService)
    {
        _emoticonActionFrameService = emoticonActionFrameService;

        _localSettingsService = localSettingsService;

        _dispatcherTimer = dispatcherTimer;

        _speechAndTTSService = speechAndTTSService;

        _dispatcherTimer.Interval = new TimeSpan(0, 0, 1);

        _dispatcherTimer.Tick += DispatcherTimer_Tick;

        _viewProviderFactory = viewProviderFactory;

        _expressionProviderFactory = actionExpressionProviderFactory;

        ClockComboxModels = comboxDataService.GetClockViewComboxList();

        _mediaPlayer = mediaPlayer;

        _mediaPlayer.VideoFrameAvailable += MediaPlayer_VideoFrameAvailable;

        _mediaPlayer.IsVideoFrameServerEnabled = true;

        var defaultProvider = _expressionProviderFactory.CreateActionExpressionProvider("Default");

        _actionExpressionProvider = defaultProvider;

        ElectronBotHelper.Instance.SerialPort.DataReceived += SerialPort_DataReceived;

        ElectronBotHelper.Instance.ClockCanvasStop += Instance_ClockCanvasStop;
    }

    private void Instance_ClockCanvasStop(object? sender, EventArgs e)
    {
        _dispatcherTimer.Stop();
    }

    [ObservableProperty]
    int selectIndex;

    [ObservableProperty]
    int interval;

    /// <summary>
    /// 时钟选中数据
    /// </summary>
    [ObservableProperty]
    ComboxItemModel clockComBoxSelect;

    /// <summary>
    /// 表盘列表
    /// </summary>
    [ObservableProperty]
    public ObservableCollection<ComboxItemModel> clockComboxModels;

    /// <summary>
    /// 当前播放表情
    /// </summary>
    [ObservableProperty]
    ImageSource emojiImageSource;

    /// <summary>
    /// 表盘内容
    /// </summary>
    [ObservableProperty]
    UIElement element;



    [RelayCommand]
    private void RebootElectron()
    {
        try
        {
            if (!ElectronBotHelper.Instance.SerialPort.IsOpen)
            {
                ElectronBotHelper.Instance.SerialPort.Open();
            }

            var byteData = new byte[]
            {
                0xea, 0x00, 0x00, 0x00, 0x00 ,0x0d, 0x02, 0x00 , 0x00, 0x0f, 0xea
            };

            ElectronBotHelper.Instance.SerialPort.Write(byteData, 0, byteData.Length);

            Thread.Sleep(1000);

            if (ElectronBotHelper.Instance.SerialPort.IsOpen)
            {
                ElectronBotHelper.Instance.SerialPort.Close();
            }

        }
        catch (Exception)
        {
        }
    }

    [RelayCommand]
    private async void TestVoice()
    {
        var textList = new List<string>()
        {
            "哥哥你好啊",
            "哥哥在干嘛",
            "哥哥想我没",
            "哥哥最好啦",
            "最喜欢哥哥啦",
            "人家好想哥哥",
            "哥哥喜欢妹妹不"
        };

        var r = new Random().Next(textList.Count);

        var text = textList[r];

        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            ToastHelper.SendToast(text, TimeSpan.FromSeconds(2));
        });

        await ElectronBotHelper.Instance.MediaPlayerPlaySoundByTTSAsync(text);


        //var stream = await _speechAndTTSService.TextToSpeechAsync(text);

        //_mediaPlayer.SetStreamSource(stream);

        //var selectedDevice = (DeviceInformation)AudioSelect?.Tag;

        //if (selectedDevice != null)
        //{
        //    _mediaPlayer.AudioDevice = selectedDevice;
        //}

        //_mediaPlayer.Play();

        //var ret = RuntimeHelper.IsAdminRun();

        //App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        //{
        //    ToastHelper.SendToast($"是否在管理权权限运行：{ret}", TimeSpan.FromSeconds(2));
        //});
    }


    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        SerialPort sp = (SerialPort)sender;
        var indata = sp.ReadExisting();
        Debug.WriteLine("Data Received:");
        Debug.Write(indata);

        if (indata.Contains("Clockwise"))
        {
            var r = new Random().Next(Constants.POTENTIAL_EMOJI_LIST.Count);

            var mediaPlayer = App.GetService<MediaPlayer>();

            mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;

            mediaPlayer.Source = MediaSource.CreateFromUri(new Uri($"ms-appx:///Assets/Emoji/{Constants.POTENTIAL_EMOJI_LIST[r]}.mp4"));

            //var selectedDevice = (DeviceInformation)AudioSelect?.Tag;

            //if (selectedDevice != null)
            //{
            //    mediaPlayer.AudioDevice = selectedDevice;
            //}

            mediaPlayer.Play();
        }
    }

    private async void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
    {
        await _speechAndTTSService.InitializeRecognizerAsync(SpeechRecognizer.SystemSpeechLanguage);

        await _speechAndTTSService.StartAsync();
    }

    [RelayCommand]
    private void TestPlayEmoji()
    {
        try
        {
            _dispatcherTimer.Stop();

            var r = new Random().Next(Constants.POTENTIAL_EMOJI_LIST.Count);

            _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri($"ms-appx:///Assets/Emoji/{Constants.POTENTIAL_EMOJI_LIST[r]}.mp4"));

            //var selectedDevice = (DeviceInformation)AudioSelect?.Tag;

            //if (selectedDevice != null)
            //{
            //    _mediaPlayer.AudioDevice = selectedDevice;
            //}
            _mediaPlayer.Play();

            _actionExpressionProvider.PlayActionExpressionAsync($"{Constants.POTENTIAL_EMOJI_LIST[r]}", actions.ToList());
        }
        catch (Exception)
        {

        }
    }

    /// <summary>
    /// 媒体播放帧处理事件
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void MediaPlayer_VideoFrameAvailable(MediaPlayer sender, object args)
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                var canvasDevice = App.GetService<CanvasDevice>();

                if (frameServerDest == null)
                {
                    // FrameServerImage in this example is a XAML image control
                    frameServerDest =
                        new SoftwareBitmap(BitmapPixelFormat.Rgba8, 240, 240, BitmapAlphaMode.Ignore);

                }
                if (canvasImageSource == null)
                {
                    canvasImageSource = new CanvasImageSource(canvasDevice, 240, 240, 96);//96); 

                    EmojiImageSource = canvasImageSource;

                }

                using var inputBitmap = CanvasBitmap.CreateFromSoftwareBitmap(canvasDevice, frameServerDest);

                using var ds = canvasImageSource.CreateDrawingSession(Microsoft.UI.Colors.Black);

                _mediaPlayer.CopyFrameToVideoSurface(inputBitmap);

                ds.DrawImage(inputBitmap);
            }
            catch (Exception ex)
            {

            }
        });
    }

    /// <summary>
    /// 表盘切换方法
    /// </summary>
    [RelayCommand]
    private void ClockChanged()
    {
        var clockName = clockComBoxSelect?.DataKey;

        if (!string.IsNullOrWhiteSpace(clockName))
        {
            var service = App.GetService<EmoticonActionFrameService>();

            service.ClearQueue();

            var viewProvider = _viewProviderFactory.CreateClockViewProvider(clockName);

            if (clockName == "GooeyFooter")
            {
                _dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 40);
            }
            else
            {
                _dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            }

            Element = viewProvider.CreateClockView(clockName);
        }
    }


    /// <summary>
    /// 头部舵机
    /// </summary>
    [ObservableProperty]
    float j1;

    /// <summary>
    /// 左臂展开
    /// </summary>
    [ObservableProperty]
    float j2;
    /// <summary>
    /// 左臂旋转
    /// </summary>
    [ObservableProperty]
    float j3;
    /// <summary>
    /// 右臂展开
    /// </summary>
    [ObservableProperty]
    float j4;
    /// <summary>
    /// 右臂旋转
    /// </summary>
    [ObservableProperty]
    float j5;
    /// <summary>
    /// 底盘转动
    /// </summary>
    [ObservableProperty]
    float j6;


    /// <summary>
    /// 定时器处理
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void DispatcherTimer_Tick(object? sender, object e)
    {
        if (modeNo == 2)
        {
            if (ElectronBotHelper.Instance.EbConnected)
            {
                await EbHelper.ShowClockCanvasToDeviceAsync(Element);
            }
        }
        else if (modeNo == 3)
        {
            if (ElectronBotHelper.Instance.EbConnected)
            {
                var data = new byte[240 * 240 * 3];

                var frame = new EmoticonActionFrame(data);

                ElectronBotHelper.Instance.PlayEmoticonActionFrame(frame);

                var jointAngles = ElectronBotHelper.Instance?.ElectronBot?.GetJointAngles();

                if (jointAngles != null)
                {
                    var actionData = new ElectronBotAction()
                    {
                        Id = Guid.NewGuid().ToString(),
                        J1 = (int)jointAngles[0],
                        J2 = (int)jointAngles[1],
                        J3 = (int)jointAngles[2],
                        J4 = (int)jointAngles[3],
                        J5 = (int)jointAngles[4],
                        J6 = (int)jointAngles[5]
                    };

                    Actions.Add(actionData);
                }
            }
        }
    }

    public void Head_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (ElectronBotHelper.Instance.EbConnected && modeNo == 1)
        {
            Task.Run(() =>
            {
                if (ElectronBotHelper.Instance.EbConnected)
                {
                    var data = new byte[240 * 240 * 3];

                    var frame = new EmoticonActionFrame(data, true, j1, j2, j3, j4, j5, j6);

                    ElectronBotHelper.Instance.PlayEmoticonActionFrame(frame);
                }
            });
        }
    }

    public async void RadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var radioButtons = (RadioButtons)sender;

        var service = App.GetService<EmoticonActionFrameService>();

        service.ClearQueue();

        var list = radioButtons.Items;

        List<RadioButton> rbList = new();

        if (list != null && list.Count > 0)
        {
            foreach (var item in list)
            {
                rbList.Add((RadioButton)item);
            }
        }

        var index = rbList.IndexOf(rbList.Where(l => l.IsChecked == true).FirstOrDefault());

        if (index > -1)
        {
            modeNo = index;
        }

        if (index == 2)
        {
            if (!ElectronBotHelper.Instance.EbConnected)
            {
                ToastHelper.SendToast("PleaseConnectToastText".GetLocalized(), TimeSpan.FromSeconds(3));
            }
            else
            {
                await ResetActionAsync();

                var clockName = clockComBoxSelect?.DataKey;

                if (clockName != "GooeyFooter")
                {
                    _dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
                }

                _dispatcherTimer.Start();
            }
        }
        else if (index == 3)
        {
            if (!ElectronBotHelper.Instance.EbConnected)
            {
                ToastHelper.SendToast("PleaseConnectToastText".GetLocalized(), TimeSpan.FromSeconds(3));
            }
            else
            {
                await ResetActionAsync();
                _dispatcherTimer.Interval = TimeSpan.FromMilliseconds(Interval);
                _dispatcherTimer.Start();
            }
        }
        else
        {
            _dispatcherTimer.Stop();
        }
    }

    [ObservableProperty]
    ElectronBotAction selectdAction = new();

    [ObservableProperty]
    ObservableCollection<ElectronBotAction> actions = new();

    /// <summary>
    /// 导入动作列表
    /// </summary>
    [RelayCommand]
    public async Task ImportAsync()
    {
        var list = await EbHelper.ImportActionListAsync(_hwnd);

        Actions = new ObservableCollection<ElectronBotAction>(list);
    }

    [RelayCommand]
    public async Task PlayAsync()
    {
        if (modeNo == 1)
        {
            if (actions.Count > 0)
            {
                await ResetActionAsync();

                await EbHelper.PlayActionListAsync(Actions.ToList(), Interval);

            }
            else
            {
                ToastHelper.SendToast("PlayEmptyToastText".GetLocalized(), TimeSpan.FromSeconds(3));
            }

        }
        else
        {
            ToastHelper.SendToast("PlayErrorToastText".GetLocalized(), TimeSpan.FromSeconds(3));
        }
    }

    [RelayCommand]
    public void Stop()
    {
        _dispatcherTimer.Stop();
    }

    [RelayCommand]
    public void Clear()
    {
        actions.Clear();

        count = 0;

        actionCount = 0;

        ToastHelper.SendToast("PlayClearToastText".GetLocalized(), TimeSpan.FromSeconds(3));
    }

    [RelayCommand]
    public void Reconnect()
    {
        try
        {
            _dispatcherTimer.Stop();
            //ElectronBotHelper.Instance?.ElectronBot?.Disconnect();
            ElectronBotHelper.Instance?.ElectronBot?.ResetDevice();
        }
        catch (Exception)
        {

        }


        ToastHelper.SendToast("ReconnectText".GetLocalized(), TimeSpan.FromSeconds(3));
    }

    [RelayCommand]
    public async Task ResetAsync()
    {
        if (modeNo == 1)
        {
            if (ElectronBotHelper.Instance.EbConnected)
            {
                await ResetActionAsync();

                ToastHelper.SendToast("PlayResetToastText".GetLocalized(), TimeSpan.FromSeconds(3));
            }
            else
            {
                ToastHelper.SendToast("PleaseConnectToastText".GetLocalized(), TimeSpan.FromSeconds(3));
            }

        }
        else
        {
            ToastHelper.SendToast("PlayErrorToastText".GetLocalized(), TimeSpan.FromSeconds(3));
        }
    }

    private ICommand _pauseCommand;

    public ICommand PauseCommand
    {
        get
        {
            _pauseCommand ??= new RelayCommand(
                    () =>
                    {

                    });

            return _pauseCommand;
        }
    }

    [RelayCommand]
    public async Task ExportAsync()
    {
        StorageFolder destinationFolder = null;

        try
        {
            destinationFolder = await KnownFolders.PicturesLibrary
            .CreateFolderAsync("ElectronBot", CreationCollisionOption.OpenIfExists);
        }
        catch (Exception ex)
        {
            return;
        }

        if (Actions != null && Actions.Count > 0)
        {
            var fileName = $"electronbot-action-{DateTime.Now:yyyy-MM-dd-hh-mm-ss}.json";

            var destinationFile = await destinationFolder
                .CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

            var content = JsonSerializer
                .Serialize(Actions, options: new JsonSerializerOptions { WriteIndented = true });

            await FileIO.WriteTextAsync(destinationFile, content);

            var text = "ExportToastText".GetLocalized();

            ToastHelper.SendToast($"{text}-{destinationFile.Path}", TimeSpan.FromSeconds(5));
        }
        else
        {
            ToastHelper.SendToast("PlayEmptyToastText".GetLocalized(), TimeSpan.FromSeconds(3));
        }
    }

    [RelayCommand]
    public void Add()
    {
        if (SelectIndex < 0)
        {
            SelectIndex = 0;
        }
        else if (SelectIndex > Actions.Count)
        {
            SelectIndex = Actions.Count;
        }

        if (Actions.Count > 0)
        {
            Actions.Insert(SelectIndex + 1, new ElectronBotAction
            {
                J1 = J1,
                J2 = J2,
                J3 = J3,
                J4 = J4,
                J5 = J5,
                J6 = J6
            });
        }
        else
        {
            Actions.Add(new ElectronBotAction
            {
                J1 = J1,
                J2 = J2,
                J3 = J3,
                J4 = J4,
                J5 = J5,
                J6 = J6
            });
        }
    }

    [RelayCommand]
    public void RemoveAction()
    {
        if (SelectIndex < 0)
        {
            SelectIndex = 0;
        }
        else if (SelectIndex > Actions.Count)
        {
            SelectIndex = Actions.Count;
        }

        Actions.RemoveAt(SelectIndex);
    }

    [RelayCommand]
    public async Task AddPictureAsync()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);

        var picker = new Windows.Storage.Pickers.FileOpenPicker
        {
            ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail,

            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads
        };

        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");

        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();

        if (file != null)
        {
            var config = new ImageCropperConfig
            {
                ImageFile = file,
                AspectRatio = 1
            };

            var croppedImage = await ImageHelper.CropImage(config);

            if (croppedImage != null)
            {
                SelectdAction.BitmapImageData = croppedImage;

                var act = Actions.Where(i => i.Id == selectdAction.Id).FirstOrDefault();

                if (act != null)
                {
                    var bytes = croppedImage.PixelBuffer.ToArray();

                    var imageData = await EbHelper.ToBase64Async(
                        bytes, (uint)croppedImage.PixelWidth, (uint)croppedImage.PixelWidth);

                    act.ImageData = $"data:image/png;base64,{imageData}";

                    act.BitmapImageData = croppedImage;
                }
            }
        }
    }

    private async Task ResetActionAsync()
    {
        J1 = 0;
        J2 = 0;
        J3 = 0;
        J4 = 0;
        J5 = 0;
        J6 = 0;

        await Task.Run(() =>
        {
            if (ElectronBotHelper.Instance.EbConnected)
            {
                if (ElectronBotHelper.Instance.EbConnected)
                {
                    var data = new byte[240 * 240 * 3];

                    var frame = new EmoticonActionFrame(data, true);

                    var service = App.GetService<EmoticonActionFrameService>();

                    service.ClearQueue();

                    ElectronBotHelper.Instance.PlayEmoticonActionFrame(frame);
                }
            }
        });
    }

    public async void OnNavigatedTo(object parameter)
    {
        var viewProvider = _viewProviderFactory.CreateClockViewProvider("DefautView");

        Element = viewProvider.CreateClockView("DefautView");

        if (modeNo == 3)
        {
            if (ElectronBotHelper.Instance.EbConnected)
            {
                _dispatcherTimer.Start();
            }
        }
    }

    public void OnNavigatedFrom()
    {
        _dispatcherTimer.Stop();
        // await _speechAndTTSService.ReleaseRecognizerAsync();
    }
}
