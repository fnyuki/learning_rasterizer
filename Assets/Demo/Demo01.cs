using LR.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LR.Demos
{

	public class Demo01 : DemoBase
	{
		protected override void OnRender(FrameBuffer frameBuffer)
		{
			base.OnRender(frameBuffer);
			frameBuffer.Clear();
			for (var y = 0; y < m_size.y; y++)
			{
				for (var x = 0; x < m_size.x; x++)
				{
					frameBuffer.SetPixel(new Vector2Int(x, y), new Color((float)x / m_size.x, (float)y / m_size.y, 1.0f));
				}
			}
		}

	}

}