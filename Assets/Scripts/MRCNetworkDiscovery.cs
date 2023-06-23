using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class MRCNetworkDiscovery : MonoBehaviour
{
	public int serverListeningPort = 47898;
	public float timeInterval = 0.03333f;

	private float lastBroadcastTime;
	private OVRNetwork.OVRNetworkTcpServer tcpServer;

	private void Start()
	{
		
	}

	private void Update()
	{
		if (tcpServer == null)
			return;

		if (!tcpServer.HasConnectedClient())
			return;

		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (realtimeSinceStartup - lastBroadcastTime > timeInterval)
		{
			tcpServer.ForEachClient(ClientBroadcastMessage);
		}
	}

	private static void ClientBroadcastMessage(TcpClient client)
	{
		string broadcastData = SystemInfo.deviceName + ": " + SystemInfo.deviceModel;
		byte[] dataBuffer = Encoding.UTF8.GetBytes(broadcastData);
		client.GetStream().WriteAsync(dataBuffer, 0, dataBuffer.Length);
	}

	public void StartBroadcast()
	{
		tcpServer = new OVRNetwork.OVRNetworkTcpServer();
		tcpServer.StartListening(serverListeningPort);
		Debug.Log("[MRCNetworkDiscovery] Server Created with default port");
	}

	public void StopBroadcast()
	{
		tcpServer.StopListening();
	}
}
