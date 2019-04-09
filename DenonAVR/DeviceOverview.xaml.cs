using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace DenonAVR
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class DeviceOverview : Page
	{
		public DeviceOverview()
		{
			this.InitializeComponent();

			SetConnectionStatus("Not Connected", Colors.Red);

			ButtonValidate.Click += ButtonValidate_Click;
			SliderVolume.ValueChanged += SliderVolume_ValueChanged;

			Device.Instance.OnGetTaskCompleted += Instance_OnGetTaskCompleted;

			EnableDeviceControls(false);
		}

		private void SliderVolume_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
		{
			Device.Instance.SetVolume((int)(e.NewValue), 1);
		}

		~DeviceOverview()
		{
			Device.Instance.OnGetTaskCompleted -= Instance_OnGetTaskCompleted;
		}

		private void Instance_OnGetTaskCompleted(object sender, TaskWrapper<string> e)
		{
			if (e.taskType == TaskType.GET_DEVICE_INFO)
			{
				if (e.task.IsCompleted && Device.Instance.LastDeviceInfoOK)
				{
					SetConnectionStatus("Connected to: " + Device.Instance.ModelName, Colors.LimeGreen);
					EnableDeviceControls(true);
				}
				else
					SetConnectionStatus("Could not retrieve device info", Colors.Red);

				EnableSetupControls(true);
			}
		}

		private void ButtonValidate_Click(object sender, RoutedEventArgs e)
		{
			if (String.IsNullOrEmpty(TextIP.Text))
			{
				SetConnectionStatus("Please enter an IP Address", Colors.Red);
				return;
			}

			IPAddress addr;
			if (!IPAddress.TryParse(TextIP.Text, out addr))
			{
				SetConnectionStatus("Failed to parse IP Address", Colors.Red);
				return;
			}

			SetConnectionStatus("Connecting...", Colors.White, true);
			EnableSetupControls(false);
			EnableDeviceControls(false);

			Device.Instance.SetIPAddress(addr);
			Device.Instance.Connect();
		}

		private void EnableSetupControls(bool Flag)
		{
			TextIP.IsEnabled = Flag;
			ButtonValidate.IsEnabled = Flag;
		}

		private void EnableDeviceControls(bool Flag)
		{
			SliderVolume.IsEnabled = Flag;
		}

		private void SetConnectionStatus(string text, Color color, bool showSpinner = false)
		{
			ConnectionStatusRoot.Children.Clear();

			if (showSpinner)
			{
				ProgressRing ring = new ProgressRing();
				ring.IsActive = true;
				ring.Foreground = new SolidColorBrush(Colors.White);
				ring.Margin = new Thickness(0, 0, 10, 0);
				ConnectionStatusRoot.Children.Add(ring);
			}

			TextBlock textBlock = new TextBlock();
			textBlock.Foreground = new SolidColorBrush(color);
			textBlock.Text = text;
			ConnectionStatusRoot.Children.Add(textBlock);
		}
	}
}
