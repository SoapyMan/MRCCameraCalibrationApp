//#define ENABLE_MRC_IN_APP
//#define ENABLE_QUEST_STORE

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Xml;
using Oculus.Platform;
using Oculus.Platform.Models;
using UnityEngine;
using UnityEngine.Android;

public class CalibrationNetworkServer : MonoBehaviour
{
	private enum AdjustKey
	{
		None,
		Up,
		Down,
		Left,
		Right,
		Forward,
		Backward,
		PitchIncrease,
		PitchDecrease,
		RollIncrease,
		RollDecrease,
		YawIncrease,
		YawDecrease,
		FovIncrease,
		FovDecrease
	}

	private DirectoryInfo thisAppDir;
	private DirectoryInfo storageDir;
	private List<DirectoryInfo> appDirs;

	public int serverListeningPort = 25671;
	private float timeInterval = 1.0f / 30.0f;

	private OVRNetwork.OVRNetworkTcpServer tcpServer;
	private OVRNetwork.OVRNetworkTcpClient tcpClient;

	private float lastBroadcastTime;
	private int currentBroadcstFrameIndex;
	private int primaryButtonPressedTimes;
	private int secondaryButtonPressedTimes;
	private string userId;

	private MRCNetworkDiscovery mrcNetworkDiscovery;

	private const int DataVersion = 1;

	private const int USER_ID = 31;
	private const int DATA_VERSION = 32;
	private const int POSE_UPDATE = 33;
	private const int PRIMARY_BUTTON_PRESSED = 34;
	private const int SECONDARY_BUTTON_PRESSED = 35;
	private const int CALIBRATION_DATA = 36;
	private const int CLEAR_CALIBRATION = 37;
	private const int OPERATION_COMPLETE = 38;
	private const int STATE_CHANGE_PAUSE = 39;
	private const int ADJUST_KEY = 40;

#if ENABLE_QUEST_STORE
	private void EntitlementCallback(Message msg)
	{
		if (msg.IsError)
		{
			Debug.LogError("You are NOT entitled to use this app, you still may use it");
			Debug.LogError(msg.GetError().Message);
		}
		else
		{
			Debug.Log("You are entitled to use this app.");
		}
	}

	private void Awake()
	{
		try
		{
			Core.AsyncInitialize();
			Entitlements.IsUserEntitledToApplication().OnComplete(EntitlementCallback);
		}
		catch (UnityException exception)
		{
			Debug.LogError("Platform failed to initialize due to exception.");
			Debug.LogException(exception);
			UnityEngine.Application.Quit();
		}
	}

	private void GetLoggedInUserCallback(Message<User> message)
	{
		if (message.IsError)
		{
			Debug.LogError($"[CalibrationNetworkServer] {message.GetError().Message}");
			UnityEngine.Application.Quit();
			return;
		}

		userId = message.Data.ID.ToString();
	}
#endif

	private void Start()
	{
		thisAppDir = new DirectoryInfo(UnityEngine.Application.persistentDataPath);
		storageDir = GetExternalStorageDirectory();
		appDirs = GetApplicationDirectories();

		CheckRequestPermissions();

		Debug.Log("[CalibrationNetworkServer] MRC Calibration Network Server startup...");

		tcpServer = new OVRNetwork.OVRNetworkTcpServer();

		tcpServer.clientConnectedCallback = ClientConnected;
		tcpServer.StartListening(serverListeningPort);

#if ENABLE_MRC_IN_APP
		OVRPlugin.InitializeMixedReality();
#endif
#if ENABLE_QUEST_STORE && UNITY_ANDROID
		Users.GetLoggedInUser().OnComplete(GetLoggedInUserCallback);
#else
		userId = "0";
#endif

		mrcNetworkDiscovery = GameObject.Find("Network Broadcaster").GetComponent<MRCNetworkDiscovery>();
		mrcNetworkDiscovery.StartBroadcast();
	}

	private void OnDestroy()
	{
		if (mrcNetworkDiscovery != null)
		{
			mrcNetworkDiscovery.StopBroadcast();
		}

		if (tcpServer != null)
		{
			tcpServer.StopListening();
		}

		if (tcpClient != null)
		{
			tcpClient.Disconnect();
		}
	}

	// queue for not running Android JNI functions in another thread
	private Queue<Action> actions = new Queue<Action>();

	private void Update()
	{
		RequestDataPermissions();

		lock(actions)
		{
			while(actions.Count > 0)
			{
				Action action = actions.Dequeue();
				action.Invoke();
			}
		}

		if (tcpClient != null)
		{
			tcpClient.Tick();
		}

		if (tcpServer != null && HasConnectedClient())
		{
			float realtimeSinceStartup = Time.timeSinceLevelLoad;
			if (realtimeSinceStartup > lastBroadcastTime + timeInterval)
			{
				byte[] bytes = BitConverter.GetBytes(DataVersion);
				tcpServer.Broadcast(DATA_VERSION, bytes);
				Debug.Log($"[CalibrationNetworkServer] broadcast DATA_VERSION");

				OVRPlugin.Posef nodePose = OVRPlugin.GetNodePose(OVRPlugin.Node.Head, OVRPlugin.Step.Render);
				string text = FormatVector3fWithInvariantCulture(nodePose.Position);
				string text2 = FormatQuatfWithInvariantCulture(nodePose.Orientation);

				OVRPlugin.Posef nodePose2 = OVRPlugin.GetNodePose(OVRPlugin.Node.HandLeft, OVRPlugin.Step.Render);
				string text3 = FormatVector3fWithInvariantCulture(nodePose2.Position);
				string text4 = FormatQuatfWithInvariantCulture(nodePose2.Orientation);

				OVRPlugin.Posef nodePose3 = OVRPlugin.GetNodePose(OVRPlugin.Node.HandRight, OVRPlugin.Step.Render);
				string text5 = FormatVector3fWithInvariantCulture(nodePose3.Position);
				string text6 = FormatQuatfWithInvariantCulture(nodePose3.Orientation);

				OVRPlugin.Posef trackingTransformRawPose = OVRPlugin.GetTrackingTransformRawPose();
				string text7 = FormatVector3fWithInvariantCulture(trackingTransformRawPose.Position);
				string text8 = FormatQuatfWithInvariantCulture(trackingTransformRawPose.Orientation);

				bool controllerPositionTracked = OVRInput.GetControllerPositionTracked(OVRInput.Controller.LTouch);
				bool controllerPositionValid = OVRInput.GetControllerPositionValid(OVRInput.Controller.LTouch);
				bool controllerPositionTracked2 = OVRInput.GetControllerPositionTracked(OVRInput.Controller.RTouch);
				bool controllerPositionValid2 = OVRInput.GetControllerPositionValid(OVRInput.Controller.RTouch);

				string s = $"frame {currentBroadcstFrameIndex} " +
					$"time {realtimeSinceStartup} " +
					$"head_pos {text} " +
					$"head_rot {text2} " +
					$"left_hand_pos {text3} " +
					$"left_hand_rot {text4} " +
					$"right_hand_pos {text5} " +
					$"right_hand_rot {text6} " +
					$"raw_pos {text7} " +
					$"raw_rot {text8} " +
					$"lht {(controllerPositionTracked ? 1 : 0)} " +
					$"lhv {(controllerPositionValid ? 1 : 0)} " +
					$"rht {(controllerPositionTracked2 ? 1 : 0)} " +
					$"rhv {(controllerPositionValid2 ? 1 : 0)}";
				byte[] bytes2 = Encoding.UTF8.GetBytes(s);
				tcpServer.Broadcast(POSE_UPDATE, bytes2);
				Debug.Log($"[CalibrationNetworkServer] broadcast POSE_UPDATE");

				currentBroadcstFrameIndex++;
				lastBroadcastTime = realtimeSinceStartup;
			}

			AdjustKey adjustKey = AdjustKey.None;
			if (OVRInput.Get(OVRInput.Button.PrimaryThumbstickUp, OVRInput.Controller.RTouch))
			{
				adjustKey = ((!OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch)) ? AdjustKey.Up : AdjustKey.PitchIncrease);
			}
			else if (OVRInput.Get(OVRInput.Button.PrimaryThumbstickDown, OVRInput.Controller.RTouch))
			{
				adjustKey = (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch) ? AdjustKey.PitchDecrease : AdjustKey.Down);
			}
			else if (OVRInput.Get(OVRInput.Button.PrimaryThumbstickLeft, OVRInput.Controller.RTouch))
			{
				adjustKey = (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch) ? AdjustKey.RollIncrease : AdjustKey.Left);
			}
			else if (OVRInput.Get(OVRInput.Button.PrimaryThumbstickRight, OVRInput.Controller.RTouch))
			{
				adjustKey = (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch) ? AdjustKey.RollDecrease : AdjustKey.Right);
			}
			else if (OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.RTouch))
			{
				adjustKey = (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch) ? AdjustKey.YawIncrease : AdjustKey.Forward);
			}
			else if (OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.RTouch))
			{
				adjustKey = (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch) ? AdjustKey.YawDecrease : AdjustKey.Backward);
			}
			else if (OVRInput.Get(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch))
			{
				adjustKey = (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch) ? AdjustKey.FovDecrease : AdjustKey.FovIncrease);
			}

			if (adjustKey != 0)
			{
				tcpServer.Broadcast(ADJUST_KEY, BitConverter.GetBytes((int)adjustKey));
				Debug.Log($"[CalibrationNetworkServer] broadcast ADJUST_KEY");
			}

			if (OVRInput.GetDown(OVRInput.Button.One | OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch) || OVRInput.GetDown(OVRInput.Button.One | OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch))
			{
				primaryButtonPressedTimes++;

				tcpServer.Broadcast(PRIMARY_BUTTON_PRESSED, BitConverter.GetBytes(GetPrimaryButtonPressedTimes()));
				Debug.Log($"[CalibrationNetworkServer] broadcast PRIMARY_BUTTON_PRESSED");
			}

			if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch) || OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch))
			{
				secondaryButtonPressedTimes++;

				tcpServer.Broadcast(SECONDARY_BUTTON_PRESSED, BitConverter.GetBytes(GetSecondaryButtonPressedTimes()));
				Debug.Log($"[CalibrationNetworkServer] broadcast SECONDARY_BUTTON_PRESSED");
			}
		}
	}

	public bool HasConnectedClient()
	{
		return tcpServer.HasConnectedClient();
	}

	public int GetPrimaryButtonPressedTimes()
	{
		return primaryButtonPressedTimes;
	}

	public int GetSecondaryButtonPressedTimes()
	{
		return secondaryButtonPressedTimes;
	}

	private void CopyFile(string from, string to)
	{
		if (from.Equals(to))
			return;

#if UNITY_ANDROID
		try
		{
			AndroidJavaObject activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			AndroidJavaObject pluginInstanceClass = new AndroidJavaClass("com.insbyte.unityplugin.PluginInstance");

			pluginInstanceClass.CallStatic("copyFile", from, to);
		}
		catch (Exception ex)
		{
			Debug.LogError($"[CalibrationNetworkServer] {ex.Message}");
		}
#else
		File.Copy(from, to, true);
#endif
	}

	private void DeleteFile(string path)
	{
#if UNITY_ANDROID
		try
		{
			AndroidJavaObject activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			AndroidJavaObject pluginInstanceClass = new AndroidJavaClass("com.insbyte.unityplugin.PluginInstance");

			pluginInstanceClass.CallStatic("deleteFile", path);
		}
		catch (Exception ex)
		{
			Debug.LogError($"[CalibrationNetworkServer] {ex.Message}");
		}
#else
		File.Delete(path);
#endif
	}

#if UNITY_ANDROID
	private void ReleaseAllFolderPermissions()
	{
		try
		{
			AndroidJavaObject activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			AndroidJavaObject currentActivity = activityClass.GetStatic<AndroidJavaObject>("currentActivity");
			AndroidJavaObject pluginInstanceClass = new AndroidJavaClass("com.insbyte.unityplugin.PluginInstance");

			pluginInstanceClass.CallStatic("releaseAllPermissions");
		}
		catch (Exception ex)
		{
			Debug.LogError($"[CalibrationNetworkServer] {ex.Message}");
		}
	}

	private bool HasFolderPermissions(string directory)
	{
		try
		{
			AndroidJavaObject activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			AndroidJavaObject currentActivity = activityClass.GetStatic<AndroidJavaObject>("currentActivity");
			AndroidJavaObject pluginInstanceClass = new AndroidJavaClass("com.insbyte.unityplugin.PluginInstance");

			return pluginInstanceClass.CallStatic<bool>("hasAccessToFolder", directory);
		}
		catch (Exception ex)
		{
			Debug.LogError($"[CalibrationNetworkServer] {ex.Message}");
		}
		return false;
	}

	private void RequestFolderPermissions(string directory)
	{
		try
		{
			AndroidJavaObject activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			AndroidJavaObject currentActivity = activityClass.GetStatic<AndroidJavaObject>("currentActivity");
			AndroidJavaObject pluginInstanceClass = new AndroidJavaClass("com.insbyte.unityplugin.PluginInstance");

			pluginInstanceClass.CallStatic("askForAccess", directory);
		}
		catch (Exception ex)
		{
			Debug.LogError($"[CalibrationNetworkServer] {ex.Message}");
		}
	}

	int RequestedDataPermissions = 0;
	int NumFramesToStartRequest = 60;
	private void RequestDataPermissions()
	{
		string[] RequiredPermissions = new string[]
		{
#if UNITY_ANDROID
			"Android/data",
#endif
		};

		if (RequestedDataPermissions == RequiredPermissions.Length)
		{
			return;
		}

		if (NumFramesToStartRequest > 0)
		{
			NumFramesToStartRequest--;
			return;
		}
		NumFramesToStartRequest = 0;

		if (HasFolderPermissions(Path.Combine(storageDir.FullName, RequiredPermissions[RequestedDataPermissions])))
		{
			RequestedDataPermissions++; // time to request next permission
			return;
		}

		if(RequestedDataPermissions < RequiredPermissions.Length)
			RequestFolderPermissions(Path.Combine(storageDir.FullName, RequiredPermissions[RequestedDataPermissions]));
	}
#endif

	private void CheckRequestPermissions()
	{
#if UNITY_ANDROID
		AndroidJavaObject activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
		AndroidJavaObject currentActivity = activityClass.GetStatic<AndroidJavaObject>("currentActivity");

		// initialize plugin instance
		try
		{
			// TODO: make a shim
			AndroidJavaObject pluginInstanceClass = new AndroidJavaClass("com.insbyte.unityplugin.PluginInstance");
			pluginInstanceClass.CallStatic("initialize", currentActivity);
		}
		catch (Exception ex)
		{
			Debug.LogError($"[CalibrationNetworkServer] {ex.Message}");
		}

		/*
		// request storage permissions
		AndroidJavaClass environment = new AndroidJavaClass("android.os.Environment");
		if (!environment.CallStatic<bool>("isExternalStorageManager"))
		{
			string manageAppFilesAccess = new AndroidJavaClass("android.provider.Settings").GetStatic<string>("ACTION_MANAGE_APP_ALL_FILES_ACCESS_PERMISSION");
			AndroidJavaObject intentUri = new AndroidJavaClass("android.net.Uri").CallStatic<AndroidJavaObject>("parse", $"package:{UnityEngine.Application.identifier}");

			var intent = new AndroidJavaObject("android.content.Intent", manageAppFilesAccess, intentUri);
			currentActivity.Call("startActivity", intent);
		}
		*/
		if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead) ||
			!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite))
		{
			Permission.RequestUserPermissions(new[] {
				Permission.ExternalStorageRead,
				Permission.ExternalStorageWrite
			});
		}
#endif
	}

	private DirectoryInfo GetExternalStorageDirectory()
	{
		AndroidJavaClass environment = new AndroidJavaClass("android.os.Environment");
		AndroidJavaObject directory = environment.CallStatic<AndroidJavaObject>("getExternalStorageDirectory");
		string externalFilesDir = directory.Call<string>("getPath");

		return new DirectoryInfo(externalFilesDir);
	}

	private List<DirectoryInfo> GetApplicationDirectories()
	{
		List<DirectoryInfo> list = new List<DirectoryInfo>();

		try
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(Path.Combine(storageDir.FullName, "Android/data"));

			AndroidJavaObject activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			AndroidJavaObject currentActivity = activityClass.GetStatic<AndroidJavaObject>("currentActivity");

			AndroidJavaObject androidJavaObject = currentActivity.Call<AndroidJavaObject>("getPackageManager", Array.Empty<object>());

			int @static = new AndroidJavaClass("android.content.pm.PackageManager").GetStatic<int>("GET_META_DATA");
			int static2 = new AndroidJavaClass("android.content.pm.ApplicationInfo").GetStatic<int>("FLAG_SYSTEM");
			int static3 = new AndroidJavaClass("android.content.pm.ApplicationInfo").GetStatic<int>("FLAG_UPDATED_SYSTEM_APP");
			AndroidJavaObject androidJavaObject2 = androidJavaObject.Call<AndroidJavaObject>("getInstalledPackages", new object[1] { @static });

			int num = androidJavaObject2.Call<int>("size", Array.Empty<object>());
			for (int i = 0; i < num; i++)
			{
				AndroidJavaObject androidJavaObject3 = androidJavaObject2.Call<AndroidJavaObject>("get", new object[1] { i });
				int num2 = androidJavaObject3.Get<AndroidJavaObject>("applicationInfo").Get<int>("flags");
				
				if ((num2 & (static2 | static3)) == 0)
				{
					string packageName = androidJavaObject3.Get<string>("packageName");
					Debug.Log($"[CalibrationNetworkServer] *** non-system package {packageName} ***");

					DirectoryInfo directoryInfo2 = new DirectoryInfo(Path.Combine(directoryInfo.FullName, packageName));
					if (directoryInfo2.Exists)
					{
						Debug.Log($"[CalibrationNetworkServer] {packageName} is a writable application");
						list.Add(directoryInfo2);
					}
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogError($"[CalibrationNetworkServer] {ex.Message}");
		}

		return list;
	}

	private void ClientConnected(TcpClient client)
	{
		Debug.Log($"[CalibrationNetworkServer] Client connected - userId {userId}");

		tcpClient = new OVRNetwork.OVRNetworkTcpClient(client);
		tcpClient.payloadReceivedCallback = PayloadReceived;

		byte[] bytes = Encoding.UTF8.GetBytes(userId);
		tcpServer.Broadcast(USER_ID, bytes);
		Debug.Log($"[CalibrationNetworkServer] broadcast USER_ID");

		byte[] payload = new byte[0];

		try
		{
			FileInfo fileInfo = new FileInfo(Path.Combine(thisAppDir.FullName, "mrc.xml"));

			if (fileInfo.Exists)
			{
				XmlDocument xmlDocument = new XmlDocument();
				xmlDocument.Load(fileInfo.FullName);
				StringWriter stringWriter = new StringWriter();
				XmlWriter xmlWriter = XmlWriter.Create(stringWriter);
				xmlDocument.WriteTo(xmlWriter);
				xmlWriter.Flush();
				string text = stringWriter.GetStringBuilder().ToString();
				Debug.Log($"[CalibrationNetworkServer] {text}");
				payload = Encoding.UTF8.GetBytes(text);
			}
			else
				Debug.Log($"[CalibrationNetworkServer] no calibration data exist");
		} 
		catch(Exception ex)
		{
			Debug.LogError($"[CalibrationNetworkServer] {ex.Message}");
		}

		tcpServer.Broadcast(CALIBRATION_DATA, payload);
		Debug.Log($"[CalibrationNetworkServer] broadcast CALIBRATION_DATA");
	}

	private void PayloadReceived(int payloadType, byte[] buffer, int start, int length)
	{
		Debug.Log("[CalibrationNetworkServer] Network Payload Received");

		switch (payloadType)
		{
			case CALIBRATION_DATA:
			{
				string errorText = null;
				string calibrationXmlData = Encoding.UTF8.GetString(buffer, start, length);
				XmlDocument xmlDocument = new XmlDocument();
				xmlDocument.LoadXml(calibrationXmlData);

				XmlNode cameraNode = xmlDocument.SelectSingleNode("opencv_storage/camera_id");
				if (cameraNode != null)
				{
#if UNITY_ANDROID
					try
					{
						Convert.ToUInt32(cameraNode.Value);
						string fileName = Path.Combine(thisAppDir.FullName, "mrc.xml");

						Debug.Log($"[CalibrationNetworkServer] Writing camera calibration to {fileName}");
						File.WriteAllText(fileName, calibrationXmlData);

						lock (actions)
						{
							actions.Enqueue(UpdateCalibrationFiles);
							actions.Enqueue(PostCalibrationUpdate);
						}
					}
					catch (Exception ex3)
					{
						errorText = "Could not write file\n" + ex3.Message;
					}
#else
					try
					{
						SendCalibrationDataToOVRServer(xmlDocument);
					}
					catch (Exception ex2)
					{
						text = "Failed to send calibration data\n" + ex2.Message;
					}
#endif
				}
				else
				{
					errorText = "XML or Camera ID invalid";
				}

				if (errorText != null)
				{
					Debug.Log($"[CalibrationNetworkServer] {errorText}");
				}

				tcpServer.Broadcast(OPERATION_COMPLETE, (errorText != null) ? Encoding.UTF8.GetBytes(errorText) : new byte[0]);
				Debug.Log($"[CalibrationNetworkServer] broadcast OPERATION_COMPLETE");
				break;
			}
			case CLEAR_CALIBRATION:
			{
				lock (actions)
				{
					actions.Enqueue(TryDeleteCalibrationData);
				}
				break;
			}
		}
	}

	private void UpdateCalibrationFiles()
	{
		string errorText = null;
		FileInfo fileInfo = new FileInfo(Path.Combine(thisAppDir.FullName, "mrc.xml"));
		if (fileInfo.Exists)
		{
			Debug.Log("[CalibrationNetworkServer] Saved MRC Calibration Found, applying to apps");

			foreach (DirectoryInfo appDir in appDirs)
			{
				if (new DirectoryInfo(Path.Combine(appDir.FullName, "files")).Exists)
				{
					try
					{
						CopyFile(fileInfo.FullName, Path.Combine(appDir.FullName, "files/mrc.xml"));
						Debug.Log("[CalibrationNetworkServer] MRC calibration copied to " + Path.Combine(appDir.FullName, "files/mrc.xml"));
					}
					catch (Exception)
					{
						Debug.Log("[CalibrationNetworkServer] Cannot copy MRC config to " + Path.Combine(appDir.FullName, "files/mrc.xml"));
					}
				}
				else
				{
					Debug.Log($"[CalibrationNetworkServer] {Path.Combine(appDir.FullName, "files")} is not valid");
				}
			}
		}

		tcpServer.Broadcast(OPERATION_COMPLETE, (errorText != null) ? Encoding.UTF8.GetBytes(errorText) : new byte[0]);
		Debug.Log($"[CalibrationNetworkServer] broadcast OPERATION_COMPLETE");
	}

	private void TryDeleteCalibrationData()
	{
		string text = null;
		try
		{
			foreach (DirectoryInfo appDir2 in appDirs)
			{
				DirectoryInfo directoryInfo = new DirectoryInfo(Path.Combine(appDir2.FullName, "files"));
				if (directoryInfo.Exists)
				{
					Debug.Log($"[CalibrationNetworkServer] Deleting camera calibration config from {Path.Combine(directoryInfo.FullName, "mrc.xml")}");
					DeleteFile(Path.Combine(directoryInfo.FullName, "mrc.xml"));
				}
			}
		}
		catch (Exception)
		{
			text = "Could not delete files";
		}

		tcpServer.Broadcast(OPERATION_COMPLETE, (text != null) ? Encoding.UTF8.GetBytes(text) : new byte[0]);
		Debug.Log($"[CalibrationNetworkServer] broadcast OPERATION_COMPLETE");
	}

	private void PostCalibrationUpdate()
	{
#if ENABLE_MRC_IN_APP
		int audioSampleRate = AudioSettings.outputSampleRate;

		if (OVRPlugin.Media.GetInitialized())
		{
			Debug.Log("[CalibrationNetworkServer] Re-initializing MRC with new calibration");
			OVRPlugin.Media.Shutdown();
		}

		bool num = OVRPlugin.Media.Initialize();
		Debug.Log($"[CalibrationNetworkServer] {(num ? "OVRPlugin.Media initialized" : "OVRPlugin.Media not initialized")}");
		if (num)
		{
			OVRPlugin.Media.SetMrcAudioSampleRate(audioSampleRate);
			OVRPlugin.Media.SetMrcInputVideoBufferType(OVRPlugin.Media.InputVideoBufferType.TextureHandle);
			OVRPlugin.Media.SetMrcActivationMode(OVRPlugin.Media.MrcActivationMode.Automatic);
		}
#endif
	}

	private void OnApplicationPause(bool pause)
	{
		if (pause)
		{
			tcpServer.Broadcast(STATE_CHANGE_PAUSE, new byte[0]);
			Debug.Log($"[CalibrationNetworkServer] broadcast STATE_CHANGE_PAUSE");
		}
	}

	private void SendCalibrationDataToOVRServer(XmlDocument xml)
	{
		XmlNode xmlNode = xml.SelectSingleNode("opencv_storage");
		float[] array = Array.ConvertAll(xmlNode["translation"]["data"].InnerText.Split(new char[0], StringSplitOptions.RemoveEmptyEntries), float.Parse);
		float[] array2 = Array.ConvertAll(xmlNode["rotation"]["data"].InnerText.Split(new char[0], StringSplitOptions.RemoveEmptyEntries), float.Parse);
		float[] array3 = Array.ConvertAll(xmlNode["camera_matrix"]["data"].InnerText.Split(new char[0], StringSplitOptions.RemoveEmptyEntries), float.Parse);
		
		float[,] array4 = new float[3, 3];
		int num = 0;
		for (int i = 0; i < 3; i++)
		{
			for (int j = 0; j < 3; j++)
			{
				array4[i, j] = array3[num];
				num++;
			}
		}

		int num2 = XmlConvert.ToInt32(xmlNode["image_width"].InnerText);
		int num3 = XmlConvert.ToInt32(xmlNode["image_height"].InnerText);

		OVRPlugin.CameraIntrinsics cameraIntrinsics = default(OVRPlugin.CameraIntrinsics);
		cameraIntrinsics.LastChangedTimeSeconds = OVRPlugin.GetTimeInSeconds();
		cameraIntrinsics.FOVPort = default(OVRPlugin.Fovf);
		cameraIntrinsics.FOVPort.UpTan = array4[1, 2] / array4[1, 1];
		cameraIntrinsics.FOVPort.DownTan = ((float)num3 - array4[1, 2]) / array4[1, 1];
		cameraIntrinsics.FOVPort.LeftTan = array4[0, 2] / array4[0, 0];
		cameraIntrinsics.FOVPort.RightTan = ((float)num2 - array4[0, 2]) / array4[0, 0];
		cameraIntrinsics.VirtualNearPlaneDistanceMeters = 0.1f;
		cameraIntrinsics.VirtualFarPlaneDistanceMeters = 1000f;
		cameraIntrinsics.ImageSensorPixelResolution.w = num2;
		cameraIntrinsics.ImageSensorPixelResolution.h = num3;

		OVRPlugin.CameraExtrinsics cameraExtrinsics = default(OVRPlugin.CameraExtrinsics);
		cameraExtrinsics.LastChangedTimeSeconds = OVRPlugin.GetTimeInSeconds();
		cameraExtrinsics.CameraStatusData = OVRPlugin.CameraStatus.CameraStatus_Calibrated;
		cameraExtrinsics.RelativePose.Position = default(OVRPlugin.Vector3f);
		cameraExtrinsics.RelativePose.Position.x = array[0];
		cameraExtrinsics.RelativePose.Position.y = array[1];
		cameraExtrinsics.RelativePose.Position.z = array[2];
		cameraExtrinsics.RelativePose.Orientation = default(OVRPlugin.Quatf);
		cameraExtrinsics.RelativePose.Orientation.x = array2[0];
		cameraExtrinsics.RelativePose.Orientation.y = array2[1];
		cameraExtrinsics.RelativePose.Orientation.z = array2[2];
		cameraExtrinsics.RelativePose.Orientation.w = array2[3];

		OVRPlugin.SetExternalCameraProperties(xmlNode["camera_name"].InnerText, ref cameraIntrinsics, ref cameraExtrinsics);
		if (!OVRPlugin.UpdateExternalCamera())
		{
			Debug.LogError("[CalibrationNetworkServer] UpdateExternalCamera failed");
		}
	}

	private string FormatVector3fWithInvariantCulture(OVRPlugin.Vector3f vec)
	{
		return string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}", vec.x, vec.y, vec.z);
	}

	private string FormatQuatfWithInvariantCulture(OVRPlugin.Quatf quat)
	{
		return string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}, {3}", quat.x, quat.y, quat.z, quat.w);
	}
}
