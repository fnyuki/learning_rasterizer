using LR.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace LR.Demos
{

	public class Demo04 : DemoBase
	{

		private AxisUtility m_axisUtility = null;

		private Vector3[] m_cachedVerts = null;

		private List<int[]> m_cachedIndices = null;

		[Header("Camera")]
		[SerializeField]
		private float m_fov = 60.0f;

		[SerializeField]
		private float m_nearClip = 0.01f;

		[SerializeField]
		private float m_farClip = 100.0f;

		[Header("Mesh")]
		[SerializeField]
		private Mesh m_mesh = null;

		[SerializeField]
		private Vector3 m_position = Vector3.zero;

		[SerializeField]
		private Vector3 m_scale = Vector3.one;

		protected override void OnStart(FrameBuffer frameBuffer)
		{
			base.OnStart(frameBuffer);
			if (m_mesh == null)
			{
				enabled = false;
				return;
			}
			m_axisUtility = FindObjectOfType<AxisUtility>();
			m_cachedVerts = m_mesh.vertices;
			m_cachedIndices = new List<int[]>();
			for (var i = 0; i < m_mesh.subMeshCount; i++)
			{
				var indices = m_mesh.GetIndices(i);
				if (indices.Length % 3 != 0)
				{
					Debug.LogError("不正なインデックスデータを含んでいます.");
					enabled = false;
					return;
				}
				m_cachedIndices.Add(indices);
			}
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
			Matrix4x4 P = Matrix4x4.Perspective(m_fov, frameBuffer.GetAspectRatio(), m_nearClip, m_farClip);
			Matrix4x4 MVP = P * V * M;
			System.Func<Vector4, Vector4> vertexShader = (input) =>
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

			// Bresenham's line algorithm
			// https://en.wikipedia.org/wiki/Bresenham%27s_line_algorithm
			System.Action<Vector2, Vector2> rasterLine = (p0, p1) =>
			{
				var x0 = (int)p0.x;
				var y0 = (int)p0.y;
				var x1 = (int)p1.x;
				var y1 = (int)p1.y;
				var steep = Mathf.Abs(y1 - y0) > Mathf.Abs(x1 - x0);
				if (steep)
				{
					MathUtility.Swap(ref x0, ref y0);
					MathUtility.Swap(ref x1, ref y1);
				}
				if (x0 > x1)
				{
					MathUtility.Swap(ref x0, ref x1);
					MathUtility.Swap(ref y0, ref y1);
				}
				var dx = x1 - x0;
				var dy = Mathf.Abs(y1 - y0);
				var error = dx / 2;
				var ystep = y0 < y1 ? 1 : -1;
				var y = y0;
				for (var x = x0; x <= x1; x++)
				{
					frameBuffer.SetPixel(steep ? new Vector2Int(y, x) : new Vector2Int(x, y), Color.white);
					error -= dy;
					if (error < 0)
					{
						y += ystep;
						error += dx;
					}
				}
			};

			// Sutherland–Hodgman algorithm
			// https://en.wikipedia.org/wiki/Sutherland%E2%80%93Hodgman_algorithm
			var clipPlaneNormals = new Vector4[]
			{
				new Vector4( 1.0f,  0.0f,  0.0f, 1.0f),
				new Vector4(-1.0f,  0.0f,  0.0f, 1.0f),
				new Vector4( 0.0f,  1.0f,  0.0f, 1.0f),
				new Vector4( 0.0f, -1.0f,  0.0f, 1.0f),
				new Vector4( 0.0f,  0.0f,  1.0f, 1.0f),
				new Vector4( 0.0f,  0.0f, -1.0f, 1.0f),
			};
			System.Func<Vector4, Vector4, Vector4, List<Vector4[]>> clip = (v0, v1, v2) =>
			{
				var verts = new List<Vector4>() { v0, v1, v2 };
				var tempVerts = new List<Vector4>();
				foreach (var clipPlaneNormal in clipPlaneNormals)
				{
					tempVerts.Clear();

					for (int i = 0; i < verts.Count; i++)
					{
						// Current edge
						var p0 = verts[i];
						var p1 = verts[(i + 1) % verts.Count];

						// Signed distance
						// +---------------------------------------------------------------------------+
						// | Note:                                                                     |
						// | https://brilliant.org/wiki/dot-product-distance-between-point-and-a-line/ |
						// | d = abs(v) * cos(x)                                                       |
						// | then:                                                                     |
						// |      abs(v) * abs(n) * cos(x) = dot(v, n)                                 |
						// |   => abs(v) = dot(v, n) / (abs(n) * cos(x))                               |
						// |   => abs(v) = dot(v, n) / (1.0 * cos(x))                                  |
						// | so:                                                                       |
						// | d = dot(v, n) / cos(x) * cos(x) => d = dot(v, n)                          |
						// +---------------------------------------------------------------------------+
						var d0 = Vector4.Dot(p0, clipPlaneNormal);
						var d1 = Vector4.Dot(p1, clipPlaneNormal);

						// Calculate intersection between a segment and a clip plane
						var intersect = Vector4.Lerp(p0, p1, d0 / (d0 - d1));

						// Check whether points are inside or outside
						// +----------------------------------------------------+
						// | Note;                                              |
						// | https://risalc.info/src/plane-signed-distance.html |
						// | if d > 0, the point is inside the edge.            |
						// | if d < 0, the point is outside the edge.           |
						// +----------------------------------------------------+
						if (d0 > 0.0f)
						{
							if (d1 > 0.0f)
							{
								// p0: indide, p1: indide
								tempVerts.Add(p1);
							}
							else
							{
								// p0: indide, p1: outside
								tempVerts.Add(intersect);
							}
						}
						else
						{
							if (d1 > 0.0f)
							{
								// p0: outside, p1: inside
								tempVerts.Add(intersect);
								tempVerts.Add(p1);
							}
						}

					}
					verts = new List<Vector4>(tempVerts);
				}

				if (verts.Count == 0)
				{
					return new List<Vector4[]>();
				}

				var output = new List<Vector4[]>();
				var vt1 = verts[0];
				for (int i = 1; i < verts.Count - 1; i++)
				{
					output.Add(new Vector4[] { verts[0], verts[i], verts[(i + 1) % verts.Count] });
				}

				return output;
			};

			m_cachedIndices.ForEach((submeshIndices) =>
			{
				for (var index = 0; index < submeshIndices.Length / 3; index++)
				{
					var v0 = m_cachedVerts[submeshIndices[index * 3 + 0]];
					var v1 = m_cachedVerts[submeshIndices[index * 3 + 1]];
					var v2 = m_cachedVerts[submeshIndices[index * 3 + 2]];

					var v0VS = vertexShader(new Vector4(v0.x, v0.y, v0.z, 1.0f));
					var v1VS = vertexShader(new Vector4(v1.x, v1.y, v1.z, 1.0f));
					var v2VS = vertexShader(new Vector4(v2.x, v2.y, v2.z, 1.0f));

					var vertsClipped = clip(v0VS, v1VS, v2VS);

					vertsClipped.ForEach((vs) =>
					{
						var v0PD = applyPerspevtiveDivision(vs[0]);
						var v1PD = applyPerspevtiveDivision(vs[1]);
						var v2PD = applyPerspevtiveDivision(vs[2]);

						var v0VP = applyViewport(v0PD);
						var v1VP = applyViewport(v1PD);
						var v2VP = applyViewport(v2PD);

						rasterLine(v0VP, v1VP);
						rasterLine(v1VP, v2VP);
						rasterLine(v2VP, v0VP);
					});
				}
			});
		}

	}

}