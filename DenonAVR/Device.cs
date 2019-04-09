using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace DenonAVR
{
	public enum TaskType
	{
		GET_DEVICE_INFO,
		MUTE,
		VOLUME,
	}

	public class Command
	{
		public string Key;
		public TaskType Type;

		public Command(string Key, TaskType Type)
		{
			this.Key = Key;
			this.Type = Type;
		}
	}

	public enum CommandFlags
	{
		NONE = 0,
		REQUEST = (1 << 0),
	}

	public static class Commands
	{
		public static Command CMD_VOLUME = new Command("", TaskType.VOLUME);
		public static Command CMD_MUTE = new Command("MU", TaskType.MUTE);
	}

	public class TaskWrapper<T>
	{
		public event EventHandler<TaskWrapper<T>> OnCompleted;
		public Task<T> task;
		public TaskType taskType;
		public bool isRequest;

		public TaskWrapper(Task<T> task, TaskType taskType)
		{
			this.task = task;
			this.taskType = taskType;
			this.isRequest = false;
			this.task.GetAwaiter().OnCompleted(OnTaskCompleted);
		}

		private void OnTaskCompleted()
		{
			OnCompleted(this, this);
		}
	}

	public class TaskList<T>
	{
		public event EventHandler<TaskWrapper<T>> OnTaskCompleted;

		public List<TaskWrapper<T>> list;

		public TaskList()
		{
			list = new List<TaskWrapper<T>>();
		}

		public void BindTask(Task<T> task, TaskType type, bool isRequest = false)
		{
			TaskWrapper<T> wrap = new TaskWrapper<T>(task, type);
			wrap.isRequest = isRequest;
			wrap.OnCompleted += OnTaskCompleted;
			wrap.OnCompleted += RemoveTask;
			list.Add(wrap);
		}

		private void RemoveTask(Object sender, TaskWrapper<T> obj)
		{
			list.Remove(obj);
		}
	}

	public class Device
	{
		public static Device Instance = new Device();

		public string ModelName { get; set; }
		public bool LastDeviceInfoOK { get; set; }

		private int Volume;

		HttpClient client;
		IPAddress ip;
		TaskList<string> tasksGet;
		TaskList<HttpResponseMessage> tasksPost;

		public event EventHandler<TaskWrapper<string>> OnGetTaskCompleted;

		public Device()
		{
			client = new HttpClient();
			tasksGet = new TaskList<string>();
			tasksGet.OnTaskCompleted += HandleTaskComplete;
			tasksPost = new TaskList<HttpResponseMessage>();
			tasksPost.OnTaskCompleted += TasksPost_OnTaskCompleted;
			LastDeviceInfoOK = false;
		}

		public void SetIPAddress(IPAddress ipAddress)
		{
			ip = ipAddress;
		}

		public void Connect()
		{
			var task = client.GetStringAsync(string.Format("http://{0}:8080/goform/Deviceinfo.xml", ip));
			tasksGet.BindTask(task, TaskType.GET_DEVICE_INFO);
		}

		public void RefreshDeviceStats()
		{
			CmdVolume("?", 1, true);
		}

		public void Mute()
		{
			string cmd = "MUON";
			HttpContent msg = new ByteArrayContent(System.Text.UTF8Encoding.ASCII.GetBytes(cmd));
			client.PostAsync(string.Format("http://{0}:8080/goform/formiPhoneAppDirect.xml?MUOFF", ip), null);
		}

		public void SetVolume(int newVolume, int zone) { CmdVolume(ToDenonValue(newVolume), zone, false); }
		private void CmdVolume(string state, int zone, bool isRequest)
		{
			String zonePrefix;
			switch (zone)
			{
				case 1:
					zonePrefix = "MV";
					break;
				case 2:
				case 3:
					zonePrefix = "Z" + zone;
					break;
				default:
					throw new Exception("Zone must be in range [1-3], zone: " + zone);
			}

			SendCommand(Commands.CMD_VOLUME, zonePrefix + state, isRequest);
		}

		private string ToDenonValue(int number)
		{
			string str = "";

			if (number < 10)
				str += '0';

			str += number;

			return str;
		}

		private void BindGetTask(Task<string> task, TaskType type)
		{
			tasksGet.BindTask(task, type);
		}

		private void SendCommand(TaskType type, string cmd, bool isRequest = false)
		{
			string url = string.Format("http://{0}:8080/goform/formiPhoneAppDirect.xml?" + cmd, ip);

				var task = client.PostAsync(url, null);
				tasksPost.BindTask(task, type, isRequest);
		}

		private void SendCommand(Command command, string commandState, bool isRequest = false)
		{
			SendCommand(command.Type, command.Key + commandState, isRequest);
		}

		private void HandleTaskComplete(Object sender, TaskWrapper<string> task)
		{
			if (task.taskType == TaskType.GET_DEVICE_INFO)
			{
				if (task.task.Status == TaskStatus.RanToCompletion)
					ParseAndUpdateDeviceStatus(task.task.Result);
				else
					LastDeviceInfoOK = false;
			}

			OnGetTaskCompleted(this, task);
		}

		private void TasksPost_OnTaskCompleted(object sender, TaskWrapper<HttpResponseMessage> e)
		{
			if (e.isRequest)
			{
				Task<string> responseTask = e.task.Result.Content.ReadAsStringAsync();
				responseTask.Wait();
			}
		}

		private void ParseAndUpdateDeviceStatus(string xmlStr)
		{
			XmlDocument xml = new XmlDocument();
			xml.LoadXml(xmlStr);

			try
			{
				ModelName = xml["Device_Info"]["ModelName"].InnerText;
				LastDeviceInfoOK = true;
			}
			catch
			{
				LastDeviceInfoOK = false;
			}
		}
	}
}
