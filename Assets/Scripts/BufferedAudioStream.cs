using System;
using UnityEngine;

public class BufferedAudioStream
{
	private const bool VerboseLogging = false;

	private AudioSource audio;

	private float[] audioBuffer;

	private int writePos;

	private const float bufferLengthSeconds = 0.25f;

	private const int sampleRate = 48000;

	private const int bufferSize = 12000;

	private const float playbackDelayTimeSeconds = 0.05f;

	private float playbackDelayRemaining;

	private float remainingBufferTime;

	public BufferedAudioStream(AudioSource audio)
	{
		audioBuffer = new float[12000];
		this.audio = audio;
		audio.loop = true;
		audio.clip = AudioClip.Create("", 12000, 1, 48000, stream: false);
		Stop();
	}

	public void Update()
	{
		if (remainingBufferTime > 0f)
		{
			if (!audio.isPlaying && remainingBufferTime > 0.05f)
			{
				playbackDelayRemaining -= Time.deltaTime;
				if (playbackDelayRemaining <= 0f)
				{
					audio.Play();
				}
			}
			if (audio.isPlaying)
			{
				remainingBufferTime -= Time.deltaTime;
				if (remainingBufferTime < 0f)
				{
					remainingBufferTime = 0f;
				}
			}
		}
		if (remainingBufferTime <= 0f)
		{
			if (audio.isPlaying)
			{
				Debug.Log("Buffer empty, stopping " + DateTime.Now);
				Stop();
			}
			else if (writePos != 0)
			{
				Debug.LogError("writePos non zero while not playing, how did this happen?");
			}
		}
	}

	private void Stop()
	{
		audio.Stop();
		audio.time = 0f;
		writePos = 0;
		playbackDelayRemaining = 0.05f;
	}

	public void AddData(float[] samples)
	{
		int num = samples.Length;
		if (writePos > audioBuffer.Length)
		{
			throw new Exception();
		}
		do
		{
			int num2 = num;
			int num3 = audioBuffer.Length - writePos;
			if (num2 > num3)
			{
				num2 = num3;
			}
			Array.Copy(samples, 0, audioBuffer, writePos, num2);
			num -= num2;
			writePos += num2;
			if (writePos > audioBuffer.Length)
			{
				throw new Exception();
			}
			if (writePos == audioBuffer.Length)
			{
				writePos = 0;
			}
		}
		while (num > 0);
		remainingBufferTime += (float)samples.Length / 48000f;
		audio.clip.SetData(audioBuffer, 0);
	}
}
