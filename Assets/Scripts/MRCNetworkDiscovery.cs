using UnityEngine;
//using UnityEngine.Networking;

public class MRCNetworkDiscovery : MonoBehaviour // : NetworkDiscovery
{
	private void Start()
	{
	}

	public void StartBroadcast()
	{
		Debug.Log("UNIMPLEMENTED StartBroadcast");
	}

	public void StopBroadcast()
	{

	}
	/*
	public void StartBroadcast()
	{
		base.broadcastPort = 47898;
		if (NetworkServer.Listen(base.broadcastPort))
		{
			Debug.Log("Server Created with default port");
			base.broadcastData = SystemInfo.deviceName + ": " + SystemInfo.deviceModel;
			Initialize();
			StartAsServer();
		}
		else
		{
			Debug.Log("Failed to create with the default port");
		}
	}*/
}
