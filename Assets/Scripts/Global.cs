using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class Global : MonoBehaviour
{
	//private OVRManager manager;

	//private OVRCameraRig cameraRig;

	public TextMesh statusText;

	private void Start()
	{
		/*
		manager = OVRManager.instance;
		cameraRig = (manager ? manager.GetComponent<OVRCameraRig>() : null);
		
		if ((bool)cameraRig.centerEyeAnchor.GetComponent<Camera>())
		{
			cameraRig.centerEyeAnchor.GetComponent<Camera>().clearFlags = CameraClearFlags.Color;
		}
		if ((bool)cameraRig.leftEyeAnchor.GetComponent<Camera>())
		{
			cameraRig.leftEyeAnchor.GetComponent<Camera>().clearFlags = CameraClearFlags.Color;
		}
		if ((bool)cameraRig.rightEyeAnchor.GetComponent<Camera>())
		{
			cameraRig.rightEyeAnchor.GetComponent<Camera>().clearFlags = CameraClearFlags.Color;
		}*/
	}

	private void Update()
	{
		if (statusText != null)
		{
			string empty = string.Empty;
			empty = empty + "IP address(es): " + LocalIPAddress() + "\n";
			CalibrationNetworkServer component = GetComponent<CalibrationNetworkServer>();
			empty = ((!(component == null)) ? (empty + (component.HasConnectedClient() ? "Camera Calibration Tool: Connected" : "Camera Calibration Tool: Not Connected")) : (empty + "Error: no CalibrationNetworkServer attached"));
			statusText.text = empty;
		}
	}

	public static string LocalIPAddress()
	{
		string text = "0.0.0.0";
		bool flag = false;
		IPAddress[] addressList = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
		foreach (IPAddress iPAddress in addressList)
		{
			if (iPAddress.AddressFamily == AddressFamily.InterNetwork)
			{
				if (!flag)
				{
					text = iPAddress.ToString();
					flag = true;
				}
				else
				{
					text = text + " / " + iPAddress.ToString();
				}
				break;
			}
		}
		return text;
	}
}
