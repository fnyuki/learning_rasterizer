using LR.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace LR.Demos
{

	public class Demo05 : DemoBase
	{

		public enum ShadingMode
		{
			Lambert,
			DebugNormal
		}

		private struct ModelConstantBuffer
		{
			public Matrix4x4 M;
			public Matrix4x4 invM;
		}

		private struct ViewConstantBuffer
		{
			public Matrix4x4 V;
			public Matrix4x4 P;
		}

		private struct SceneConstantBuffer
		{
			public Vector3 normalizedLightDir;
		}

		private struct VSInput
		{
			public Vector3 position;
			public Vector3 normal;
		}

		private struct VSOutput2PSInput
		{
			public Vector4 position;
			public Vector3 normal;
		}

		private static readonly Vector4[] k_clipPlaneNormals = new Vector4[]
		{
			new Vector4( 1.0f,  0.0f,  0.0f, 1.0f),
			new Vector4(-1.0f,  0.0f,  0.0f, 1.0f),
			new Vector4( 0.0f,  1.0f,  0.0f, 1.0f),
			new Vector4( 0.0f, -1.0f,  0.0f, 1.0f),
			new Vector4( 0.0f,  0.0f,  1.0f, 1.0f),
			new Vector4( 0.0f,  0.0f, -1.0f, 1.0f),
		};

		private FrameBuffer m_frameBuffer = null;

		private AxisUtility m_axisUtility = null;

		private Vector3[] m_cachedVerts = null;

		private Vector3[] m_cachedNormals = null;

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

		[Header("Shading")]
		[SerializeField]
		private ShadingMode m_shadingMode = ShadingMode.Lambert;

		#region for Shader

		private ModelConstantBuffer m_modelConstantBuffer;
		private ViewConstantBuffer m_viewConstantBuffer;
		private SceneConstantBuffer m_sceneConstantBuffer;

		#endregion

		private VSOutput2PSInput VertexShader(VSInput input)
		{
			VSOutput2PSInput output = new VSOutput2PSInput();
			var MVP = m_viewConstantBuffer.P * m_viewConstantBuffer.V * m_modelConstantBuffer.M;
			output.position = MVP * new Vector4(input.position.x, input.position.y, input.position.z, 1.0f);
			output.normal   = m_modelConstantBuffer.invM * new Vector4(input.normal.x, input.normal.y, input.normal.z, 1.0f);
			return output;
		}

		private Color PixelShader(VSOutput2PSInput input)
		{
			if (m_shadingMode == ShadingMode.Lambert)
			{
				var col = 0.2f + 0.8f * Mathf.Max(0.0f, Vector3.Dot(input.normal, m_sceneConstantBuffer.normalizedLightDir));
				return new Color(col, col, col, 1.0f);
			}
			// else if (m_shadingMode == ShadingMode.DebugNormal)
			{
				var col = (input.normal + Vector3.one) * 0.5f;
				return new Color(col.x, col.y, col.z, 1.0f);
			}
		}

		private VSOutput2PSInput[] ExecVertexShaderStage(VSInput[] verts)
		{
			return new VSOutput2PSInput[]{
				VertexShader(verts[0]),
				VertexShader(verts[1]),
				VertexShader(verts[2])
			};
		}

		private void ExecRasterizerStage(VSOutput2PSInput[] vs2ps, System.Func<VSOutput2PSInput, Color> pixelShader)
		{
			// Sutherland–Hodgman algorithm
			// https://en.wikipedia.org/wiki/Sutherland%E2%80%93Hodgman_algorithm
			System.Func<VSOutput2PSInput[], List<VSOutput2PSInput[]>> clip = (v) =>
			{
				var verts = new List<VSOutput2PSInput>() { v[0], v[1], v[2] };
				var tempVerts = new List<VSOutput2PSInput>();
				foreach (var clipPlaneNormal in k_clipPlaneNormals)
				{
					tempVerts.Clear();

					for (int i = 0; i < verts.Count; i++)
					{
						// Current edge
						var p0 = verts[i].position;
						var p1 = verts[(i + 1) % verts.Count].position;
						var n0 = verts[i].normal;
						var n1 = verts[(i + 1) % verts.Count].normal;

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
						var intersect = new VSOutput2PSInput() { position = Vector4.Lerp(p0, p1, d0 / (d0 - d1)), normal = Vector4.Lerp(n0, n1, d0 / (d0 - d1)) };

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
								tempVerts.Add(new VSOutput2PSInput() { position = p1, normal = n1 });
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
								tempVerts.Add(new VSOutput2PSInput() { position = p1, normal = n1 });
							}
						}

					}
					verts = new List<VSOutput2PSInput>(tempVerts);
				}

				if (verts.Count == 0)
				{
					return new List<VSOutput2PSInput[]>();
				}

				var output = new List<VSOutput2PSInput[]>();
				var vt1 = verts[0];
				for (int i = 1; i < verts.Count - 1; i++)
				{
					output.Add(new VSOutput2PSInput[] { verts[0], verts[i], verts[(i + 1) % verts.Count] });
				}

				return output;
			};

			System.Func<VSOutput2PSInput, Vector3> applyPerspevtiveDivision = (input) =>
			{
				return new Vector3(input.position.x, input.position.y, input.position.z) / input.position.w;
			};

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
					m_frameBuffer.SetPixel(steep ? new Vector2Int(y, x) : new Vector2Int(x, y), Color.white);
					error -= dy;
					if (error < 0)
					{
						y += ystep;
						error += dx;
					}
				}
			};

			// Edge function
			// https://www.scratchapixel.com/lessons/3d-basic-rendering/rasterization-practical-implementation/rasterization-stage
			System.Func<Vector2[], Vector2, float> edgeFunc = (edge, point) =>
			{
				var d1 = point - edge[0];
				var d2 = edge[1] - edge[0];
				return d1.x * d2.y - d1.y * d2.x;
			};

			var vertsClipped = clip(vs2ps);

			vertsClipped.ForEach((vs) =>
			{
				var v0PD = applyPerspevtiveDivision(vs[0]);
				var v1PD = applyPerspevtiveDivision(vs[1]);
				var v2PD = applyPerspevtiveDivision(vs[2]);

				var v0VP = applyViewport(v0PD);
				var v1VP = applyViewport(v1PD);
				var v2VP = applyViewport(v2PD);

				var denom = edgeFunc(new Vector2[] { v0VP, v1VP }, v2VP);
				var isBack = denom < 0.0f;
				if (isBack/* && cullbackface*/)
				{
					return;
				}
#if false
				rasterLine(v0VP, v1VP);
				rasterLine(v1VP, v2VP);
				rasterLine(v2VP, v0VP);
#endif

				var min = Vector2.Max(Vector2.Min(Vector2.Min(v0VP, v1VP), v2VP), Vector2.zero);
				var max = Vector2.Min(Vector2.Max(Vector2.Max(v0VP, v1VP), v2VP), new Vector2(m_size.x - 1, m_size.y - 1));

				for (var y = (int)min.y; y <= (int)max.y ; y++)
				{
					for (var x = (int)min.x; x <= (int)max.x; x++)
					{
						var p = new Vector2(x + 0.5f, y + 0.5f);
						var b0 = edgeFunc(new Vector2[] { v1VP, v2VP }, p);
						var b1 = edgeFunc(new Vector2[] { v2VP, v0VP }, p);
						var b2 = edgeFunc(new Vector2[] { v0VP, v1VP }, p);
						var isInside = (b0 > 0 && b1 > 0 && b2 > 0) || (b0 < 0 && b1 < 0 && b2 < 0);
						if (!isInside)
						{
							continue;
						}
						b0 /= denom;
						b1 /= denom;
						b2 /= denom;
						var pixel = new VSOutput2PSInput()
						{
							position = b0 * v0PD + b1 * v1PD + b2 * v2PD,
							//normal = b0 * vs[0].normal + b1 * vs[1].normal + b2 * vs[2].normal
							normal = b0 / vs[0].position.w * vs[0].normal + b1 / vs[1].position.w * vs[1].normal + b2 / vs[2].position.w * vs[2].normal
						};

						var pixelPos = new Vector2Int(x, y);

						// Compare depth
						if (m_frameBuffer.GetDepth(pixelPos) < pixel.position.z)
						{
							continue;
						}
						m_frameBuffer.SetDepth(pixelPos, pixel.position.z);
						m_frameBuffer.SetPixel(pixelPos, pixelShader(pixel));

					}
				}
			});
		}

		private void ExecPipeline(List<VSInput[]> tris)
		{
			tris.ForEach((tri) =>
			{
				// Vertex Shader Stage
				var vsOutput = ExecVertexShaderStage(tri);

				// Rasterizer Stage & Pixel Shader Stage
				ExecRasterizerStage(vsOutput, PixelShader);
			});
		}

		protected override void OnStart(FrameBuffer frameBuffer)
		{
			base.OnStart(frameBuffer);
			if (m_mesh == null)
			{
				enabled = false;
				return;
			}
			m_frameBuffer = frameBuffer;
			m_axisUtility = FindObjectOfType<AxisUtility>();
			m_cachedVerts = m_mesh.vertices;
			m_cachedNormals = m_mesh.normals;
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

			m_modelConstantBuffer.M = Matrix4x4.TRS(m_position, Quaternion.identity, m_scale);
			m_modelConstantBuffer.invM = m_modelConstantBuffer.M.inverse;
			m_viewConstantBuffer.V = m_axisUtility.GetLookAt();
			m_viewConstantBuffer.P = Matrix4x4.Perspective(m_fov, frameBuffer.GetAspectRatio(), m_nearClip, m_farClip);
			m_sceneConstantBuffer.normalizedLightDir = new Vector3(0.3f, -0.8f, 1.0f).normalized;

			var mesh = new List<VSInput[]>();
			m_cachedIndices.ForEach((submeshIndices) =>
			{
				for (var index = 0; index < submeshIndices.Length / 3; index++)
				{
					mesh.Add(new VSInput[]
					{
						new VSInput() { position = m_cachedVerts[submeshIndices[index * 3 + 0]], normal = m_cachedNormals[submeshIndices[index * 3 + 0]] },
						new VSInput() { position = m_cachedVerts[submeshIndices[index * 3 + 1]], normal = m_cachedNormals[submeshIndices[index * 3 + 1]] },
						new VSInput() { position = m_cachedVerts[submeshIndices[index * 3 + 2]], normal = m_cachedNormals[submeshIndices[index * 3 + 2]] }
					});
				}
			});

			ExecPipeline(mesh);
		}

	}

}