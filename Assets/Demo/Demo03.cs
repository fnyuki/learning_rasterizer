using LR.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LR.Demos
{

	public class Demo03 : DemoBase
	{

		private AxisUtility m_axisUtility = null;

		private Vector3[] m_cachedVerts = null;

		[SerializeField]
		private Mesh m_mesh = null;

		[SerializeField]
		private Vector3 m_position = Vector3.zero;

		[SerializeField]
		private Vector3 m_scale = Vector3.one;

		protected override void OnStart(FrameBuffer frameBuffer)
		{
			base.OnStart(frameBuffer);
			m_axisUtility = FindObjectOfType<AxisUtility>();
			m_cachedVerts = m_mesh.vertices;
		}

		protected override void OnRender(FrameBuffer frameBuffer)
		{
			base.OnRender(frameBuffer);

			frameBuffer.Clear();

			// MVP Transform (affine/projective transformation)
			//       | Model mat. | View mat. | Proj mat.
			//       V ~          V ~         V ~
			// local ->   world   ->   view   ->    clip   -> ...
			Matrix4x4 M = Matrix4x4.TRS(m_position, Quaternion.identity, m_scale);
			Matrix4x4 V = m_axisUtility.GetLookAt();
			Matrix4x4 P = Matrix4x4.Perspective(60.0f, frameBuffer.GetAspectRatio(), 0.01f, 100.0f);
			Matrix4x4 MVP = P * V * M;
			System.Func<Vector4, Vector4> applyMVP = (input) =>
			{
				return MVP * input;
			};

			// Perspective division
			//      | Perspective division
			//      V
			// clip -> nomalized device coordinates (aka NDC) -> ...
			// +----------------------------------------------------------------------+
			// | Note:                                                                |
			// | NDC space directly works with [-1.0, +1.0] range to simplify things; |
			// | i.e. being independent from screen resolution and pixel density.     |
			// +----------------------------------------------------------------------+
			System.Func<Vector4, Vector3> applyPerspevtiveDivision = (input) =>
			{
				return new Vector3(input.x, input.y, input.z) / input.w;
			};

			// Viewport transform
			//     | Viewport transform
			//     V
			// NDC -> screen (window)
			System.Func<Vector3, Vector2> applyViewport = (input) =>
			{
				return new Vector2
				(
					(1.0f + input.x) * 0.5f * m_size.x,
					(1.0f - input.y) * 0.5f * m_size.y
				);
			};

			for (var i = 0; i < m_cachedVerts.Length; i++)
			{
				var v = m_cachedVerts[i];
				var vMVP = applyMVP(new Vector4(v.x, v.y, v.z, 1.0f));
				var vPD = applyPerspevtiveDivision(vMVP);
				var vVP = applyViewport(vPD);
				frameBuffer.SetPixel(new Vector2Int((int)vVP.x, (int)vVP.y), Color.white);
			}
		}

	}

}