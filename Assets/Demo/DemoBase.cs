using LR.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace LR.Demos
{

	[RequireComponent(typeof(Framework))]
	[DisallowMultipleComponent]
	public abstract class DemoBase : MonoBehaviour
	{

		protected Framework m_framework = null;

		[Header("Frame")]
		[SerializeField]
		protected Vector2Int m_size = new Vector2Int(1280, 720);

		protected virtual void OnStart(FrameBuffer frameBuffer)
		{
		}

		protected virtual void OnUpdate(FrameBuffer frameBuffer)
		{
		}

		protected virtual void OnRender(FrameBuffer frameBuffer)
		{
		}

		protected virtual void Awake()
		{
			m_framework = GetComponent<Framework>();
		}

		protected virtual void OnEnable()
		{
			var callbacks = new Dictionary<Framework.EventType, System.Action<FrameBuffer>>()
			{
				{ Framework.EventType.OnStart, OnStart },
				{ Framework.EventType.OnUpdate, OnUpdate },
				{ Framework.EventType.OnRender, OnRender }
			};
			var frameBuffer = new FrameBuffer(m_size);
			m_framework.Initialize(frameBuffer, callbacks);
		}

		protected virtual void OnDisable()
		{
			m_framework.Terminate();
		}

	}

	public static class MathUtility
	{
		public static void Swap<T>(ref T lhs, ref T rhs)
		{
			var temp = lhs;
			lhs = rhs;
			rhs = temp;
		}
	}

	public sealed class Edge<T>
	{

		public Edge(T from, T to)
		{
			this.from = from;
			this.to = to;
		}

		public T from { set; get; }

		public T to { set; get; }

	}

}
