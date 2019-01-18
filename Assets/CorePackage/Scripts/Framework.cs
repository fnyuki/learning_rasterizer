using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.Linq;

namespace LR.Core
{

	public sealed class FrameBuffer
	{

		private Vector2Int m_size;

		private Color[] m_pixels = null;

		private float[] m_depths = null;

		public FrameBuffer(Vector2Int size)
		{
			m_size = size;
			Clear();
		}

		public Vector2Int GetSize()
		{
			return m_size;
		}

		public float GetAspectRatio()
		{
			return (float)m_size.x / m_size.y;
		}

		public void Clear()
		{
			m_pixels = new Color[m_size.x * m_size.y];
			m_depths = Enumerable.Repeat(1.0f, m_size.x * m_size.y).ToArray();
		}

		public void SetPixel(Vector2Int pos, Color color)
		{
			if (pos.x < 0 || pos.x >= m_size.x)
			{
				//Debug.LogWarning("pos.x が size の外側!");
				return;
			}
			if (pos.y < 0 || pos.y >= m_size.y)
			{
				//Debug.LogWarning("pos.y が size の外側!");
				return;
			}
			m_pixels[pos.x + pos.y * m_size.x] = color;
		}

		public void SetDepth(Vector2Int pos, float depth)
		{
			m_depths[pos.x + pos.y * m_size.x] = depth;
		}

		public Color[] GetPixels()
		{
			return m_pixels;
		}

		public float GetDepth(Vector2Int pos)
		{
			return m_depths[pos.x + pos.y * m_size.x];
		}

	}

	[RequireComponent(typeof(Camera))]
	public sealed class Framework : MonoBehaviour
	{

		public enum EventType
		{
			OnStart,
			OnUpdate,
			OnRender,
		}

		private Camera m_camera = null;

		private CommandBuffer m_commandBuffer = null;

		private Material m_material = null;

		private ComputeBuffer m_computeBuffer = null;

		private FrameBuffer m_frameBuffer = null;

		private Dictionary<EventType, System.Action<FrameBuffer>> m_callbacks = null;

		[SerializeField]
		private Shader m_shader = null;

		private void CreateResidentResources()
		{
			if (m_camera == null)
			{
				m_camera = GetComponent<Camera>();
			}
			if (m_material == null)
			{
				m_material = new Material(m_shader);
			}
			if (m_commandBuffer == null)
			{
				m_commandBuffer = new CommandBuffer();
				m_commandBuffer.name = "Learning Rasterizer";
			}
		}

		private void DestroyResidentResources()
		{
			if (m_commandBuffer != null)
			{
				m_commandBuffer.Dispose();
			}
			if (m_material != null)
			{
				Destroy(m_material);
			}
		}

		public void Initialize(FrameBuffer frameBuffer, Dictionary<EventType, System.Action<FrameBuffer>> callbacks)
		{
			CreateResidentResources();
			m_frameBuffer = frameBuffer;
			m_callbacks = callbacks;
			var size = m_frameBuffer.GetSize();
			var colors = m_frameBuffer.GetPixels();
			m_computeBuffer = new ComputeBuffer(colors.Length, sizeof(float) * 4, ComputeBufferType.Default);
			int sizeId = Shader.PropertyToID("_Size");
			int bufferId = Shader.PropertyToID("_Buffer");
			m_commandBuffer.SetGlobalVector(sizeId, new Vector4(size.x, size.y));
			m_commandBuffer.SetGlobalBuffer(bufferId, m_computeBuffer);
			m_commandBuffer.Blit(BuiltinRenderTextureType.None, BuiltinRenderTextureType.CurrentActive, m_material);
			m_camera.AddCommandBuffer(CameraEvent.AfterEverything, m_commandBuffer);
			if (m_callbacks.ContainsKey(EventType.OnStart) && m_callbacks[EventType.OnStart] != null)
			{
				m_callbacks[EventType.OnStart](m_frameBuffer);
			}
		}

		public void Terminate()
		{
			m_commandBuffer.Clear();
			m_camera.RemoveCommandBuffer(CameraEvent.AfterEverything, m_commandBuffer);
			if (m_computeBuffer != null)
			{
				m_computeBuffer.Release();
				m_computeBuffer.Dispose();
			}
			m_callbacks = null;
			m_frameBuffer = null;
		}

		private void Update()
		{
			if (m_frameBuffer == null)
			{
				return;
			}
			if (m_callbacks.ContainsKey(EventType.OnUpdate) && m_callbacks[EventType.OnUpdate] != null)
			{
				m_callbacks[EventType.OnUpdate](m_frameBuffer);
			}
			if (m_callbacks.ContainsKey(EventType.OnRender) && m_callbacks[EventType.OnRender] != null)
			{
				m_callbacks[EventType.OnRender](m_frameBuffer);
			}
			m_computeBuffer.SetData(m_frameBuffer.GetPixels());
		}

		private void OnDestroy()
		{
			Terminate();
			DestroyResidentResources();
		}

	}

}