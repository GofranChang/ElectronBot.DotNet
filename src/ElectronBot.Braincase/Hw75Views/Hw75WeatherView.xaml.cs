using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using ViewModels;
using ElectronBot.Braincase.ViewModels;
using ElectronBot.Braincase;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Hw75Views
{
    public sealed partial class Hw75WeatherView : UserControl
    {
        public Hw75WeatherViewModel ViewModel
        {
            get;
        }
        public Hw75WeatherView()
        {
            this.InitializeComponent();

            ViewModel = App.GetService<Hw75WeatherViewModel>();
        }
    }
}
